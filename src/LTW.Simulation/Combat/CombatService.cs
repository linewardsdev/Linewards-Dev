using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatService
{
    public CreepCombatState SpawnCreep(
        EntityId entityId,
        CreepDefinition creep,
        PlayerId senderId,
        LaneId laneId)
    {
        return new CreepCombatState(
            entityId,
            creep.Id,
            senderId,
            laneId,
            creep.MaxHealth,
            pathIndex: 0,
            movementProgress: 0,
            hasLeaked: false);
    }

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
            hasLeaked: false);
    }

    public CombatTickResult Advance(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick)
    {
        var events = new List<ISimulationEvent>();
        var next = MoveCreeps(state, content, routes, tick, events);
        // Shells resolve AFTER movement, so a barrage hits whoever walked into the cell this tick
        // (which also makes the lead exactly speed x flightTicks), and BEFORE attacks, so a creep
        // the shell kills is already gone and no other tower wastes its shot on a corpse.
        next = ResolveLandedShells(next, content, routes, tick, events);
        next = AttackWithTowers(next, content, routes, tick, events);
        return new CombatTickResult(next, events.ToArray());
    }

    public IReadOnlyList<CreepPresentationSnapshot> GetCreepSnapshots(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes)
    {
        return state.Creeps
            .Where(creep => !creep.IsDead && !creep.HasLeaked)
            .Select(creep => new CreepPresentationSnapshot(
                creep.EntityId,
                creep.CreepId,
                creep.SenderId,
                creep.LaneId,
                ResolvePosition(creep, routes),
                creep.Health,
                content.GetCreep(creep.CreepId).MaxHealth))
            .ToArray();
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

    public IReadOnlyList<TowerAimSnapshot> GetTowerAimSnapshots(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes)
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
            var target = SelectTarget(tower, towerDefinition, visibleTargets);
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
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        var brambleZones = BuildBrambleZones(state, content, routes);

        foreach (var creep in state.Creeps.Where(creep => !creep.IsDead && !creep.HasLeaked).ToArray())
        {
            var definition = content.GetCreep(creep.CreepId);
            var route = routes[creep.LaneId];
            var braked = IsUnderBramble(brambleZones, creep.LaneId, creep.PathIndex);
            var (pathIndex, movement) = StepCreep(creep.PathIndex, creep.MovementProgress, definition.SpeedPerSecond, route.Count, braked);

            var moved = creep.WithMovement(pathIndex, movement);
            if (pathIndex >= route.Count - 1 && !moved.HasLeaked)
            {
                moved = moved.MarkLeaked();
                events.Add(new LeakEvent(tick, moved.SenderId, content.GetLaneOwner(moved.LaneId), moved.EntityId, LeakLifeLossFor(moved.CreepId), content.GetCreep(moved.CreepId).LeakBounty));
            }

            next = next.ReplaceCreep(moved);
        }

        return next;
    }

    private const int BrambleZoneCells = 3;

    /// <summary>Cells under bramble cost this much movement instead of 1, so exactly half speed.</summary>
    private const int BrambleMovementCost = 2;

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
    /// always zero, because every cost was 1 and SpeedPerSecond is a whole number.
    /// </remarks>
    private static (int PathIndex, int Movement) StepCreep(
        int pathIndex,
        int movementProgress,
        int speedPerSecond,
        int routeCount,
        bool braked)
    {
        var step = braked ? BrambleMovementCost : 1;
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
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes)
    {
        var zones = new Dictionary<LaneId, List<(int Start, int End)>>();
        foreach (var tower in state.Towers)
        {
            if (!IsThornTower(tower.TowerId) || !routes.TryGetValue(tower.LaneId, out var route))
            {
                continue;
            }

            var zone = BrambleZoneFor(tower, content.GetTower(tower.TowerId).RangeCells, route);
            if (zone is null)
            {
                continue;
            }

            if (!zones.TryGetValue(tower.LaneId, out var list))
            {
                list = new List<(int Start, int End)>();
                zones[tower.LaneId] = list;
            }

            list.Add(zone.Value);
        }

        return zones;
    }

    /// <summary>
    /// The route indices a thorn tower covers, widened forward to at least
    /// <see cref="BrambleZoneCells"/> so a fast creep cannot step clean over the whole zone in one
    /// tick. A tower covering no route cell has no zone.
    /// </summary>
    private static (int Start, int End)? BrambleZoneFor(TowerCombatState tower, int rangeCells, IReadOnlyList<GridPosition> route)
    {
        var first = -1;
        var last = -1;
        for (var index = 0; index < route.Count; index++)
        {
            if (IsInRange(tower.Position, route[index], rangeCells))
            {
                if (first < 0)
                {
                    first = index;
                }

                last = index;
            }
        }

        if (first < 0)
        {
            return null;
        }

        // Every route cell the tower can reach, with BrambleZoneCells as a MINIMUM rather than a cap.
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
        var end = Math.Min(Math.Max(last, first + BrambleZoneCells - 1), route.Count - 1);
        return (first, end);
    }

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

    private static CombatState AttackWithTowers(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        foreach (var tower in state.Towers.OrderBy(tower => tower.EntityId.Value).ToArray())
        {
            if (tick.CompareTo(tower.NextAttackTick) < 0 || tower.HasShellInFlight)
            {
                continue;
            }

            var towerDefinition = content.GetTower(tower.TowerId);
            var availableTargets = next.Creeps
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
                    .Where(creep => CanLeadTarget(next, content, routes, tower, creep))
                    .ToArray();
            }

            var target = SelectTarget(tower, towerDefinition, availableTargets);

            if (target is null)
            {
                continue;
            }

            var targetCell = ResolvePosition(target, routes);

            if (IsFoundryTower(tower.TowerId))
            {
                // Indirect fire: no damage now. The target was already filtered to one this tower can
                // lead, so LeadPathIndex cannot be null here.
                var impactCell = routes[tower.LaneId][LeadPathIndex(next, content, routes, tower, target)!.Value];
                var impactTick = new SimulationTick(tick.Value + FoundryShellFlightTicks);

                events.Add(new TowerFiredEvent(tick, tower.LaneId, tower.EntityId, tower.Position, target.EntityId, targetCell, impactTick, impactCell));
                next = next.ReplaceTower(tower
                    .WithShellInFlight(impactTick, impactCell)
                    .WithNextAttackTick(new SimulationTick(tick.Value + EffectiveCooldown(next, tower, towerDefinition.AttackCooldownTicks))));
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
            var baseDamage = BaseDamageFor(towerDefinition);

            var shotDamage = baseDamage;
            if (IsSaplingTower(tower.TowerId))
            {
                shotDamage += GrovebondBonus(next, tower, baseDamage);
            }
            else if (IsSporeTower(tower.TowerId))
            {
                shotDamage = RotDamage(content, target.CreepId, baseDamage);
            }
            else if (IsBloomheartTower(tower.TowerId))
            {
                shotDamage += CrowdBloomBonus(next, routes, tower, target, baseDamage);
            }

            next = DamageCreep(next, content, tower, target, shotDamage, tick, events);

            if (IsTeslaTower(tower.TowerId))
            {
                next = ChainArc(next, content, routes, tower, target, baseDamage, tick, events);
            }

            if (IsPulseTower(tower.TowerId))
            {
                var splashDamage = Math.Max(1, baseDamage / 2);
                var targetPosition = ResolvePosition(target, routes);
                var splashTargets = next.Creeps
                    .Where(creep => !creep.EntityId.Equals(target.EntityId))
                    .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                    .Where(creep => IsInRange(targetPosition, ResolvePosition(creep, routes), 1))
                    .OrderByDescending(creep => creep.PathIndex)
                    .ThenBy(creep => creep.EntityId.Value)
                    .Take(2)
                    .ToArray();

                foreach (var splashTarget in splashTargets)
                {
                    next = DamageCreep(next, content, tower, splashTarget, splashDamage, tick, events);
                }
            }

            next = next.ReplaceTower(tower.WithNextAttackTick(
                new SimulationTick(tick.Value + EffectiveCooldown(next, tower, towerDefinition.AttackCooldownTicks))));
        }

        return next;
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
    private static bool IsServicedByDrone(CombatState state, TowerCombatState tower)
    {
        foreach (var other in state.Towers)
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
    private static int EffectiveCooldown(CombatState state, TowerCombatState tower, int authoredCooldown) =>
        IsServicedByDrone(state, tower) ? Math.Max(1, authoredCooldown - 1) : authoredCooldown;

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
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        TowerCombatState tower,
        CreepCombatState target)
    {
        var route = routes[tower.LaneId];
        var creepDefinition = content.GetCreep(target.CreepId);
        var brambleZones = BuildBrambleZones(state, content, routes);
        var index = target.PathIndex;
        var movement = target.MovementProgress;

        for (var step = 0; step < FoundryShellFlightTicks; step++)
        {
            var braked = IsUnderBramble(brambleZones, target.LaneId, index);
            (index, movement) = StepCreep(index, movement, creepDefinition.SpeedPerSecond, route.Count, braked);
        }

        // route.Count - 1 is the leak index: a creep arriving there is marked leaked and filtered out
        // of the impact check, so it can never be hit.
        return index < route.Count - 1 ? index : null;
    }

    private static bool CanLeadTarget(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        TowerCombatState tower,
        CreepCombatState target) =>
        LeadPathIndex(state, content, routes, tower, target) is not null;

    /// <summary>
    /// Lands any Foundry shell whose impact tick has arrived, damaging every live creep standing on
    /// the impact cell.
    /// </summary>
    /// <remarks>
    /// Disarms before damaging, so nothing can double-resolve. Re-reads next.Creeps per shell rather
    /// than taking one snapshot up front, because DamageCreep removes the dead mid-iteration and
    /// CombatState.Replace throws on a missing entity.
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
    private static CombatState ChainArc(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        TowerCombatState tower,
        CreepCombatState primary,
        int baseDamage,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        var damage = baseDamage;
        var fromIndex = primary.PathIndex;
        var struck = new List<long> { primary.EntityId.Value };

        for (var hop = 0; hop < ChainArcMaxHops; hop++)
        {
            damage = Math.Max(1, damage / 2);
            var fromPosition = routes[tower.LaneId][Math.Min(fromIndex, routes[tower.LaneId].Count - 1)];

            var link = next.Creeps
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
            next = DamageCreep(next, content, tower, link, damage, tick, events);
        }

        return next;
    }

    private static CombatState ResolveLandedShells(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        foreach (var tower in state.Towers.OrderBy(tower => tower.EntityId.Value).ToArray())
        {
            if (tower.ShellImpactTick is not { } impactTick || tick.CompareTo(impactTick) < 0)
            {
                continue;
            }

            var impactCell = tower.ShellImpactCell!.Value;
            next = next.ReplaceTower(tower.WithoutShellInFlight());

            var towerDefinition = content.GetTower(tower.TowerId);
            var hit = next.Creeps
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
                next = DamageCreep(next, content, tower, creep, BaseDamageFor(towerDefinition), tick, events);
            }
        }

        return next;
    }

    private static CreepCombatState? SelectTarget(
        TowerCombatState tower,
        TowerDefinition towerDefinition,
        IReadOnlyList<CreepCombatState> targets)
    {
        if (IsElderCanopyTower(tower.TowerId))
        {
            // Targets the creep FURTHEST BACK in range, not the leader. With the roster's longest
            // reach (5) that means it engages arrivals at the mouth of the lane, softening a wave
            // before it reaches everything else — area denial rather than last-ditch defence. Nothing
            // else on the roster targets back-most, so the tell is that it visibly shoots the far
            // creep while its neighbours all shoot the near one.
            return targets
                .OrderBy(creep => creep.PathIndex)
                .ThenBy(creep => creep.EntityId.Value)
                .FirstOrDefault();
        }

        if (IsPrismTower(tower.TowerId))
        {
            return targets
                .OrderByDescending(creep => IsShadeCreep(creep.CreepId))
                .ThenByDescending(creep => creep.Health)
                .ThenByDescending(creep => creep.PathIndex)
                .ThenBy(creep => creep.EntityId.Value)
                .FirstOrDefault();
        }

        return targets
            .OrderByDescending(creep => creep.PathIndex)
            .ThenBy(creep => creep.EntityId.Value)
            .FirstOrDefault();
    }

    private static CombatState DamageCreep(
        CombatState state,
        CombatContent content,
        TowerCombatState tower,
        CreepCombatState target,
        int baseDamage,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var damage = AdjustDamageForRoles(tower.TowerId, target.CreepId, baseDamage);
        var damaged = target.WithHealth(Math.Max(0, target.Health - damage));
        var next = state.ReplaceCreep(damaged);
        events.Add(new CreepDamagedEvent(tick, tower.OwnerId, tower.LaneId, tower.EntityId, tower.Position, damaged.EntityId, damage));

        if (damaged.IsDead)
        {
            events.Add(new CreepKilledEvent(tick, damaged.EntityId, tower.OwnerId, content.GetCreep(damaged.CreepId).KillBounty));
            next = next.RemoveCreep(damaged.EntityId);
        }

        return next;
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
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes)
    {
        var route = routes[creep.LaneId];
        return route[Math.Min(creep.PathIndex, route.Count - 1)];
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
    private static int GrovebondBonus(CombatState state, TowerCombatState sapling, int baseDamage)
    {
        var adjacent = 0;
        foreach (var other in state.Towers)
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
        CombatState state,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        TowerCombatState tower,
        CreepCombatState target,
        int baseDamage)
    {
        var targetCell = ResolvePosition(target, routes);
        var crowd = 0;
        foreach (var creep in state.Creeps)
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

    private static bool IsThornTower(ContentId towerId) => ContainsRole(towerId, "thorn");

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
    /// The single place a tower-line tier multiplier belongs (docs/CATEGORY_UPGRADE_TIERS_PLAN.md). Every
    /// path that deals damage on a tower's behalf reads this — the primary shot, Pulse's splash, Chain
    /// Arc's hops and the Foundry's shell — so scaling here scales the whole tower rather than only the
    /// shot the player happens to be looking at.
    ///
    /// It is a pass-through today. That is intentional: the seam exists so the multiplier is a one-line
    /// change against a single function instead of a hunt through four call sites, three of which were
    /// found only by grepping for the raw field after the first two were fixed.
    /// </remarks>
    private static int BaseDamageFor(TowerDefinition towerDefinition) => towerDefinition.Damage;

    private static bool IsPulseTower(ContentId towerId) => ContainsRole(towerId, "pulse");

    private static bool IsPrismTower(ContentId towerId) => ContainsRole(towerId, "prism");

    private static bool IsControlTower(ContentId towerId) => ContainsRole(towerId, "control");

    private static bool IsShadeCreep(ContentId creepId) => ContainsRole(creepId, "shade") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "invisible");

    private static Lives LeakLifeLossFor(ContentId creepId) => ContainsRole(creepId, "siege") ? new Lives(2) : new Lives(1);

    private static bool ContainsRole(ContentId contentId, string role) => contentId.Value.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0;
}
