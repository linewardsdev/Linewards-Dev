using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatService
{
    /// <summary>
    /// Spawns a creep, baking its sender's send-category tier into the health it starts with.
    /// </summary>
    /// <remarks>
    /// The tier applies HERE, at spawn, and nowhere else. A creep is something the sender bought;
    /// its strength is fixed when it is paid for. Applying the multiplier anywhere later would let
    /// a player rescue a wave that is already losing by upgrading mid-flight, and would make a
    /// creep's health depend on when you looked at it.
    ///
    /// <paramref name="healthPercent"/> defaults to 100 so a caller with no economy behind it — the
    /// combat tests, the scenario harnesses — gets authored health rather than having to thread a
    /// tier it does not model. The one caller that matters, LocalVerticalSlice.QueueSend, resolves
    /// the real percent from the sender's tiers.
    /// </remarks>
    public CreepCombatState SpawnCreep(
        EntityId entityId,
        CreepDefinition creep,
        PlayerId senderId,
        LaneId laneId,
        int healthPercent = 100)
    {
        return new CreepCombatState(
            entityId,
            creep.Id,
            senderId,
            laneId,
            CategoryTierRules.Scale(creep.MaxHealth, healthPercent),
            pathIndex: 0,
            movementProgress: 0,
            hasLeaked: false,
            maxHealth: CategoryTierRules.Scale(creep.MaxHealth, healthPercent),
            ignoresMaze: creep.IgnoresMaze);
    }

    /// <summary>
    /// Carries a leaked creep into the next opponent's lane, keeping the health it has left.
    /// </summary>
    /// <remarks>
    /// Takes <c>creep.Health</c>, not the definition's MaxHealth, so a category tier is neither
    /// re-applied nor lost when a creep crosses lanes — it is already baked into that number by
    /// <see cref="SpawnCreep"/>. Re-resolving the tier here would re-scale an already-scaled value
    /// and hand a creep more health for surviving.
    /// </remarks>
    public CreepCombatState TransferCreep(
        EntityId entityId,
        CreepCombatState creep,
        LaneId laneId)
    {
        return new CreepCombatState(
            entityId,
            creep.CreepId,
            creep.SenderId,
            laneId,
            creep.Health,
            pathIndex: 0,
            movementProgress: 0,
            hasLeaked: false,
            maxHealth: creep.MaxHealth,
            // A flyer that leaks keeps flying. Its nature travels with it exactly as its health
            // does, so it gets a fresh direct route through the next lane rather than silently
            // becoming a walker on arrival.
            ignoresMaze: creep.IgnoresMaze);
    }

    /// <summary>
    /// Advances a tick where every creep walks the same route.
    /// </summary>
    /// <remarks>
    /// Kept so the tests and scenario harnesses that predate flying creeps read unchanged. None of
    /// them contain a creep that ignores the maze, so collapsing both routes into one is exactly
    /// what they already assumed — stated here once instead of edited into fifty call sites.
    /// </remarks>
    public CombatTickResult Advance(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick) =>
        Advance(state, content, LaneRouteSet.Single(routes), tick);

    public CombatTickResult Advance(
        CombatState state,
        CombatContent content,
        LaneRouteSet routes,
        SimulationTick tick)
    {
        var events = new List<ISimulationEvent>();

        // Resolved from the PRE-movement state, so every creep in a wave sees the same aura for the
        // whole tick. Resolving after movement would make the buff depend on creep iteration order,
        // which is exactly the class of bug BuildBrambleZones was extracted to avoid.
        var auras = SupportAuraField.Resolve(state, content, tick);
        auras.BindTowersInRange(state, content, creep => ResolvePosition(creep, routes));

        var next = MoveCreeps(state, content, routes, tick, events, auras);
        next = ApplyMenderHealing(next, auras, tick, events);

        // Both damage phases share ONE mutable buffer, rather than each hit rebuilding the whole
        // state. Every path that hurts a creep — direct fire, splash, chain, artillery — used to
        // call ReplaceCreep, and RemoveCreep again on a kill, and each of those rebuilt the creep
        // array; a board where every tower hits every tick was therefore quadratic in creep count.
        // The buffer keeps the same data flow (a tower shoots, the next tower reads the result) and
        // pays for one array pair per tick instead of one per hit. See CombatDamageBuffer for why
        // this cannot reorder anything.
        //
        // Shells resolve AFTER movement, so a barrage hits whoever walked into the cell this tick
        // (which also makes the lead exactly speed x flightTicks), and BEFORE attacks, so a creep
        // the shell kills is already gone and no other tower wastes its shot on a corpse. Sharing
        // one buffer across both preserves that: a creep the shell killed has already left the
        // buffer by the time AttackWithTowers reads it.
        var buffer = new CombatDamageBuffer(next);
        ResolveLandedShells(buffer, content, routes, tick, events, auras);
        AttackWithTowers(buffer, content, routes, tick, events, auras);
        return new CombatTickResult(buffer.ToState(), events.ToArray());
    }

    /// <summary>Snapshots where every creep walks the same route. See the Advance overload above.</summary>
    public IReadOnlyList<CreepPresentationSnapshot> GetCreepSnapshots(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes) =>
        GetCreepSnapshots(state, content, LaneRouteSet.Single(routes));

    public IReadOnlyList<CreepPresentationSnapshot> GetCreepSnapshots(
        CombatState state,
        CombatContent content,
        LaneRouteSet routes)
    {
        // Built once for the whole snapshot, exactly as MoveCreeps builds it once for the whole
        // creep loop. Per creep it would be O(creeps x towers) on a path that runs every frame.
        var brambleZones = BuildBrambleZones(state.Towers, content, routes);

        return state.Creeps
            .Where(creep => !creep.IsDead && !creep.HasLeaked)
            .Select(creep => new CreepPresentationSnapshot(
                creep.EntityId,
                creep.CreepId,
                creep.SenderId,
                creep.LaneId,
                ResolvePosition(creep, routes),
                creep.Health,
                creep.MaxHealth,
                content.GetCreep(creep.CreepId).SpeedPerSecond,
                ResolveNextPosition(creep, routes),
                creep.MovementProgress,
                // The creep's own cost, not the global one. A support creep banks movement against
                // 4 rather than 3, and a renderer told otherwise interpolates it at the wrong pace
                // and visibly stutters between cells.
                content.GetCreep(creep.CreepId).MovementCost,
                // The same condition MoveCreeps brakes on, so the creep-side tell cannot disagree
                // with whether the creep is actually slowed. A flyer is never braked because it is
                // not walking the mazed route these spans are measured against.
                !content.GetCreep(creep.CreepId).IgnoresMaze
                    && IsUnderBramble(brambleZones, creep.LaneId, creep.PathIndex)))
            .ToArray();
    }

    /// <summary>
    /// The grid cells currently under a Thorn Snare's brambles, per lane, for presentation.
    /// </summary>
    /// <remarks>
    /// Exists so the client can DRAW the brake. Bramble Hold has been one of the roster's most-tuned
    /// mechanics — zone widened, capped at 3 cells, then uncapped again when the maze showed the cap
    /// contributed 0% — and until now nothing on screen said it was happening at all. The owner did
    /// not know the game had a slowing tower, which is a readability failure rather than a design one.
    ///
    /// Resolved through <see cref="BuildBrambleZones"/> rather than recomputed, so the cells drawn are
    /// by construction the cells braked. A presentation copy of "which cells does a thorn tower cover"
    /// would be a second definition of the mechanic, free to drift from the one that moves creeps —
    /// and it would drift silently, because a wrong decal looks like a design choice.
    ///
    /// Mazed route only, matching the mechanic: a flyer ignores brambles, so there is nothing to draw
    /// for it.
    /// </remarks>
    /// <summary>Bramble cells where every creep walks the same route. See the Advance overload above.</summary>
    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> GetBrambleCells(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes) =>
        GetBrambleCells(state, content, LaneRouteSet.Single(routes));

    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> GetBrambleCells(
        CombatState state,
        CombatContent content,
        LaneRouteSet routes)
    {
        var cells = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        foreach (var lane in BuildBrambleZones(state.Towers, content, routes))
        {
            if (!routes.Mazed.TryGetValue(lane.Key, out var route))
            {
                continue;
            }

            // Deduplicated by route index: two thorn towers covering the same stretch, or one tower
            // whose runs touch, would otherwise stack decals on a cell and draw it twice as dense as
            // its neighbour for no mechanical reason — the brake does not stack either.
            var seen = new HashSet<int>();
            var laneCells = new List<GridPosition>();
            foreach (var span in lane.Value)
            {
                for (var index = Math.Max(0, span.Start); index <= Math.Min(route.Count - 1, span.End); index++)
                {
                    if (seen.Add(index))
                    {
                        laneCells.Add(route[index]);
                    }
                }
            }

            if (laneCells.Count > 0)
            {
                cells[lane.Key] = laneCells;
            }
        }

        return cells;
    }

    /// <summary>
    /// A tower's presentation-layer "vision": the same target a tower would actually fire at, but
    /// checked against a wider radius than its real attack range and NOT gated by attack cooldown.
    /// This exists purely so a turret can start turning to face a creep well before it's close
    /// enough to actually be fired on (matched by <see cref="AttackWithTowers"/>'s own range/target
    /// selection) — a real shot never happens outside AttackWithTowers's own (unchanged, smaller)
    /// RangeCells check, only the visual aim-tracking gets a head start.
    /// </summary>
    private const int VisionBufferCells = 3;

    /// <summary>Aim snapshots where every creep walks the same route. See the Advance overload above.</summary>
    public IReadOnlyList<TowerAimSnapshot> GetTowerAimSnapshots(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes) =>
        GetTowerAimSnapshots(state, content, LaneRouteSet.Single(routes));

    public IReadOnlyList<TowerAimSnapshot> GetTowerAimSnapshots(
        CombatState state,
        CombatContent content,
        LaneRouteSet routes)
    {
        var results = new List<TowerAimSnapshot>();
        foreach (var tower in state.Towers)
        {
            var towerDefinition = content.GetTower(tower.TowerId);
            var visionRangeCells = towerDefinition.RangeCells + VisionBufferCells;
            var visibleTargets = state.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => CanEngage(tower, ResolvePosition(creep, routes), visionRangeCells))
                .ToArray();
            var target = SelectTarget(tower, towerDefinition, visibleTargets, routes);
            if (target is not null)
            {
                results.Add(new TowerAimSnapshot(tower.EntityId, ResolvePosition(target, routes)));
            }
        }

        return results;
    }

    private static CombatState MoveCreeps(
        CombatState state,
        CombatContent content,
        LaneRouteSet routes,
        SimulationTick tick,
        List<ISimulationEvent> events,
        SupportAuraField auras)
    {
        var brambleZones = BuildBrambleZones(state.Towers, content, routes);

        // Built once and adopted whole, rather than calling ReplaceCreep per creep. Every creep
        // moves every tick, and each ReplaceCreep rebuilt the entire creep array — so movement
        // alone cost N array rebuilds per tick, which at the 266 creeps the capture harness has
        // measured is roughly 70,000 element copies for a phase that changes each creep exactly
        // once. Creeps are appended in their existing order and untouched ones are carried across
        // unchanged, so the resulting order is identical to what per-creep replacement produced.
        var moved = new List<CreepCombatState>(state.Creeps.Count);

        foreach (var creep in state.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked)
            {
                moved.Add(creep);
                continue;
            }

            var definition = content.GetCreep(creep.CreepId);
            var route = routes.For(creep.LaneId, definition.IgnoresMaze);
            // A flyer is above the brambles, and the zone indices are measured against the mazed
            // route anyway, so they do not even address the cells it is walking.
            var braked = !definition.IgnoresMaze && IsUnderBramble(brambleZones, creep.LaneId, creep.PathIndex);
            // Floored at 1: a Pacesetter must never drive the cost to zero, which would advance a
            // creep through the whole route inside StepCreep's loop in a single tick.
            var cost = Math.Max(1, definition.MovementCost - (auras.IsPaced(creep.EntityId) ? SupportAuraField.PacesetterMovementBonus : 0));
            var (pathIndex, movement) = StepCreep(creep.PathIndex, creep.MovementProgress, definition.SpeedPerSecond, route.Count, braked, cost);

            var stepped = creep.WithMovement(pathIndex, movement);
            if (pathIndex >= route.Count - 1 && !stepped.HasLeaked)
            {
                stepped = stepped.MarkLeaked();
                events.Add(new LeakEvent(tick, stepped.SenderId, content.GetLaneOwner(stepped.LaneId), stepped.EntityId, LeakLifeLossFor(stepped.CreepId), content.GetCreep(stepped.CreepId).LeakBounty));
            }

            moved.Add(stepped);
        }

        return new CombatState(moved, state.Towers);
    }

    /// <summary>
    /// Restores health to every creep a Mender reached this tick.
    /// </summary>
    /// <remarks>
    /// Runs after movement and BEFORE shells and towers, so a creep healed this tick gets the
    /// benefit of it against this tick's fire rather than the next one's. Clamped to MaxHealth, so
    /// a Mender can undo damage but never bank health a creep never had.
    /// </remarks>
    private static CombatState ApplyMenderHealing(
        CombatState state,
        SupportAuraField auras,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        if (auras.Mended.Count == 0)
        {
            return state;
        }

        // One working copy and an id index, rather than a FirstOrDefault scan plus a full array
        // rebuild per mended creep. Healing is still applied in auras.Mended order and reads each
        // creep's already-updated state, so a creep listed twice heals twice exactly as before —
        // and the events come out in the same order, which several tests assert on.
        var healedCreeps = state.Creeps.ToArray();
        var indexByEntityId = new Dictionary<EntityId, int>(healedCreeps.Length);
        for (var index = 0; index < healedCreeps.Length; index++)
        {
            indexByEntityId[healedCreeps[index].EntityId] = index;
        }

        var changed = false;
        foreach (var entityId in auras.Mended)
        {
            if (!indexByEntityId.TryGetValue(entityId, out var creepIndex))
            {
                continue;
            }

            var creep = healedCreeps[creepIndex];
            if (creep.IsDead || creep.HasLeaked || creep.Health >= creep.MaxHealth)
            {
                continue;
            }

            var healed = Math.Min(creep.MaxHealth, creep.Health + SupportAuraField.MenderHealAmount);
            healedCreeps[creepIndex] = creep.WithHealth(healed);
            events.Add(new CreepHealedEvent(tick, creep.EntityId, healed - creep.Health, healed));
            changed = true;
        }

        return changed ? new CombatState(healedCreeps, state.Towers) : state;
    }

    private const int BrambleZoneCells = 3;

    /// <summary>
    /// Movement a creep must accumulate to advance one route cell, so a creep's real ground speed is
    /// SpeedPerSecond / BaseMovementCost cells per tick.
    /// </summary>
    /// <remarks>
    /// This exists so creep pace can be tuned WITHOUT rewriting every creep's SpeedPerSecond, which
    /// is a small integer (1-3) and cannot express anything slower than one cell per tick.
    ///
    /// At 1 — the original value — a creep moved a whole cell every tick, and at 4 ticks/second on a
    /// board where one cell is one world unit that is 4 cells/second for the SLOWEST creep in the
    /// roster. The straight lane is 16 cells, so an undefended lane was crossed in 4.0s by a Brute
    /// or Colossus and 1.33s by a Crystal Wisp or Zephyr Wraith. Two independent observations landed
    /// on this: play testing ("all creeps move too fast"), and rig_turret_walker.py measuring that
    /// 8 world units/second is roughly six body lengths per second, which no legged gait can read as
    /// anything but skating.
    ///
    /// Raising it divides every creep's speed by the same factor, so the roster's relative pacing —
    /// which the balance record is built on — is preserved exactly.
    /// </remarks>
    /// <remarks>
    /// Public because tests have to reason about it: several seed a creep on a cell and assert that
    /// one Advance steps it exactly one further, which is only true if they bank
    /// BaseMovementCost minus the creep's speed first. Leaving them to hardcode the old
    /// one-cell-per-tick assumption is what broke them when this went from 1 to 3.
    /// </remarks>
    public const int BaseMovementCost = 3;

    /// <summary>
    /// Cells under bramble cost this much movement, so a third of normal speed. Defined as a
    /// multiple of <see cref="BaseMovementCost"/> rather than a bare number, so retuning the global
    /// pace cannot silently change what Thorn Snare's brake is worth.
    /// </summary>
    /// <remarks>
    /// Raised from x2 to x3 by the fire-rate rebalance, and the reason generalises to every
    /// time-buying effect on the roster. A slow is worth the SHOTS it buys, not the seconds:
    /// shots gained is extra-time divided by cooldown. Doubling every tower's cooldown therefore
    /// halved what this was worth, and MechanicContributionTests measured it going from a 20%
    /// contribution to exactly 0% — the creeps were held longer and no tower was ready to use it.
    ///
    /// Restoring the multiplier restores the shots: x2 leaves one unit of extra time per cell, x3
    /// leaves two, which is what the doubled cooldown needs to buy the same extra shot.
    /// </remarks>
    private const int BrambleMovementCost = BaseMovementCost * 3;

    /// <summary>
    /// Advances one creep by one tick, returning its new path index and leftover movement.
    /// </summary>
    /// <remarks>
    /// Extracted so Thorn Snare's brake and any future consumer share one definition of a step. The
    /// Foundry mortar leads its target by asking this same function where the creep will be, and if
    /// it computed the lead independently every mortar shot in a braked lane would miss with no test
    /// failing.
    ///
    /// MovementProgress is a real accumulator for the first time here. Before Bramble Hold it was
    /// always zero, because every cost was 1 and SpeedPerSecond is a whole number. With
    /// BaseMovementCost above 1 it carries a genuine fraction of a cell between ticks for every
    /// creep, braked or not, which is what lets a whole-number speed express a sub-cell pace.
    /// </remarks>
    private static (int PathIndex, int Movement) StepCreep(
        int pathIndex,
        int movementProgress,
        int speedPerSecond,
        int routeCount,
        bool braked,
        int movementCost = BaseMovementCost)
    {
        // Bramble adds the SAME absolute brake to every creep rather than doubling whatever the
        // creep's own cost happens to be. Doubling would make Thorn Snare worth more against the
        // slow support creeps than against the heavies it exists to stop, which is backwards.
        var step = braked ? movementCost + (BrambleMovementCost - BaseMovementCost) : movementCost;
        var movement = movementProgress + speedPerSecond;
        while (movement >= step && pathIndex < routeCount - 1)
        {
            movement -= step;
            pathIndex++;
        }

        return (pathIndex, movement);
    }

    /// <summary>
    /// Route index spans under a Thorn Snare's brambles, per lane. Computed once per tick before the
    /// creep loop, so it cannot depend on creep iteration order.
    /// </summary>
    private static Dictionary<LaneId, List<(int Start, int End)>> BuildBrambleZones(
        IReadOnlyList<TowerCombatState> allTowers,
        CombatContent content,
        LaneRouteSet routes)
    {
        var zones = new Dictionary<LaneId, List<(int Start, int End)>>();
        foreach (var tower in allTowers)
        {
            // Mazed, deliberately. A bramble zone is a span of route INDICES, and Thorn Snare brakes
            // the cells creeps are funnelled through — which is the mazed route by definition. A
            // flyer is not on this route at all, which is why MoveCreeps skips the check for it
            // rather than trying to translate these spans onto the direct one.
            if (!content.GetTower(tower.TowerId).SlowsCreeps || !routes.Mazed.TryGetValue(tower.LaneId, out var route))
            {
                continue;
            }

            var towerZones = BrambleZonesFor(tower, content.GetTower(tower.TowerId).RangeCells, route);
            if (towerZones.Count == 0)
            {
                continue;
            }

            if (!zones.TryGetValue(tower.LaneId, out var list))
            {
                list = new List<(int Start, int End)>();
                zones[tower.LaneId] = list;
            }

            list.AddRange(towerZones);
        }

        return zones;
    }

    /// <summary>
    /// The route index spans a thorn tower covers, one span per contiguous run of in-range cells,
    /// each widened forward to at least <see cref="BrambleZoneCells"/> so a fast creep cannot step
    /// clean over that run in one tick. A tower covering no route cell returns no spans.
    /// </summary>
    /// <remarks>
    /// A single (first-covered, last-covered) span used to be returned here, which is only correct
    /// on a straight lane. A serpentine maze can carry a route past the same tower more than once —
    /// near it, away, then back — so "in range" is not contiguous, and collapsing first..last into
    /// one span braked every index in between, including the stretch the route spent nowhere near
    /// the tower. Segmenting into one span per run fixes that: cells the tower cannot reach are
    /// never braked, and every run still gets its own minimum-width guarantee.
    /// </remarks>
    private static IReadOnlyList<(int Start, int End)> BrambleZonesFor(TowerCombatState tower, int rangeCells, IReadOnlyList<GridPosition> route)
    {
        var spans = new List<(int Start, int End)>();
        var runStart = -1;
        for (var index = 0; index < route.Count; index++)
        {
            if (IsInRange(tower.Position, route[index], rangeCells))
            {
                if (runStart < 0)
                {
                    runStart = index;
                }

                continue;
            }

            if (runStart >= 0)
            {
                spans.Add(WidenedSpan(runStart, index - 1, route.Count));
                runStart = -1;
            }
        }

        if (runStart >= 0)
        {
            spans.Add(WidenedSpan(runStart, route.Count - 1, route.Count));
        }

        return spans;
    }

    // Every route cell one contiguous run actually covers, with BrambleZoneCells as a MINIMUM
    // rather than a cap.
    //
    // This has been both ways round, and the second version was calibrated against the wrong board.
    // Capping at 3 was a deliberate nerf after the zone measured as an automatic purchase — but that
    // measurement used a straight 16-cell lane, where 3 braked cells is a fifth of the whole walk.
    // Re-measured against a real 52-cell maze the same cap contributed 0%: three slowed cells out of
    // fifty-two is noise. Covering what the tower actually reaches is also the more honest rule on a
    // maze, where a snaking route can pass a single tower several times and legitimately spend much
    // longer in its brambles.
    //
    // The minimum still guarantees a speed-3 creep cannot step clean over the zone in one tick, which
    // is the constraint the width exists to satisfy.
    private static (int Start, int End) WidenedSpan(int runStart, int lastCoveredIndex, int routeCount) =>
        (runStart, Math.Min(Math.Max(lastCoveredIndex, runStart + BrambleZoneCells - 1), routeCount - 1));

    /// <summary>
    /// Whether a creep STARTS its tick inside a bramble zone. Using the start-of-tick index is what
    /// makes the halving exact — testing mid-step would let a creep pay a partial brake.
    /// </summary>
    private static bool IsUnderBramble(
        Dictionary<LaneId, List<(int Start, int End)>> zones,
        LaneId laneId,
        int pathIndex)
    {
        if (!zones.TryGetValue(laneId, out var list))
        {
            return false;
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (pathIndex >= list[index].Start && pathIndex <= list[index].End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs every tower's shot for this tick against the shared damage buffer.
    /// </summary>
    /// <remarks>
    /// Mutates <paramref name="buffer"/> rather than returning a state, so a hit costs one slot
    /// write instead of a full array rebuild. The tower loop still snapshots its running order with
    /// ToArray BEFORE anything is written, which matters more now than it did: the buffer's tower
    /// list is edited in place, so a lazy OrderBy over it would see this tick's own cooldown updates
    /// mid-iteration.
    /// </remarks>
    private static void AttackWithTowers(
        CombatDamageBuffer buffer,
        CombatContent content,
        LaneRouteSet routes,
        SimulationTick tick,
        List<ISimulationEvent> events,
        SupportAuraField auras)
    {
        foreach (var tower in buffer.Towers.OrderBy(tower => tower.EntityId.Value).ToArray())
        {
            if (tick.CompareTo(tower.NextAttackTick) < 0 || tower.HasShellInFlight)
            {
                continue;
            }

            var towerDefinition = content.GetTower(tower.TowerId);
            var availableTargets = buffer.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => CanEngage(tower, ResolvePosition(creep, routes), towerDefinition.RangeCells))
                .ToArray();
            if (IsFoundryTower(tower.TowerId))
            {
                // Artillery cannot hit something that will have left the board. Measured before this
                // filter existed: a Foundry in the last rows whiffed 100% of its shells against
                // speed-2 and speed-3 creeps, because the lead carried the impact cell onto the leak
                // index, which is filtered out of the impact check — a guaranteed miss, every shot,
                // forever. Selecting only leadable creeps turns that from a trap into the tower
                // holding fire or shooting something further back that it CAN lead.
                availableTargets = availableTargets
                    .Where(creep => CanLeadTarget(buffer.Towers, content, routes, tower, creep))
                    .ToArray();
            }

            var target = SelectTarget(tower, towerDefinition, availableTargets, routes);

            if (target is null)
            {
                continue;
            }

            var targetCell = ResolvePosition(target, routes);

            if (IsFoundryTower(tower.TowerId))
            {
                // Indirect fire: no damage now. The target was already filtered to one this tower can
                // lead, so LeadPathIndex cannot be null here.
                var targetRoute = routes.For(tower.LaneId, target.IgnoresMaze);
                var impactCell = targetRoute[LeadPathIndex(buffer.Towers, content, routes, tower, target)!.Value];
                var impactTick = new SimulationTick(tick.Value + FoundryShellFlightTicks);

                events.Add(new TowerFiredEvent(tick, tower.LaneId, tower.EntityId, tower.Position, target.EntityId, targetCell, impactTick, impactCell));
                buffer.ReplaceTower(tower
                    .WithShellInFlight(impactTick, impactCell)
                    .WithNextAttackTick(new SimulationTick(tick.Value + EffectiveCooldown(buffer.Towers, tower, towerDefinition.AttackCooldownTicks, auras))));
                continue;
            }

            events.Add(new TowerFiredEvent(tick, tower.LaneId, tower.EntityId, tower.Position, target.EntityId, targetCell, tick, targetCell));

            // Two levels, and the split is what lets a future damage multiplier (see
            // docs/CATEGORY_UPGRADE_TIERS_PLAN.md) apply to EVERYTHING a tower does rather than only its
            // primary hit.
            //
            //   baseDamage  — the tower's damage before any mechanic. Splash and any other secondary
            //                 effect must read this, so scaling it scales the whole tower.
            //   shotDamage  — baseDamage plus this tower's own mechanic. Primary hit only.
            //
            // Splash previously read towerDefinition.Damage directly, deliberately, so a hypothetical
            // tower that matched both the pulse and sapling role tokens could not have its splash
            // inflated by Grovebond. That still holds — splash reads baseDamage, not shotDamage — but it
            // now goes through the seam, so a tier multiplier applied to baseDamage reaches it.
            var baseDamage = BaseDamageFor(tower, towerDefinition);

            var shotDamage = baseDamage;
            if (IsSaplingTower(tower.TowerId))
            {
                shotDamage += GrovebondBonus(buffer.Towers, tower, baseDamage);
            }
            else if (IsSporeTower(tower.TowerId))
            {
                shotDamage = RotDamage(content, target.CreepId, baseDamage);
            }
            else if (IsBloomheartTower(tower.TowerId))
            {
                shotDamage += CrowdBloomBonus(buffer.Creeps, routes, tower, target, baseDamage);
            }

            DamageCreep(buffer, content, tower, target, shotDamage, tick, events, auras);

            if (IsTeslaTower(tower.TowerId))
            {
                ChainArc(buffer, content, routes, tower, target, baseDamage, auras, tick, events);
            }

            if (IsPulseTower(tower.TowerId))
            {
                var splashDamage = Math.Max(1, baseDamage / 2);
                var targetPosition = ResolvePosition(target, routes);
                var splashTargets = buffer.Creeps
                    .Where(creep => !creep.EntityId.Equals(target.EntityId))
                    .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                    .Where(creep => IsInRange(targetPosition, ResolvePosition(creep, routes), 1))
                    .OrderByDescending(creep => creep.PathIndex)
                    .ThenBy(creep => creep.EntityId.Value)
                    .Take(2)
                    .ToArray();

                foreach (var splashTarget in splashTargets)
                {
                    DamageCreep(buffer, content, tower, splashTarget, splashDamage, tick, events, auras);
                }
            }

            buffer.ReplaceTower(tower.WithNextAttackTick(
                new SimulationTick(tick.Value + EffectiveCooldown(buffer.Towers, tower, towerDefinition.AttackCooldownTicks, auras))));
        }
    }

    private const int FoundryShellFlightTicks = 2;

    private static bool IsFoundryTower(ContentId towerId) => ContainsRole(towerId, "foundry");

    private static bool IsTeslaTower(ContentId towerId) => ContainsRole(towerId, "tesla");

    private static bool IsRepairDroneTower(ContentId towerId) => ContainsRole(towerId, "repair_drone");

    private static bool IsElderCanopyTower(ContentId towerId) => ContainsRole(towerId, "elder_canopy");

    private const int ChainArcMaxHops = 2;
    private const int ChainArcHopRangeCells = 2;

    /// <summary>
    /// Whether a tower has a Repair Drone Spire servicing it — orthogonally adjacent, same owner, same
    /// lane.
    /// </summary>
    private static bool IsServicedByDrone(IReadOnlyList<TowerCombatState> allTowers, TowerCombatState tower)
    {
        foreach (var other in allTowers)
        {
            if (other.EntityId.Equals(tower.EntityId))
            {
                continue;
            }

            if (!other.LaneId.Equals(tower.LaneId) || !other.OwnerId.Equals(tower.OwnerId))
            {
                continue;
            }

            if (IsRepairDroneTower(other.TowerId) && IsOrthogonallyAdjacent(tower.Position, other.Position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A tower's attack cooldown after any Repair Drone Spire servicing it — one tick faster, never
    /// below one.
    /// </summary>
    /// <remarks>
    /// This replaced a +1 RANGE buff, which measurement showed was very nearly inert. Over 12 creeps an
    /// adjacent Arrow Tower gained 2 damage — one extra shot in the whole run — against 48 unaided.
    /// RepairDroneValueTests has the numbers.
    ///
    /// The reason is worth recording, because it generalises: under continuous pressure every tower is
    /// COOLDOWN-limited, not range-limited. Extra reach only helps a tower that is idle for want of a
    /// target, so the old buff paid out in the sparse case where you did not need it and paid nothing in
    /// the dense case where you did — exactly backwards for a support tower.
    ///
    /// Cooldown is the binding constraint, so that is what the drone now relieves. Bonuses do not stack:
    /// two drones beside one tower still give one tick, or a drone sandwich would trivialise cadence.
    /// </remarks>
    /// <remarks>
    /// The one place a tower's rate of fire is decided, which is why the Binder's brake belongs here
    /// rather than at the two call sites that arm the cooldown. Repair Drone already shortens the
    /// same number, so a serviced tower inside a Binder's reach simply comes back to its authored
    /// rate — the two cancel, which is the reading a player would expect without being told.
    /// </remarks>
    /// <summary>Ticks a Repair Drone takes off a serviced tower's cooldown.</summary>
    /// <remarks>
    /// Two, not one, and it moved with the fire-rate rebalance rather than independently. A flat tick
    /// of relief is worth whatever a tick is worth: against the old cooldowns of 1 to 6 it was a
    /// large fraction of a shot, and doubling every cooldown silently halved the drone's entire
    /// contribution without anything in its own definition changing. Scaling it alongside keeps the
    /// mechanic worth what it was tuned to be worth.
    /// </remarks>
    private const int DroneCooldownReliefTicks = 2;

    private static int EffectiveCooldown(IReadOnlyList<TowerCombatState> allTowers, TowerCombatState tower, int authoredCooldown, SupportAuraField auras)
    {
        var cooldown = IsServicedByDrone(allTowers, tower) ? Math.Max(1, authoredCooldown - DroneCooldownReliefTicks) : authoredCooldown;
        return auras.IsBound(tower.EntityId) ? cooldown + SupportAuraField.BinderCooldownExtraTicks : cooldown;
    }

    /// <summary>
    /// Where a creep will stand once a Foundry shell has finished its flight.
    /// </summary>
    /// <remarks>
    /// Uses the same <see cref="StepCreep"/> the movement phase uses, including the bramble brake, so
    /// the lead cannot drift from actual movement. Returns null when the creep would reach the end of
    /// the route — it leaks there and is no longer a valid impact, so a shell aimed at it is a
    /// guaranteed miss.
    /// </remarks>
    private static int? LeadPathIndex(
        IReadOnlyList<TowerCombatState> allTowers,
        CombatContent content,
        LaneRouteSet routes,
        TowerCombatState tower,
        CreepCombatState target)
    {
        var creepDefinition = content.GetCreep(target.CreepId);
        var route = routes.For(tower.LaneId, creepDefinition.IgnoresMaze);
        var brambleZones = BuildBrambleZones(allTowers, content, routes);
        var index = target.PathIndex;
        var movement = target.MovementProgress;

        for (var step = 0; step < FoundryShellFlightTicks; step++)
        {
            var braked = IsUnderBramble(brambleZones, target.LaneId, index);
            (index, movement) = StepCreep(index, movement, creepDefinition.SpeedPerSecond, route.Count, braked, creepDefinition.MovementCost);
        }

        // route.Count - 1 is the leak index: a creep arriving there is marked leaked and filtered out
        // of the impact check, so it can never be hit.
        return index < route.Count - 1 ? index : null;
    }

    private static bool CanLeadTarget(
        IReadOnlyList<TowerCombatState> allTowers,
        CombatContent content,
        LaneRouteSet routes,
        TowerCombatState tower,
        CreepCombatState target) =>
        LeadPathIndex(allTowers, content, routes, tower, target) is not null;

    /// <summary>
    /// Lands any Foundry shell whose impact tick has arrived, damaging every live creep standing on
    /// the impact cell.
    /// </summary>
    /// <remarks>
    /// Disarms before damaging, so nothing can double-resolve. Re-reads the damage buffer per shell
    /// rather than taking one snapshot up front, because DamageCreep removes the dead mid-iteration
    /// and replacing a creep that is no longer there throws.
    ///
    /// Damage goes through DamageCreep rather than being applied directly: that is what applies
    /// AdjustDamageForRoles (a Shade on the impact cell still halves the hit), and what emits the
    /// damage, kill and bounty events the renderer and economy depend on.
    ///
    /// A shell can legitimately hit nothing — the creep it led may have died, leaked, or the lane may
    /// have re-pathed. That whiff is the point of aiming at ground rather than at an entity, and it
    /// is deterministic, since placement arrives as a command in the tick stream.
    /// </remarks>
    /// <summary>
    /// Arcs a Tesla Coil Spire's shot from creep to creep BACK down the queue, halving each hop.
    /// </summary>
    /// <remarks>
    /// Distinct from Pulse's splash on purpose. Pulse hits everything within one cell of the target
    /// at a flat half damage, so it rewards a CLUMP. The chain walks a LINE and decays, so it rewards
    /// a column of creeps in single file — what a trickle send looks like. The two towers answer
    /// different send shapes.
    ///
    /// The direction is BACKWARD, toward spawn, and that is load-bearing rather than flavour. Target
    /// selection picks the front-most creep, so an arc that hopped forward would look for creeps ahead
    /// of the leader and find none — the mechanic would have been a total no-op in every real game.
    /// Hitting the leader and jumping back through the queue behind it is also the more natural read
    /// of a lightning arc.
    ///
    /// The hop test is "at or behind", not "strictly behind". Strictly behind measured at 0%
    /// contribution against a stacked send, which is the modal case: a quantity-N send puts every creep
    /// on the SAME path index, so a strict inequality excluded all of them and the chain died on the
    /// leader. This is the same failure that killed Reaping Bloom — a comparison that ties in the common
    /// case — and it was caught the same way, by measuring against a stat-identical control rather than
    /// by reading the code.
    ///
    /// Already-struck creeps are excluded by entity id, so "at or behind" cannot re-hit the primary or
    /// loop, and ordering by descending index then entity id keeps the chain a property of the board
    /// rather than of iteration order.
    ///
    /// Hops decay from BASE damage, not from the primary hit. Identical today, since nothing modifies a
    /// Tesla's shot — but reading base is what makes the whole chain scale with a tower-line tier in one
    /// place, and it matches the rule Pulse's splash follows: secondary effects read base, so a mechanic
    /// bonus can never propagate through them.
    /// </remarks>
    private static void ChainArc(
        CombatDamageBuffer buffer,
        CombatContent content,
        LaneRouteSet routes,
        TowerCombatState tower,
        CreepCombatState primary,
        int baseDamage,
        SupportAuraField auras,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var damage = baseDamage;
        var fromIndex = primary.PathIndex;
        var struck = new List<long> { primary.EntityId.Value };

        for (var hop = 0; hop < ChainArcMaxHops; hop++)
        {
            damage = Math.Max(1, damage / 2);
            var arcRoute = routes.For(tower.LaneId, primary.IgnoresMaze);
            var fromPosition = arcRoute[Math.Min(fromIndex, arcRoute.Count - 1)];

            // Re-read per hop, exactly as before: a hop reads the buffer AFTER the previous hop's
            // damage, so a creep the chain has already killed is gone rather than arced to again.
            var link = buffer.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => !struck.Contains(creep.EntityId.Value))
                .Where(creep => creep.PathIndex <= fromIndex)
                .Where(creep => IsInRange(fromPosition, ResolvePosition(creep, routes), ChainArcHopRangeCells))
                .OrderByDescending(creep => creep.PathIndex)
                .ThenBy(creep => creep.EntityId.Value)
                .FirstOrDefault();

            if (link is null)
            {
                break;
            }

            struck.Add(link.EntityId.Value);
            fromIndex = link.PathIndex;
            DamageCreep(buffer, content, tower, link, damage, tick, events, auras);
        }
    }

    private static void ResolveLandedShells(
        CombatDamageBuffer buffer,
        CombatContent content,
        LaneRouteSet routes,
        SimulationTick tick,
        List<ISimulationEvent> events,
        SupportAuraField auras)
    {
        foreach (var tower in buffer.Towers.OrderBy(tower => tower.EntityId.Value).ToArray())
        {
            if (tower.ShellImpactTick is not { } impactTick || tick.CompareTo(impactTick) < 0)
            {
                continue;
            }

            var impactCell = tower.ShellImpactCell!.Value;
            buffer.ReplaceTower(tower.WithoutShellInFlight());

            var towerDefinition = content.GetTower(tower.TowerId);
            var hit = buffer.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => ResolvePosition(creep, routes).Equals(impactCell))
                .OrderBy(creep => creep.EntityId.Value)
                .ToArray();

            foreach (var creep in hit)
            {
                // Shell damage goes through the same accessor as a direct shot, so a tower-line tier
                // reaches artillery too. This was the third site reading the authored damage directly,
                // after Pulse's splash and Chain Arc, and the easiest to miss because it resolves in a
                // different phase where baseDamage is not in scope.
                DamageCreep(buffer, content, tower, creep, BaseDamageFor(tower, towerDefinition), tick, events, auras);
            }
        }
    }

    /// <summary>
    /// How far along its OWN route a creep is, scaled so two different routes can be compared.
    /// </summary>
    /// <remarks>
    /// Targeting has always meant "whoever is closest to leaking", and while every creep in a lane
    /// walked the same route, the raw path index said exactly that. It stops saying it the moment a
    /// second route exists: a flyer at index 15 of a 16-cell direct route is one step from leaking,
    /// and a walker at index 20 of a 34-cell maze is not yet halfway — but the raw comparison ranks
    /// the walker first and lets the leak through.
    ///
    /// Scaled by 1,000 and kept in integers, because the simulation is deterministic and no float
    /// may reach a targeting decision. Ties fall through to entity id as they always did, which is
    /// what makes send ORDER the tie-break within a wave: the pack is bought first and takes the
    /// lower ids, so towers shoot it before the support sent behind it.
    ///
    /// This is a no-op for every match that contains no flyer, since one route means one scale.
    /// </remarks>
    private const int ProgressScale = 1_000;

    private static int RouteProgress(CreepCombatState creep, IReadOnlyList<GridPosition> route) =>
        route.Count <= 1 ? ProgressScale : creep.PathIndex * ProgressScale / (route.Count - 1);

    private static CreepCombatState? SelectTarget(
        TowerCombatState tower,
        TowerDefinition towerDefinition,
        IReadOnlyList<CreepCombatState> targets,
        LaneRouteSet routes)
    {
        if (IsElderCanopyTower(tower.TowerId))
        {
            // Targets the creep FURTHEST BACK in range, not the leader. With the roster's longest
            // reach (5) that means it engages arrivals at the mouth of the lane, softening a wave
            // before it reaches everything else — area denial rather than last-ditch defence. Nothing
            // else on the roster targets back-most, so the tell is that it visibly shoots the far
            // creep while its neighbours all shoot the near one.
            return targets
                .OrderBy(creep => RouteProgress(creep, routes.For(creep.LaneId, creep.IgnoresMaze)))
                .ThenBy(creep => creep.EntityId.Value)
                .FirstOrDefault();
        }

        if (IsPrismTower(tower.TowerId))
        {
            return targets
                .OrderByDescending(creep => IsShadeCreep(creep.CreepId))
                .ThenByDescending(creep => creep.Health)
                .ThenByDescending(creep => RouteProgress(creep, routes.For(creep.LaneId, creep.IgnoresMaze)))
                .ThenBy(creep => creep.EntityId.Value)
                .FirstOrDefault();
        }

        return targets
            .OrderByDescending(creep => RouteProgress(creep, routes.For(creep.LaneId, creep.IgnoresMaze)))
            .ThenBy(creep => creep.EntityId.Value)
            .FirstOrDefault();
    }

    /// <summary>
    /// The one place a creep loses health, for all four damage paths.
    /// </summary>
    /// <remarks>
    /// Writes into the shared buffer instead of returning a rebuilt state. This is where item 23's
    /// quadratic cost actually lived: this method runs once per damaging hit, and it used to rebuild
    /// the entire creep array here and AGAIN on a kill, to change one entry.
    ///
    /// The order of the two events is unchanged and load-bearing — damaged, then killed — as is the
    /// fact that the removal happens after both are raised.
    /// </remarks>
    private static void DamageCreep(
        CombatDamageBuffer buffer,
        CombatContent content,
        TowerCombatState tower,
        CreepCombatState target,
        int baseDamage,
        SimulationTick tick,
        List<ISimulationEvent> events,
        SupportAuraField auras)
    {
        var damage = AdjustDamageForRoles(tower.TowerId, target.CreepId, baseDamage);

        // Every path that hurts a creep — direct fire, splash, chain, artillery — arrives here, so
        // the shield is applied once rather than at four call sites that could drift apart.
        // Floored at 1: a shielded creep still takes something from every hit, or a low-damage tower
        // would round to zero and be unable to kill it at all.
        if (auras.IsShielded(target.EntityId))
        {
            damage = Math.Max(1, damage - (damage * SupportAuraField.BulwarkDamageReductionPercent / 100));
        }
        var damaged = target.WithHealth(Math.Max(0, target.Health - damage));
        buffer.ReplaceCreep(damaged);
        events.Add(new CreepDamagedEvent(tick, tower.OwnerId, tower.LaneId, tower.EntityId, tower.Position, damaged.EntityId, damage));

        if (damaged.IsDead)
        {
            events.Add(new CreepKilledEvent(tick, damaged.EntityId, tower.OwnerId, content.GetCreep(damaged.CreepId).KillBounty));
            buffer.RemoveCreep(damaged.EntityId);
        }
    }

    private static int AdjustDamageForRoles(ContentId towerId, ContentId creepId, int damage)
    {
        if (IsShadeCreep(creepId) && !IsControlTower(towerId) && !IsPrismTower(towerId))
        {
            return Math.Max(1, (damage + 1) / 2);
        }

        return damage;
    }

    private static GridPosition ResolvePosition(
        CreepCombatState creep,
        LaneRouteSet routes)
    {
        var route = routes.For(creep.LaneId, creep.IgnoresMaze);
        return route[Math.Min(creep.PathIndex, route.Count - 1)];
    }

    /// <summary>The cell a creep is walking toward, clamped to the route end.</summary>
    private static GridPosition ResolveNextPosition(
        CreepCombatState creep,
        LaneRouteSet routes)
    {
        var route = routes.For(creep.LaneId, creep.IgnoresMaze);
        return route[Math.Min(creep.PathIndex + 1, route.Count - 1)];
    }

    private static bool IsInRange(GridPosition tower, GridPosition creep, int rangeCells)
    {
        var distance = Math.Abs(tower.X - creep.X) + Math.Abs(tower.Y - creep.Y);
        return distance <= rangeCells;
    }

    /// <summary>
    /// Whether a tower may engage a creep at <paramref name="creepPosition"/>.
    /// </summary>
    /// <remarks>
    /// Everything except the Barricade Bastion is a plain range check. The Barricade is a fixed
    /// emplacement: it never turns, and fires up-lane only, so it cannot engage a creep that has
    /// already walked past its own row. Route Y is monotonically non-decreasing on every reachable
    /// layout (asserted in PathingTests), so <c>creepY &lt;= towerY</c> is exactly "has not passed
    /// me yet".
    ///
    /// This must be used by BOTH candidate filters — GetTowerAimSnapshots and AttackWithTowers. If
    /// only one uses it, the turret visibly tracks a creep it will never shoot.
    ///
    /// The restriction is paid for in reach and damage (range 1 to 2, damage 3 to 5), and it is a
    /// net improvement even before the mechanic: at range 1 with a full diamond, 66 of the 110 legal
    /// placements could hit nothing at all. At range 2 with the up-lane half-plane that falls to 34,
    /// and those 34 are columns 0 and 6, which no range-2 tower can reach the lane from anyway.
    /// </remarks>
    private static bool CanEngage(TowerCombatState tower, GridPosition creepPosition, int rangeCells)
    {
        if (!IsInRange(tower.Position, creepPosition, rangeCells))
        {
            return false;
        }

        return !IsBarricadeTower(tower.TowerId) || creepPosition.Y <= tower.Position.Y;
    }

    private static bool IsBarricadeTower(ContentId towerId) => ContainsRole(towerId, "barricade");

    /// <summary>Percent of base damage each bonded Grove neighbour adds.</summary>
    /// <remarks>
    /// A PERCENTAGE of base damage rather than a flat +1, and that is the point. A flat bonus silently
    /// decays as base damage grows: at the Sapling's authored 2 damage, +1 per neighbour is +50% each, but
    /// against a tier-scaled base (docs/CATEGORY_UPGRADE_TIERS_PLAN.md) the same +1 would be an ever
    /// smaller share, so investing in the line would quietly weaken its own mechanic. 50% reproduces
    /// today's numbers exactly at damage 2 — isolated 2, three neighbours 2 + 3 = 5 — and scales with any
    /// future multiplier.
    /// </remarks>
    private const int GrovebondPercentPerNeighbour = 50;

    private const int GrovebondMaxNeighbours = 3;

    /// <summary>
    /// Extra damage a Sapling Sentinel gets from orthogonally adjacent Grove towers of the same
    /// owner and lane, capped at <see cref="GrovebondMaxNeighbours"/>.
    /// </summary>
    /// <remarks>
    /// The Sapling is the cheapest tower on the roster (10 gold) and deliberately weak alone. This
    /// is what makes a cluster of them worth more than the sum of its parts, and it is
    /// self-limiting in a way that resists a runaway: in a solid block the highest-bonus towers are
    /// the interior ones, and interior towers see no route cells, so they never fire.
    /// </remarks>
    private static int GrovebondBonus(IReadOnlyList<TowerCombatState> allTowers, TowerCombatState sapling, int baseDamage)
    {
        var adjacent = 0;
        foreach (var other in allTowers)
        {
            if (other.EntityId.Equals(sapling.EntityId))
            {
                continue;
            }

            if (!other.LaneId.Equals(sapling.LaneId) || !other.OwnerId.Equals(sapling.OwnerId))
            {
                continue;
            }

            if (IsGroveTower(other.TowerId) && IsOrthogonallyAdjacent(sapling.Position, other.Position))
            {
                adjacent++;
            }
        }

        var bonded = Math.Min(GrovebondMaxNeighbours, adjacent);
        return baseDamage * GrovebondPercentPerNeighbour * bonded / 100;
    }

    /// <summary>
    /// Manhattan distance 1, so diagonals do not bond. Distance 0 cannot occur — placement rejects
    /// an occupied cell — so this is exactly "shares an edge".
    /// </summary>
    private static bool IsOrthogonallyAdjacent(GridPosition a, GridPosition b) => IsInRange(a, b, 1);

    private static bool IsGroveTower(ContentId towerId) =>
        ContainsRole(towerId, "sapling") || ContainsRole(towerId, "bloomheart") || ContainsRole(towerId, "thorn")
        || ContainsRole(towerId, "spore") || ContainsRole(towerId, "canopy");

    private static bool IsSaplingTower(ContentId towerId) => ContainsRole(towerId, "sapling");

    private static bool IsBloomheartTower(ContentId towerId) => ContainsRole(towerId, "bloomheart");

    /// <summary>Percent of base damage each additional creep on the target's cell adds.</summary>
    /// <remarks>
    /// Proportional for the same reason as Grovebond: a flat +1 shrinks as a share of base damage. 25%
    /// reproduces today's numbers at the Totem's authored 4 — two others on the cell give 4 + 2 = 6, a
    /// full crowd gives 4 + 3 = 7 — and scales with any future multiplier.
    /// </remarks>
    private const int CrowdBloomPercentPerCreep = 25;

    private const int CrowdBloomMaxCreeps = 3;

    /// <summary>
    /// Extra damage a Bloomheart Totem gets for every OTHER creep sharing its target's cell, capped at
    /// <see cref="CrowdBloomMaxCreeps"/>.
    /// </summary>
    /// <remarks>
    /// This replaced "Reaping Bloom" (finish the weakest, else lead) after measuring it. That rule was
    /// inert: BloomheartDivergenceTests ran the totem against a stat-identical baseline tower and it
    /// picked a different creep in 0% of shots in every organic scenario — same-type send, trickle, and
    /// both of those with a second tower chipping the group. The only divergence came from a wounded
    /// trailer seeded by hand. The reason is that a quantity-N send spawns all N creeps on one cell in
    /// one tick at full health, and they move as a pure function of position and speed, so every one of
    /// that rule's tie-breakers tied and the last one picked the same creep the default rule would.
    ///
    /// Crowd Bloom keys off exactly the thing that made the old rule inert. A stacked send is the modal
    /// case, so the mechanic now engages in the common situation rather than an exotic one, and the
    /// tower answers a dense queue by deleting its leader.
    ///
    /// Not a duplicate of Pulse's splash, which is the other answer to a clump: Pulse SPREADS half
    /// damage across the group and thins it, while this CONCENTRATES on one creep because the others
    /// are there. Thin the crowd or punch through it — different answers to the same board.
    /// </remarks>
    private static int CrowdBloomBonus(
        IReadOnlyList<CreepCombatState> allCreeps,
        LaneRouteSet routes,
        TowerCombatState tower,
        CreepCombatState target,
        int baseDamage)
    {
        var targetCell = ResolvePosition(target, routes);
        var crowd = 0;
        foreach (var creep in allCreeps)
        {
            if (creep.EntityId.Equals(target.EntityId) || creep.IsDead || creep.HasLeaked)
            {
                continue;
            }

            if (creep.LaneId.Equals(tower.LaneId) && ResolvePosition(creep, routes).Equals(targetCell))
            {
                crowd++;
            }
        }

        var counted = Math.Min(CrowdBloomMaxCreeps, crowd);
        return baseDamage * CrowdBloomPercentPerCreep * counted / 100;
    }


    /// <summary>
    /// Target max health that buys one full multiple of the Spore Cloud Bloom's base damage.
    /// </summary>
    /// <remarks>
    /// 24 is not arbitrary: it is the old formula's divisor of 6 times the tower's authored damage of 4, so
    /// this reproduces every value the previous version produced across the whole roster — 4 up to Brute at
    /// 24 health, then 5 / 5 / 6 / 7 / 8 / 10 / 15 for Serpent through Colossus.
    /// </remarks>
    private const int RotHealthPerDamageMultiple = 24;

    /// <summary>
    /// Spore Cloud Bloom's damage: a fraction of the target's AUTHORED max health, floored at the
    /// tower's own damage.
    /// </summary>
    /// <remarks>
    /// This makes it irrelevant against chaff and the roster's hardest counter to anything fat.
    /// MaxHealth is authored content and never mutated, so the value is constant per creep TYPE and
    /// cannot be gamed by chipping the creep down first.
    ///
    /// Rewritten from max(baseDamage, maxHealth / 6) to a PERCENTAGE OF BASE for the same reason Grovebond
    /// and Crowd Bloom were: so a tower-line tier actually improves it. Under the old form a tier raised
    /// only the FLOOR, so investing in GROVE did nothing for this tower against exactly the fat targets it
    /// exists to answer — the rot term dominated the floor and ignored the multiplier entirely.
    ///
    /// Deliberate consequence worth noting: a creep-category tier raises max health, so an attacker
    /// upgrading their creeps makes this tower hit harder. That is right for the roster's anti-fat counter,
    /// and it is one of the few places a defender benefits from the attacker's investment.
    ///
    /// Integer arithmetic throughout. The shape is still a step rather than a curve: it only exceeds base
    /// above 24 max health, so it stays inert on 8 of the 15 creeps.
    /// </remarks>
    private static int RotDamage(CombatContent content, ContentId creepId, int baseDamage)
    {
        var maxHealth = content.GetCreep(creepId).MaxHealth;
        var percent = Math.Max(100, maxHealth * 100 / RotHealthPerDamageMultiple);
        return baseDamage * percent / 100;
    }

    private static bool IsSporeTower(ContentId towerId) => ContainsRole(towerId, "spore");

    /// <summary>
    /// A tower's damage before any per-tower mechanic applies.
    /// </summary>
    /// <remarks>
    /// The single place the tower-line tier multiplier applies. Every path that deals damage on a
    /// tower's behalf reads this — the primary shot, Pulse's splash, Chain Arc's hops and the
    /// Foundry's shell — so scaling here scales the whole tower rather than only the shot the
    /// player happens to be looking at.
    ///
    /// The seam was built as a pass-through ahead of this feature precisely so the multiplier
    /// would be one change against one function instead of a hunt through four call sites, two of
    /// which were originally found only by grepping for the raw Damage field after the first two
    /// were fixed. That is now cashed in: the tier arrives here and nowhere else.
    ///
    /// Reads the TOWER's own tier, not its owner's current line tier. Buying a line tier raises the
    /// tier new towers are built at and does nothing for the ones already standing; bringing an
    /// existing tower up costs gold, one tower at a time. Reading the owner's tier here instead
    /// would hand every placed tower the upgrade for free and delete that decision.
    ///
    /// This mirrors how creep health already works — baked in when the unit is paid for — so both
    /// sides of the roster now behave the same way: an upgrade applies to what you buy next, not
    /// retroactively to what you already own.
    /// </remarks>
    private static int BaseDamageFor(TowerCombatState tower, TowerDefinition towerDefinition) =>
        CategoryTierRules.Scale(towerDefinition.Damage, CategoryTierRules.TowerDamagePercentFor(tower.Tier));

    private static bool IsPulseTower(ContentId towerId) => ContainsRole(towerId, "pulse");

    private static bool IsPrismTower(ContentId towerId) => ContainsRole(towerId, "prism");

    private static bool IsControlTower(ContentId towerId) => ContainsRole(towerId, "control");

    private static bool IsShadeCreep(ContentId creepId) => ContainsRole(creepId, "shade") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "invisible");

    // "siege" alone missed creep.colossus — its content id doesn't contain the word, but its full
    // name is "Siege Colossus" and, at 90 max health, it is the highest-health creep in the roster
    // (creep.siege itself is 48). Matching only the id meant the biggest, most expensive creep to
    // leak cost the same one life as the cheapest, while the smaller Siege cost two — the opposite
    // of what a "siege" classification is for. See OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items; the broader question of
    // whether this substring approach should become a real per-creep content field instead of a
    // name heuristic is a design decision left open, not resolved here.
    private static Lives LeakLifeLossFor(ContentId creepId) =>
        ContainsRole(creepId, "siege") || ContainsRole(creepId, "colossus") ? new Lives(2) : new Lives(1);

    private static bool ContainsRole(ContentId contentId, string role) => contentId.Value.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0;
}
