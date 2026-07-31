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
    public TowerDefinition(ContentId id, string name, Gold cost, int rangeCells, int damage, int attackCooldownTicks, int categoryIndex)
    {
        Id = id;
        Name = RequiredName(name, nameof(name));
        Cost = cost;
        RangeCells = rangeCells;
        Damage = damage;
        AttackCooldownTicks = attackCooldownTicks;
        CategoryIndex = categoryIndex;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public int RangeCells { get; }

    public int Damage { get; }

    public int AttackCooldownTicks { get; }

    /// <summary>
    /// Which tower LINE this belongs to: 0 ARCANE, 1 FOUNDRY, 2 GROVE. Selects the upgrade tier
    /// that scales this tower's damage.
    /// </summary>
    /// <remarks>
    /// Deliberately a required constructor argument with no default. The grouping is also stated
    /// on the Unity side in TowerCatalog.Entries, which drives the build menu, and the two must
    /// agree — a default here would let a new tower silently land in ARCANE and be scaled by a
    /// tier its menu card never offered. Making it required means the compiler asks.
    ///
    /// An int rather than an enum because the simulation has no opinion about what the lines are
    /// called; the labels live in TowerCatalog.CategoryLabels where the UI can read them.
    /// </remarks>
    public int CategoryIndex { get; }

    private static string RequiredName(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Name is required.", parameterName)
            : value;
}

public sealed class CreepDefinition
{
    public CreepDefinition(
        ContentId id,
        string name,
        Gold cost,
        Income incomeGain,
        Gold killBounty,
        Gold leakBounty,
        int maxHealth,
        int speedPerSecond,
        int categoryIndex,
        bool ignoresSendCooldown = false)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Cost = cost;
        IncomeGain = incomeGain;
        KillBounty = killBounty;
        LeakBounty = leakBounty;
        MaxHealth = maxHealth;
        SpeedPerSecond = speedPerSecond;
        CategoryIndex = categoryIndex;
        IgnoresSendCooldown = ignoresSendCooldown;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public Income IncomeGain { get; }

    public Gold KillBounty { get; }

    public Gold LeakBounty { get; }

    public int MaxHealth { get; }

    public int SpeedPerSecond { get; }

    /// <summary>
    /// Which send CATEGORY this belongs to: 0 CORE, 1 RAPID, 2 ELITE. Selects the upgrade tier
    /// that scales this creep's health at spawn.
    /// </summary>
    /// <remarks>
    /// Required for the same reason as <see cref="TowerDefinition.CategoryIndex"/>: the grouping is
    /// also stated on the Unity side, in SendDockController's per-category draw methods, and a
    /// default here would let a new creep be scaled by a tier whose card never listed it.
    /// </remarks>
    public int CategoryIndex { get; }

    /// <summary>
    /// When true this creep may be sent whenever the sender can afford it, and sending it neither
    /// waits on nor starts the global send cooldown.
    /// </summary>
    /// <remarks>
    /// Carried on the definition rather than checked against a list of ids in EconomyService, so
    /// the exemption travels with the content and a new creep declares its own behaviour.
    /// Sending an exempt creep deliberately does not arm the cooldown either — otherwise it would
    /// still gate the next non-exempt send, which is the opposite of being exempt.
    /// </remarks>
    public bool IgnoresSendCooldown { get; }
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
