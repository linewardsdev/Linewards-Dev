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
                creep.Health))
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
            var visionRangeCells = EffectiveRange(state, tower, towerDefinition.RangeCells) + VisionBufferCells;
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

        // Exactly BrambleZoneCells wide, not "at least". This used to widen to whichever was LARGER
        // of the covered span and the minimum, so a range-2 thorn braked all 5 route cells it could
        // see. Slowing everything that crosses is a force multiplier for every other tower, and at 5
        // cells wide it was strong enough to be an automatic purchase in any build — which is exactly
        // what "no mandatory buys" rules out. Three cells still guarantees a speed-3 creep cannot step
        // clean over the zone, which is the constraint the width exists to satisfy.
        var end = Math.Min(first + BrambleZoneCells - 1, route.Count - 1);
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
                .Where(creep => CanEngage(tower, ResolvePosition(creep, routes), EffectiveRange(next, tower, towerDefinition.RangeCells)))
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
                    .WithNextAttackTick(new SimulationTick(tick.Value + towerDefinition.AttackCooldownTicks)));
                continue;
            }

            events.Add(new TowerFiredEvent(tick, tower.LaneId, tower.EntityId, tower.Position, target.EntityId, targetCell, tick, targetCell));

            var shotDamage = towerDefinition.Damage;
            if (IsSaplingTower(tower.TowerId))
            {
                shotDamage += GrovebondBonus(next, tower);
            }
            else if (IsSporeTower(tower.TowerId))
            {
                shotDamage = RotDamage(content, target.CreepId, towerDefinition.Damage);
            }

            next = DamageCreep(next, content, tower, target, shotDamage, tick, events);

            if (IsTeslaTower(tower.TowerId))
            {
                next = ChainArc(next, content, routes, tower, target, shotDamage, tick, events);
            }

            if (IsPulseTower(tower.TowerId))
            {
                var splashDamage = Math.Max(1, towerDefinition.Damage / 2);
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

            next = next.ReplaceTower(tower.WithNextAttackTick(new SimulationTick(tick.Value + towerDefinition.AttackCooldownTicks)));
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
    private const int RepairDroneRangeBonus = 1;

    /// <summary>
    /// A tower's range including any bonus from an adjacent Repair Drone Spire.
    /// </summary>
    /// <remarks>
    /// The Repair Drone Spire has nothing to repair — towers never take damage — so its support role
    /// is expressed as reach instead: every orthogonally adjacent tower of the same owner and lane
    /// gets +1 range. This is the only mechanic in the game that modifies another tower's range, and
    /// it interacts well with the Barricade, whose whole limitation is a shallow forward arc.
    ///
    /// Bonuses do not stack: two drones beside one tower still give +1. Otherwise a drone sandwich
    /// would be a strictly better Prism for less gold.
    /// </remarks>
    private static int EffectiveRange(CombatState state, TowerCombatState tower, int authoredRange)
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
                return authoredRange + RepairDroneRangeBonus;
            }
        }

        return authoredRange;
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
    /// Restricting hops to one direction is what keeps the chain order a property of the board rather
    /// than of iteration order.
    /// </remarks>
    private static CombatState ChainArc(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        TowerCombatState tower,
        CreepCombatState primary,
        int primaryDamage,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        var damage = primaryDamage;
        var fromIndex = primary.PathIndex;
        var struck = new List<long> { primary.EntityId.Value };

        for (var hop = 0; hop < ChainArcMaxHops; hop++)
        {
            damage = Math.Max(1, damage / 2);
            var fromPosition = routes[tower.LaneId][Math.Min(fromIndex, routes[tower.LaneId].Count - 1)];

            var link = next.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => !struck.Contains(creep.EntityId.Value))
                .Where(creep => creep.PathIndex < fromIndex)
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
                next = DamageCreep(next, content, tower, creep, towerDefinition.Damage, tick, events);
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

        if (IsBloomheartTower(tower.TowerId))
        {
            // Finish the weakest, else lead. The lethality test goes through AdjustDamageForRoles
            // rather than raw damage: a 3hp Shade takes halved damage from this tower, so without
            // the adjustment it would enter the "lethal" partition, outrank a genuinely killable
            // creep on PathIndex, and eat the totem's one shot per pass without dying — the
            // mechanic visibly failing at the moment it should read as working.
            return targets
                .OrderByDescending(creep => creep.Health <= AdjustDamageForRoles(tower.TowerId, creep.CreepId, towerDefinition.Damage))
                .ThenBy(creep => creep.Health)
                .ThenByDescending(creep => creep.PathIndex)
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

    private const int GrovebondMaxBonus = 3;

    /// <summary>
    /// Extra damage a Sapling Sentinel gets from orthogonally adjacent Grove towers of the same
    /// owner and lane, capped at <see cref="GrovebondMaxBonus"/>.
    /// </summary>
    /// <remarks>
    /// The Sapling is the cheapest tower on the roster (10 gold) and deliberately weak alone. This
    /// is what makes a cluster of them worth more than the sum of its parts, and it is
    /// self-limiting in a way that resists a runaway: in a solid block the highest-bonus towers are
    /// the interior ones, and interior towers see no route cells, so they never fire.
    /// </remarks>
    private static int GrovebondBonus(CombatState state, TowerCombatState sapling)
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

        return Math.Min(GrovebondMaxBonus, adjacent);
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

    private static bool IsThornTower(ContentId towerId) => ContainsRole(towerId, "thorn");

    private const int SporeRotDivisor = 6;

    /// <summary>
    /// Spore Cloud Bloom's damage: a fraction of the target's AUTHORED max health, floored at the
    /// tower's own damage.
    /// </summary>
    /// <remarks>
    /// This makes it irrelevant against chaff and the roster's hardest counter to anything fat.
    /// MaxHealth is authored content and never mutated, so the value is constant per creep TYPE and
    /// cannot be gamed by chipping the creep down first.
    ///
    /// Integer floor division, matching the rounding-down convention Pulse's splash already uses.
    /// Be honest about the shape: rot only exceeds the floor above 30 max health, so it is a STEP at
    /// the 32hp line rather than a smooth curve, and it is inert on 8 of the 15 creeps.
    /// </remarks>
    private static int RotDamage(CombatContent content, ContentId creepId, int authoredDamage) =>
        Math.Max(authoredDamage, content.GetCreep(creepId).MaxHealth / SporeRotDivisor);

    private static bool IsSporeTower(ContentId towerId) => ContainsRole(towerId, "spore");

    private static bool IsPulseTower(ContentId towerId) => ContainsRole(towerId, "pulse");

    private static bool IsPrismTower(ContentId towerId) => ContainsRole(towerId, "prism");

    private static bool IsControlTower(ContentId towerId) => ContainsRole(towerId, "control");

    private static bool IsShadeCreep(ContentId creepId) => ContainsRole(creepId, "shade") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "invisible");

    private static Lives LeakLifeLossFor(ContentId creepId) => ContainsRole(creepId, "siege") ? new Lives(2) : new Lives(1);

    private static bool ContainsRole(ContentId contentId, string role) => contentId.Value.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0;
}
