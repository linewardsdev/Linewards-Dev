using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Content;

public sealed class ContentCatalog
{
    public ContentCatalog(
        string version,
        IReadOnlyList<TowerDefinition> towers,
        IReadOnlyList<CreepDefinition> creeps,
        IReadOnlyList<TechDefinition> techs,
        IReadOnlyList<MapDefinition> maps,
        IReadOnlyList<BotProfileDefinition> botProfiles)
    {
        Version = string.IsNullOrWhiteSpace(version)
            ? throw new ArgumentException("Content version is required.", nameof(version))
            : version;
        Towers = CopyRequired(towers, nameof(towers));
        Creeps = CopyRequired(creeps, nameof(creeps));
        Techs = CopyRequired(techs, nameof(techs));
        Maps = CopyRequired(maps, nameof(maps));
        BotProfiles = CopyRequired(botProfiles, nameof(botProfiles));
    }

    public string Version { get; }

    public IReadOnlyList<TowerDefinition> Towers { get; }

    public IReadOnlyList<CreepDefinition> Creeps { get; }

    public IReadOnlyList<TechDefinition> Techs { get; }

    public IReadOnlyList<MapDefinition> Maps { get; }

    public IReadOnlyList<BotProfileDefinition> BotProfiles { get; }

    private static IReadOnlyList<T> CopyRequired<T>(IEnumerable<T>? values, string parameterName) =>
        values?.ToArray() ?? throw new ArgumentNullException(parameterName);
}

public sealed class TowerDefinition
{
    public TowerDefinition(ContentId id, string name, Gold cost, int rangeCells, int damage, int attackCooldownTicks)
    {
        Id = id;
        Name = RequiredName(name, nameof(name));
        Cost = cost;
        RangeCells = rangeCells;
        Damage = damage;
        AttackCooldownTicks = attackCooldownTicks;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public int RangeCells { get; }

    public int Damage { get; }

    public int AttackCooldownTicks { get; }

    private static string RequiredName(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Name is required.", parameterName)
            : value;
}

public sealed class CreepDefinition
{
    public CreepDefinition(ContentId id, string name, Gold cost, Income incomeGain, Gold killBounty, Gold leakBounty, int maxHealth, int speedPerSecond)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Cost = cost;
        IncomeGain = incomeGain;
        KillBounty = killBounty;
        LeakBounty = leakBounty;
        MaxHealth = maxHealth;
        SpeedPerSecond = speedPerSecond;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public Income IncomeGain { get; }

    public Gold KillBounty { get; }

    public Gold LeakBounty { get; }

    public int MaxHealth { get; }

    public int SpeedPerSecond { get; }
}

public sealed class TechDefinition
{
    public TechDefinition(ContentId id, string name, Gold cost, IReadOnlyList<ContentId> unlocksTowerIds, IReadOnlyList<ContentId> unlocksCreepIds)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Cost = cost;
        UnlocksTowerIds = CopyRequired(unlocksTowerIds, nameof(unlocksTowerIds));
        UnlocksCreepIds = CopyRequired(unlocksCreepIds, nameof(unlocksCreepIds));
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public IReadOnlyList<ContentId> UnlocksTowerIds { get; }

    public IReadOnlyList<ContentId> UnlocksCreepIds { get; }

    private static IReadOnlyList<T> CopyRequired<T>(IEnumerable<T>? values, string parameterName) =>
        values?.ToArray() ?? throw new ArgumentNullException(parameterName);
}

public sealed class MapDefinition
{
    public MapDefinition(ContentId id, string name, int width, int height, GridPosition spawn, GridPosition exit, IReadOnlyList<GridPosition> blockedCells)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Width = width;
        Height = height;
        Spawn = spawn;
        Exit = exit;
        BlockedCells = CopyRequired(blockedCells, nameof(blockedCells));
    }

    public ContentId Id { get; }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public GridPosition Spawn { get; }

    public GridPosition Exit { get; }

    public IReadOnlyList<GridPosition> BlockedCells { get; }

    private static IReadOnlyList<T> CopyRequired<T>(IEnumerable<T>? values, string parameterName) =>
        values?.ToArray() ?? throw new ArgumentNullException(parameterName);
}

public sealed class BotProfileDefinition
{
    public BotProfileDefinition(ContentId id, string name, int aggression, int defenseBias, int minimumGoldReserve)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Aggression = aggression;
        DefenseBias = defenseBias;
        MinimumGoldReserve = minimumGoldReserve;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public int Aggression { get; }

    public int DefenseBias { get; }

    public int MinimumGoldReserve { get; }
}
