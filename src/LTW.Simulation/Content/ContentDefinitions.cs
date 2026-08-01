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
        bool ignoresSendCooldown = false,
        int movementCost = DefaultMovementCost,
        bool ignoresMaze = false,
        CreepSupportRole support = CreepSupportRole.None)
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
        MovementCost = movementCost > 0
            ? movementCost
            : throw new ArgumentOutOfRangeException(nameof(movementCost), "Movement cost must be positive.");
        IgnoresMaze = ignoresMaze;
        Support = support;
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

    /// <summary>Movement a creep must bank to advance one cell. Higher is slower.</summary>
    /// <remarks>
    /// The support creeps need to travel SLOWER than the pack they follow, so they trail behind it
    /// and are shielded from leader-first targeting — and <see cref="SpeedPerSecond"/> cannot express
    /// that. It is a whole number whose floor is 1, and Brute and Siege, the heavies a support would
    /// follow, are already at 1. There is no value below them.
    ///
    /// Cost is the other half of the same fraction (<c>SpeedPerSecond / MovementCost</c> cells per
    /// tick), so raising it gives the sub-unit pace speed alone cannot. At 4 against the default 3 a
    /// support drifts about one cell behind every twelve ticks — roughly four cells over a lane,
    /// which is what makes the aura a window rather than a permanent state.
    ///
    /// Same mechanism Thorn Snare already uses: its brake doubles this number for cells under
    /// bramble. This just lets a creep carry its own value instead of only the terrain having one.
    /// </remarks>
    public int MovementCost { get; }

    /// <summary>When true this creep walks the direct route, straight over towers and any maze.</summary>
    /// <remarks>
    /// A maze exists to make the walk long, so ignoring it is the single most disruptive thing a
    /// creep can do — which is why the one creep that does it is costed to die to almost anything.
    /// It survives only when the defence is saturated: a tower fires at one target per tick and then
    /// sits on cooldown, so a tower busy with the pack cannot shoot this. It is protected by the
    /// company it keeps, not by its own stats.
    ///
    /// Declared on the definition rather than matched against an id in CombatService, for the same
    /// reason as <see cref="IgnoresSendCooldown"/>: the behaviour travels with the content.
    /// </remarks>
    public bool IgnoresMaze { get; }

    /// <summary>What this creep does for the creeps around it. See <see cref="CreepSupportRole"/>.</summary>
    public CreepSupportRole Support { get; }

    /// <summary>
    /// Movement a creep banks per cell when nothing modifies it.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>CombatService.BaseMovementCost</c>, which cannot be referenced here without
    /// pointing Content at Combat. The two are asserted equal by test rather than left to drift.
    /// </remarks>
    public const int DefaultMovementCost = 3;
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

/// <summary>
/// What a support creep does for the creeps around it, or to the towers shooting at them.
/// </summary>
/// <remarks>
/// Declared on the creep rather than matched against a list of ids in CombatService, following the
/// reasoning already written on <see cref="CreepDefinition.IgnoresSendCooldown"/>: the behaviour
/// travels with the content, so adding a support creep is a content edit rather than a combat edit.
///
/// This is the identity of the SUPPORT category. Before it, the three categories differed only in
/// their stat ranges, and those ranges overlapped almost exactly — measured across the roster, cat 0
/// spanned cost 6-40 and health 5-48, cat 1 spanned 5-38 and 4-48. Category 1's one mechanical
/// trait, IgnoresSendCooldown, had also been inert since the send cooldown was set to zero. Players
/// were choosing between reskins.
/// </remarks>
public enum CreepSupportRole
{
    /// <summary>An ordinary creep. Walks, absorbs, leaks.</summary>
    None = 0,

    /// <summary>Speeds up nearby friendly creeps.</summary>
    Pacesetter,

    /// <summary>Heals nearby friendly creeps a little at a time.</summary>
    Mender,

    /// <summary>Reduces damage taken by nearby friendly creeps.</summary>
    Bulwark,

    /// <summary>Slows the fire rate of nearby enemy towers.</summary>
    Binder
}
