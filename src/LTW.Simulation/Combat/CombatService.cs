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

    private static CombatState MoveCreeps(
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        SimulationTick tick,
        List<ISimulationEvent> events)
    {
        var next = state;
        foreach (var creep in state.Creeps.Where(creep => !creep.IsDead && !creep.HasLeaked).ToArray())
        {
            var definition = content.GetCreep(creep.CreepId);
            var route = routes[creep.LaneId];
            var movement = creep.MovementProgress + definition.SpeedPerSecond;
            var pathIndex = creep.PathIndex;

            while (movement >= 1 && pathIndex < route.Count - 1)
            {
                movement--;
                pathIndex++;
            }

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
            if (tick.CompareTo(tower.NextAttackTick) < 0)
            {
                continue;
            }

            var towerDefinition = content.GetTower(tower.TowerId);
            var availableTargets = next.Creeps
                .Where(creep => !creep.IsDead && !creep.HasLeaked && creep.LaneId.Equals(tower.LaneId))
                .Where(creep => IsInRange(tower.Position, ResolvePosition(creep, routes), towerDefinition.RangeCells))
                .ToArray();
            var target = SelectTarget(tower, availableTargets);

            if (target is null)
            {
                continue;
            }

            next = DamageCreep(next, content, tower, target, towerDefinition.Damage, tick, events);
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

    private static CreepCombatState? SelectTarget(TowerCombatState tower, IReadOnlyList<CreepCombatState> targets)
    {
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

    private static bool IsPulseTower(ContentId towerId) => ContainsRole(towerId, "pulse");

    private static bool IsPrismTower(ContentId towerId) => ContainsRole(towerId, "prism");

    private static bool IsControlTower(ContentId towerId) => ContainsRole(towerId, "control");

    private static bool IsShadeCreep(ContentId creepId) => ContainsRole(creepId, "shade") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "invisible");

    private static Lives LeakLifeLossFor(ContentId creepId) => ContainsRole(creepId, "siege") ? new Lives(2) : new Lives(1);

    private static bool ContainsRole(ContentId contentId, string role) => contentId.Value.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0;
}
