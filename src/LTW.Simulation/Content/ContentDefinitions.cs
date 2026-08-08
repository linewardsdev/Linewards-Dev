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
    public TowerDefinition(ContentId id, string name, Gold cost, int rangeCells, int damage, int attackCooldownTicks, int categoryIndex, int signalGoldPerHit = 0, bool slowsCreeps = false, TowerRole role = TowerRole.Dps)
    {
        Id = id;
        Name = RequiredName(name, nameof(name));
        Cost = cost;
        RangeCells = rangeCells;
        Damage = damage;
        AttackCooldownTicks = attackCooldownTicks;
        CategoryIndex = categoryIndex;
        SignalGoldPerHit = signalGoldPerHit;
        SlowsCreeps = slowsCreeps;
        Role = role;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public Gold Cost { get; }

    public int RangeCells { get; }

    public int Damage { get; }

    public int AttackCooldownTicks { get; }

    /// <summary>
    /// Gold paid to this tower's owner every time it damages a creep. 0 for all but the Relay Ward.
    /// </summary>
    /// <remarks>
    /// Optional with a default of 0, unlike <see cref="CategoryIndex"/> which is deliberately
    /// required. The difference is that "no line" is not a meaningful answer for a tower while "no
    /// income" plainly is, so a default here cannot hide a mistake the way a defaulted line could.
    ///
    /// It is content rather than code because it used to be code: the bridge decided which tower
    /// earned by testing whether its content id CONTAINED "relay", "utility" or "economy" — three
    /// substrings for one tower. Nothing was wrong with the behaviour, but any future
    /// tower.economy_hub, or anything with "utility" in its name, would have started minting gold on
    /// every hit without a line of code being written. Authoring the number removes the guess.
    /// </remarks>
    public int SignalGoldPerHit { get; }

    /// <summary>Whether this tower brakes creeps walking the mazed route through its range.</summary>
    /// <remarks>
    /// Authored rather than inferred from the tower's name, for the same reason
    /// <see cref="SignalGoldPerHit"/> is: the mechanic used to be a substring match on "thorn" in
    /// CombatService, so only one tower in the game could ever have it and giving another line a
    /// brake meant naming a tower after a plant.
    ///
    /// That mattered more than tidiness. Bramble Hold was the ONLY slow on the roster and it lives
    /// in Grove, so a player locked to Arcane or Foundry had no answer to speed at all — which is
    /// what made a forced category pick unshippable until now.
    /// </remarks>
    public bool SlowsCreeps { get; }

    /// <summary>What job this tower is for. See <see cref="TowerRole"/>.</summary>
    public TowerRole Role { get; }

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
    public BotProfileDefinition(
        ContentId id,
        string name,
        int aggression,
        int defenseBias,
        int minimumGoldReserve,
        IReadOnlyList<TowerRole>? buildOrder = null,
        int minimumTowerCoverage = 0)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Aggression = aggression;
        DefenseBias = defenseBias;
        MinimumGoldReserve = minimumGoldReserve;
        BuildOrder = buildOrder?.ToArray() ?? Array.Empty<TowerRole>();
        MinimumTowerCoverage = minimumTowerCoverage;
    }

    public ContentId Id { get; }

    public string Name { get; }

    public int Aggression { get; }

    public int DefenseBias { get; }

    public int MinimumGoldReserve { get; }

    /// <summary>
    /// The towers this profile builds, cycled by how many it already owns.
    /// </summary>
    /// <remarks>
    /// Authored here rather than compiled into the bot, which is what this used to be: three
    /// <c>ContentId[]</c> arrays inside <c>LocalVerticalSlice</c> naming
    /// <c>SampleVerticalSliceContent</c> constants directly, so the bot's build-out was a property
    /// of the simulation assembly rather than of the catalog it was playing with (OPEN_ITEMS.md
    /// item 26). A catalog with a different roster could not give its bots a build order without a
    /// code change, which is the same objection <see cref="TowerDefinition.SignalGoldPerHit"/>
    /// records for its own number.
    ///
    /// It is a cycle, not a plan: <c>BuildOrder[ownedTowerCount % BuildOrder.Count]</c>. An earlier
    /// shape switched on the first few slots and then repeated one tower forever, which meant two
    /// of the three profiles could only ever reach 5 of the 15 towers. Every entry is therefore
    /// reachable, and a profile's list is a flavour rather than an exhaustive roster.
    ///
    /// Optional, and empty means "this profile does not build" rather than a fallback roster. A bot
    /// with no authored build order is a content decision that should be visible, not one the
    /// simulation quietly invents a tower list for; <see cref="ContentValidator"/> checks every id
    /// named here exists in the same catalog's towers.
    /// </remarks>
    public IReadOnlyList<TowerRole> BuildOrder { get; }

    /// <summary>
    /// Towers this profile finishes before it is allowed to send at all. A floor, never a ceiling.
    /// </summary>
    /// <remarks>
    /// Sits beside <see cref="MinimumGoldReserve"/> because it is the same kind of number — a
    /// per-profile spending discipline — and it was the last one still expressed as a switch on the
    /// profile enum. 0 means "send from the first tick", which is the aggressive-economy opening;
    /// the bot keeps building well past this number as long as gold above its reserve floor and a
    /// legal placement remain.
    /// </remarks>
    public int MinimumTowerCoverage { get; }
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
