using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.Simulation.Random;

namespace LTW.Simulation.Bots;

/// <summary>
/// One seat's whole opponent AI: what it sends, what it builds, and what it upgrades.
/// </summary>
/// <remarks>
/// All four of those used to be split. The send decision lived here; where to build, what to build,
/// which tier to buy and which tower to raise lived in <c>LocalVerticalSlice</c>, which is the match
/// bridge — about a third of that class was bot logic, and its build orders named
/// <c>SampleVerticalSliceContent</c> constants directly, so bot behaviour was compiled against
/// sample content inside the simulation assembly (OPEN_ITEMS.md item 26). The decisions are all here
/// now, reaching the board through <see cref="IBotMatchContext"/>, and the build orders are authored
/// on <see cref="BotProfileDefinition"/> like every other piece of bot tuning already was.
///
/// Nothing about the decisions themselves changed in that move, and that is a property worth
/// keeping: all-bot matches in this project are fully deterministic — there is no RNG anywhere in
/// here — so a single changed decision moves match outcomes, and several tests pin exact ones.
/// </remarks>
public sealed class BotController
{
    private readonly BotDecisionProfile profile;
    private readonly ContentId creepId;

    /// <summary>
    /// This bot's own source of variation, seeded from the match seed and its seat.
    /// </summary>
    /// <remarks>
    /// Until this existed the match seed did nothing at all — it was read once, in
    /// <c>GetReplayRecord</c>, written into replay metadata and never used, and
    /// <see cref="SeededRandomSource"/> was referenced only by a test asserting it reproduced
    /// itself. Five different seeds produced byte-identical matches, so every balance number this
    /// project has ever recorded as "measured on seed 1" was one trajectory rather than a sample.
    ///
    /// Per seat rather than one shared source, so a bot's rolls do not depend on how many other
    /// bots took a turn first. A shared source would still be deterministic, but adding or removing
    /// a seat would shift every later bot's stream and make two configurations incomparable for
    /// reasons that have nothing to do with what changed.
    ///
    /// Determinism is preserved and still matters: the same seed and seat produce the same stream,
    /// which is what keeps replays and <c>ScenarioReplayTests</c> honest.
    /// </remarks>
    private readonly IRandomSource random;

    public BotController(BotDecisionProfile profile, ContentId creepId)
        : this(profile, creepId, new SeededRandomSource(0))
    {
    }

    public BotController(BotDecisionProfile profile, ContentId creepId, IRandomSource random)
    {
        this.profile = profile;
        this.creepId = creepId;
        this.random = random;

        // The line this bot commits to, drawn once from its own seeded stream.
        //
        // This is the first thing in the simulation the match seed actually reaches, and it is a
        // decision worth spending it on: it is not pinned to an exact value by any test, and it is
        // not upstream of the build step's gold — the two properties that made send quantity the
        // wrong home for it twice (item 42).
        //
        // Chosen rather than discovered, because under the category lock a bot that simply built
        // and committed to whatever came first would commit to the same line every time: with
        // role-based orders every profile opens on the cheapest tower for its first role, which is
        // the same tower for all of them. Eight seats all locked to Arcane is not a table.
        preferredTowerLine = (int)(random.NextDouble() * PlayerEconomyState.CategoryCount);
        if (preferredTowerLine >= PlayerEconomyState.CategoryCount)
        {
            preferredTowerLine = PlayerEconomyState.CategoryCount - 1;
        }
    }

    /// <summary>The tower line this bot intends to commit to, before it has built anything.</summary>
    private readonly int preferredTowerLine;

    /// <summary>Which line this bot should build from right now.</summary>
    /// <remarks>
    /// Its committed line once it has one, and its intended line before that. Returning the
    /// preference pre-commitment is what makes the very first tower land in the line the bot meant
    /// to play, rather than committing it to whichever line happened to hold the cheapest opener.
    /// </remarks>
    private int BuildLineFor(PlayerEconomyState player) =>
        player.ChosenTowerLine == PlayerEconomyState.UnchosenTowerLine
            ? preferredTowerLine
            : player.ChosenTowerLine;

    public BotDecisionProfile Profile => profile;

    public ContentId PrimaryCreepId => creepId;

    /// <summary>
    /// How long after sending a wall this bot will send something that needs one in front of it.
    /// </summary>
    /// <remarks>
    /// Nine ticks, and the number comes from the geometry rather than from feel. Every creep spawns
    /// at path index 0, a wall covers a cell every three ticks (speed 1 against
    /// <c>BaseMovementCost</c> 3), and a support's aura reaches
    /// <c>SupportAuraField.PathRadius</c> = 3 cells. So a support sent nine ticks after its wall
    /// spawns exactly at the edge of its own aura, and anything later spawns outside it — paying
    /// for an escort that can never catch what it was bought to help.
    ///
    /// The window is generous in one direction only: sending the escort SOONER is always better,
    /// because the support is slower than the wall and the gap only widens from there.
    /// </remarks>
    public const int EscortFollowWindowTicks = 9;

    /// <summary>
    /// How long a bot may go without sending before it starts saving for one instead of building.
    /// </summary>
    /// <remarks>
    /// One income payout (EconomyRules pays every 50 ticks). A bot that has banked a whole payout and
    /// still sent nothing is not choosing to build, it is unable to reach a price — see
    /// <see cref="SendSavingsFor"/>.
    ///
    /// Swept against the two-Greedy-bot heavy scenario at 50 / 100 / 150, which trades sends against
    /// towers monotonically — by tick 900: 17 sends and 25 towers at 50, 11 and 35 at 100, 8 and 40 at
    /// 150. 50 recovers the most attacking without starving the build, and the bots it governs still
    /// build throughout. Raising it makes bots more passive, not more balanced.
    /// </remarks>
    public const int SendStarvationTicks = 50;

    /// <summary>Tick this bot last sent a wall, or null if it never has or has spent it.</summary>
    /// <remarks>
    /// The bot's whole notion of composition. It cannot see the lane — <see cref="Decide"/> receives
    /// an economy record and nothing else — so "is there a wall in front of this support" has to be
    /// answered from what it just bought rather than from what is on the board. That is a weaker
    /// signal than looking, but it is the right one here: a creep sent nine ticks ago IS still near
    /// the mouth of the lane, and no lane state can be consulted without widening the bot's
    /// interface to the whole simulation.
    ///
    /// Mutable per-bot state, which the replay determinism rules make worth stating: bots are
    /// rebuilt per match and decide in player-id order every tick, so the sequence of writes here is
    /// a pure function of the tick sequence.
    ///
    /// Nullable rather than a long.MinValue sentinel, and that is a bug fix rather than a style
    /// choice. With the sentinel, `tick.Value - lastWallSendTick` OVERFLOWS — at tick 300 it
    /// evaluates to -9223372036854775508, which is comfortably less than the nine-tick window, so
    /// the gate read as open at every tick a bot had never sent a wall. It silently did nothing,
    /// and the whole test suite passed with it in place.
    /// </remarks>
    private long? lastWallSendTick;

    /// <summary>Tick of this bot's most recent send, used to detect that it has stopped attacking.</summary>
    private long? lastSendTick;

    /// <summary>
    /// Everything this bot does in one tick, in the order it does it.
    /// </summary>
    /// <remarks>
    /// The order is not arbitrary and is the one thing here that should not be rearranged casually.
    ///
    /// Sending runs FIRST (reordered 2026-07-28 — see GD_TUNING_LOG.md). Building has no cap and
    /// used to run first, so it absorbed a bot's surplus gold into "one more tower" before a pricier
    /// preferred creep (creep.serpent at 27g, creep.obsidian_brute at 30g) ever became affordable —
    /// confirmed by replay analysis showing those creeps at 0 uses across whole playtests even
    /// though they were reachable on paper. Giving the send its claim on gold first, with towers
    /// spending only what is left, fixed the starvation without adding a new tunable cap.
    ///
    /// Tiers and tower upgrades run last for the same reason in reverse: they are bought from what
    /// is genuinely surplus, so a bot that is still building never stalls to save for one.
    /// </remarks>
    public void TakeTurn(PlayerId playerId, IBotMatchContext match)
    {
        TrySend(playerId, match);
        TryBuyCategoryTier(playerId, match);
        TryBuild(playerId, match);
        TryUpgradeTower(playerId, match);
    }

    /// <summary>
    /// The one-off opening a bot plays before the first tick of an expanded-lane match.
    /// </summary>
    /// <remarks>
    /// A tower and a single creep, so a large board is not eight seats staring at each other while
    /// the first income tick arrives.
    ///
    /// The send deliberately does NOT go through <see cref="Decide"/>: it is the configured primary
    /// creep at quantity 1, not a choice, and routing it through the decision would open the escort
    /// window (<see cref="EscortFollowWindowTicks"/>) before the bot has sent anything to escort.
    /// </remarks>
    public void TakeOpeningTurn(PlayerId playerId, IBotMatchContext match)
    {
        TryBuild(playerId, match);
        match.TrySend(playerId, creepId, quantity: 1);
        lastSendTick = match.Tick.Value;
    }

    public BotDecision Decide(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var creep = SelectCreep(player, content, tick);
        var sendQuantity = GetSendQuantity(player, content, creep);
        if (sendQuantity <= 0)
        {
            return BotDecision.None;
        }

        if (IsWall(creep))
        {
            lastWallSendTick = tick.Value;
        }
        else
        {
            // Spent. One escort per wall, so a bot cannot answer a single wall with a stream of
            // supports — which would be the same mistake as sending them alone, just slower.
            lastWallSendTick = null;
        }

        return new BotDecision(new QueueSendCommand(player.PlayerId, tick, creep.Id, sendQuantity));
    }

    /// <summary>
    /// A creep that can carry a wave on its own: it walks the maze and buffs nobody.
    /// </summary>
    /// <remarks>
    /// The inverse — an "escort" — is anything whose value depends on other creeps being there.
    /// That is the four aura supports, whose buffs land on nothing when they travel alone, and
    /// Spire Turret Walker, which has 10 health and survives only while the towers are busy with
    /// somebody else. Both are wasted gold as an opening move, which is exactly what the bots did
    /// with them before this: they select by cost, and cost says nothing about needing company.
    /// </remarks>
    private static bool IsWall(CreepDefinition creep) =>
        creep.Support == CreepSupportRole.None && !creep.IgnoresMaze;

    /// <summary>
    /// Reads this profile's tuning data from content instead of a hardcoded constant, so balance
    /// changes (tower costs, creep costs) don't silently desync the bot's reserve/coverage/
    /// pressure behavior from the numbers it was tuned against. Throws rather than falling back
    /// silently, matching the project's existing "detect missing content before play" principle
    /// (see docs/ARCHITECTURE.md's Content And Persistence section) — a missing profile entry is a
    /// content authoring bug, not a runtime condition to paper over.
    /// </summary>
    public BotProfileDefinition ResolveProfile(ContentCatalog content)
    {
        var id = BotProfileIds.For(profile);
        return content.BotProfiles.FirstOrDefault(candidate => candidate.Id.Equals(id))
            ?? throw new InvalidOperationException($"No BotProfileDefinition found for '{id.Value}'. Every BotDecisionProfile needs a matching content entry.");
    }

    public int GoldReserveFloor(ContentCatalog content) => ResolveProfile(content).MinimumGoldReserve;

    /// <summary>
    /// Higher defense bias means the bot tolerates less incoming pressure before it stops sending
    /// and holds/builds instead — this is the reactive equivalent of the old tick-scheduled
    /// "build defense before sending" behavior, but driven by actual lane threat rather than tick
    /// number.
    /// </summary>
    /// <remarks>
    /// Scales up with the bot's own tower count so tolerance grows alongside its actual defense
    /// capacity. Without this, a bot facing a sustained-aggressive neighbor could hit the
    /// threshold once, stay there indefinitely (more towers doesn't inherently clear an existing
    /// creep backlog faster than new pressure arrives), and never send again for the rest of the
    /// match — caught by <c>GameplayScenarioTests.Mixed_pressure_scenario_records_distinct_send_roles_and_defensive_response</c>,
    /// where a flat threshold left a 4-tower Balanced bot permanently locked out of sending.
    /// </remarks>
    public int PressureThreshold(ContentCatalog content, int ownedTowerCount) =>
        PressureThreshold(content, ownedTowerCount, 0L);

    /// <summary>
    /// How much incoming creep health this bot tolerates before it holds gold and builds instead of
    /// sending.
    /// </summary>
    /// <remarks>
    /// The tick argument is not decoration — without it this number is measured in different units
    /// from the thing it is compared against, and that was a real defect rather than a rounding
    /// concern.
    ///
    /// The left side, <c>LiveCreepHealthIn</c>, rides two escalators: `MatchEscalationRules` raises
    /// every new creep's health by 20% per interval for the whole match, and a tier-3 send category
    /// multiplies it again by 225%. The right side was a fixed 40–70 plus 15 a tower, so it topped
    /// out around 340 while the left side grew without bound. Past that crossing every non-Greedy
    /// bot reads its lane as permanently under pressure and <see cref="TrySend"/> returns early
    /// forever — not sending less, sending *nothing*, for the rest of the match.
    ///
    /// That is where the hoarding came from. Greedy is exempt from the check, which is exactly why
    /// the five Greedy seats spent down to 3–70 gold while the Defensive and Balanced seats died
    /// holding 23,932 and 14,934. It looked like a spending-rate problem and was a gate that had
    /// silently latched shut.
    ///
    /// Scaling by the same escalation the creeps get keeps both sides in the same units, so
    /// "am I under pressure" keeps meaning what it meant at tick 0 instead of drifting to "yes".
    /// </remarks>
    public int PressureThreshold(ContentCatalog content, int ownedTowerCount, long tick)
    {
        var defenseBias = ResolveProfile(content).DefenseBias;
        var baseThreshold = System.Math.Max(10, 120 - defenseBias);
        var atTickZero = baseThreshold + (ownedTowerCount * 15);
        return atTickZero * MatchEscalationRules.CreepHealthPercentFor(tick) / 100;
    }

    /// <summary>Sends, if this bot's own lane can currently spare the gold and the attention.</summary>
    private void TrySend(PlayerId playerId, IBotMatchContext match)
    {
        if (!HasMinimumDefenseCoverage(playerId, match) || IsLaneUnderPressure(playerId, match))
        {
            return;
        }

        if (Decide(match.PlayerState(playerId), match.Content, match.Tick).Command is QueueSendCommand send)
        {
            match.TrySend(send.PlayerId, send.CreepId, send.Quantity);
            lastSendTick = match.Tick.Value;
        }
    }

    /// <summary>
    /// Whether this bot has finished the opening tower package its profile asks for.
    /// </summary>
    /// <remarks>
    /// A floor gate, never a ceiling on how much a bot can build — <see cref="TryBuild"/> keeps
    /// building past this number for as long as gold and legal cells allow. The number is authored
    /// per profile (<see cref="BotProfileDefinition.MinimumTowerCoverage"/>); 0 means "send from the
    /// first tick", which is how the aggressive-economy profile opens.
    /// </remarks>
    private bool HasMinimumDefenseCoverage(PlayerId playerId, IBotMatchContext match)
    {
        var coverage = ResolveProfile(match.Content).MinimumTowerCoverage;
        return coverage <= 0 || match.TowersOwnedBy(playerId).Count >= coverage;
    }

    /// <summary>
    /// Gold this bot is holding back from towers because it is saving for a send it cannot yet afford.
    /// </summary>
    /// <remarks>
    /// <see cref="TakeTurn"/> gives sending FIRST claim on a tick's gold, which is enough only while
    /// the send is affordable on the tick it is wanted. It is not enough when a creep costs more than
    /// the surplus one income payout brings: <see cref="TryBuild"/> has no cap, so it spends the
    /// difference on another tower every tick and the balance never reaches the creep's price. Income
    /// only rises by sending, so the bot cannot grow its way out either — it is a closed loop, and the
    /// bot stops attacking for the rest of the match.
    ///
    /// Found on 2026-08-19 when the creep roster doubled in price. Two Greedy bots built 44 towers
    /// between them and sent nothing after tick 60: zero creeps on the board from tick 240 out to
    /// tick 900, with income frozen at 14 and 16. The roster change exposed this rather than caused
    /// it — at the old prices the cheapest wall happened to sit under one payout's surplus, so the
    /// loop existed and was never entered.
    ///
    /// Deliberately inert unless the bot is actually starving: it returns 0 whenever the bot could
    /// already afford its cheapest wall, and 0 whenever it should be building anyway (coverage not
    /// met, or its own lane under pressure). That keeps every already-working case byte-identical
    /// and confines the change to the case that was broken.
    /// </remarks>
    private int SendSavingsFor(PlayerId playerId, IBotMatchContext match, BotProfileDefinition profileDefinition)
    {
        if (!HasMinimumDefenseCoverage(playerId, match) || IsLaneUnderPressure(playerId, match))
        {
            return 0;
        }

        var player = match.PlayerState(playerId);
        if (player.IsEliminated)
        {
            return 0;
        }

        // Only once the bot has actually stopped attacking. Saving whenever a send is unaffordable
        // makes sends strictly dominate building — measured, and it took two Greedy bots to 58 sends
        // and zero towers, which is as broken as the starvation it was fixing, just in the other
        // direction. Gating on elapsed silence keeps the rule inert for a bot that is spending fine.
        if (match.Tick.Value - (lastSendTick ?? 0L) <= SendStarvationTicks)
        {
            return 0;
        }

        // The cheapest WALL, matching SelectCreep's refusal to send an unescorted support. Saving for
        // a support the bot would decline to buy would stall building for a purchase that never comes.
        var cheapestWall = PreferredCreepIds(player.Income.Amount)
            .Distinct()
            .Select(id => match.Content.Creeps.FirstOrDefault(creep => creep.Id.Value == id))
            .Where(creep => creep is not null && IsWall(creep!))
            .Select(creep => creep!.Cost.Amount)
            .DefaultIfEmpty(0)
            .Min();

        var available = player.Gold.Amount - profileDefinition.MinimumGoldReserve;
        return cheapestWall > available ? cheapestWall : 0;
    }

    /// <summary>
    /// True once this bot's own lane is carrying enough incoming creep health that it should hold
    /// and build instead of spending gold on sends — the reactive replacement for the old fixed
    /// opening-tower-count gate, driven by actual lane threat rather than a tick schedule.
    /// </summary>
    /// <remarks>
    /// Greedy is exempt, and deliberately not by way of a very high threshold: sending is its
    /// primary lever (see docs/ARCHITECTURE.md's Bot Controller section), not something pressure
    /// should suppress at any level.
    /// </remarks>
    private bool IsLaneUnderPressure(PlayerId playerId, IBotMatchContext match)
    {
        if (profile == BotDecisionProfile.Greedy)
        {
            return false;
        }

        var incomingHealth = match.LiveCreepHealthIn(match.HomeLaneFor(playerId));
        return incomingHealth >= PressureThreshold(match.Content, match.TowersOwnedBy(playerId).Count, match.Tick.Value);
    }

    /// <summary>
    /// Builds the next tower in this profile's authored build order, in the best mazing cell for it.
    /// </summary>
    /// <remarks>
    /// Deliberately has no elimination check. A defeated seat's lane is wiped, towers and all, and a
    /// bot that keeps rebuilding into it costs nothing and changes nothing — but adding the check
    /// would change what every seat after it in the tick can afford, because entity ids and gold are
    /// shared state. This is a move, not a tidy-up.
    ///
    /// Spending is gated on the profile's gold reserve floor, so a bot cannot build itself down to
    /// nothing and then be unable to answer a wave.
    /// </remarks>
    private void TryBuild(PlayerId playerId, IBotMatchContext match)
    {
        var profileDefinition = ResolveProfile(match.Content);
        // Gold earmarked for a send this bot is saving up for, on top of the profile's reserve floor.
        // Without it a bot can never accumulate past one tick's surplus — see SendSavingsFor.
        var sendSavings = SendSavingsFor(playerId, match, profileDefinition);
        var buildableGold = match.PlayerState(playerId).Gold.Amount - profileDefinition.MinimumGoldReserve - sendSavings;
        // The seat's committed line, or Unchosen while it has not built yet — the planner treats
        // that as "every line is in scope", so this behaves exactly as before until the lock lands.
        var towerId = BotBuildPlanner.NextTower(
            profileDefinition,
            match.TowersOwnedBy(playerId).Count,
            match.Content,
            BuildLineFor(match.PlayerState(playerId)),
            buildableGold);
        if (towerId is null)
        {
            return;
        }

        // Throws with a clear message rather than a bare key-not-found from the middle of a tick,
        // matching ResolveProfile above. ContentValidator already rejects a build order naming a
        // tower the catalog does not have, so reaching this means validation was skipped.
        var tower = match.FindTower(towerId.Value)
            ?? throw new InvalidOperationException($"Bot profile '{profileDefinition.Id.Value}' build order names tower '{towerId.Value}', which is not in this catalog.");

        if (buildableGold < tower.Cost.Amount)
        {
            return;
        }

        var laneId = match.HomeLaneFor(playerId);
        var position = BotBuildPlanner.BestMazingPlacement(match, playerId, laneId, towerId.Value, tower.RangeCells);
        if (position is not null)
        {
            match.TryPlaceTower(playerId, laneId, towerId.Value, position.Value);
        }
    }

    /// <summary>
    /// Buys the next tier this bot can afford, in whichever category it has already committed to.
    /// </summary>
    /// <remarks>
    /// Bots must buy tiers or the feature makes them strictly worse opponents: they maze well enough
    /// now that a tier-3 human against a tier-1 bot defence would be a walkover.
    ///
    /// Which category: whichever this bot has ALREADY invested in — the line it has built the most
    /// towers in, or the category it has sent the most creeps from. That mirrors what the tiers do
    /// (deepen a commitment rather than broaden one) and needs no new tuning knob.
    ///
    /// Which side: from the profile's own Aggression and DefenseBias rather than a separate
    /// heuristic, so a Greedy bot (90/10) deepens its sends, a Defensive one (20/80) deepens its
    /// towers, and the choice is content-tunable alongside every other bot knob. This replaced an
    /// earlier rule of "tower tier while under pressure, send tier otherwise", which read sensibly
    /// and measured terribly: bots are under pressure most of the time, so they poured almost
    /// everything into defence, and two of them facing each other could no longer finish a match at
    /// ANY tower multiplier. Preference dominated the multiplier completely — holding the multiplier
    /// and only changing which side bots buy took the same match from a stalemate past 6000 ticks to
    /// 3336, faster than the 3627 the game takes with no tiers at all.
    ///
    /// A tie (Balanced, 50/50) breaks toward the SEND side deliberately. Defence already compounds
    /// for free through an unbounded tower count, so the attacking side is the one that needs the
    /// help, and it is the side that lets a match end.
    ///
    /// Gated on the reserve floor only. An additional "must still afford the next tower afterwards"
    /// term was tried and measured worse on both counts it was meant to help: it did not recover the
    /// mazing it was added for (lane 2 stayed at 22 cells) and it pushed match completion from 3422
    /// ticks to 5078 by starving the creep tiers that let an attack close a game out. Running after
    /// <see cref="TryBuild"/> already gives towers first claim on the tick's gold, which turns out to
    /// be the whole of the protection worth having.
    /// </remarks>
    private void TryBuyCategoryTier(PlayerId playerId, IBotMatchContext match)
    {
        var player = match.PlayerState(playerId);
        if (player.IsEliminated)
        {
            return;
        }

        var profileDefinition = ResolveProfile(match.Content);
        var kind = profileDefinition.DefenseBias > profileDefinition.Aggression
            ? CategoryKind.TowerLine
            : CategoryKind.SendCategory;
        var categoryIndex = kind == CategoryKind.TowerLine
            ? MostBuiltTowerLine(playerId, match)
            : IndexOfMax(match.SendsByCategory(playerId));

        var targetTier = (kind == CategoryKind.TowerLine
            ? player.TowerLineTier(categoryIndex)
            : player.SendCategoryTier(categoryIndex)) + 1;
        if (targetTier > CategoryTierRules.MaxTier)
        {
            return;
        }

        // The escalated price, matching what the bridge will charge. Budgeting against the list
        // price would have the bot decide it can afford a tier, ask for it, and be refused — every
        // tick, forever, once it holds a few tiers.
        var cost = CategoryTierRules.CostFor(kind, targetTier, CategoryTierRules.UpgradesOwned(player));
        if (player.Gold.Amount - profileDefinition.MinimumGoldReserve < cost)
        {
            return;
        }

        match.TryBuyCategoryTier(playerId, kind, categoryIndex, targetTier);
    }

    /// <summary>
    /// Brings one of this bot's placed towers up to the line tier it has already bought.
    /// </summary>
    /// <remarks>
    /// Without this a bot buys a line tier and never realises it: the tier only reaches towers built
    /// afterwards, so a bot that has finished building would carry a tier it paid for and gets
    /// nothing from. That would make the tier a pure waste of its gold and the bot a weaker opponent
    /// than before the feature existed.
    ///
    /// Upgrades the LOWEST-tier tower first, so a bot levels its whole line evenly rather than
    /// pouring everything into one tower — and ties break on entity id so the choice stays
    /// deterministic for replays. Written as a single min pass rather than an
    /// <c>OrderBy().ThenBy().First()</c>: (tier, entity id) is a total order over towers, so the two
    /// agree by construction, and this runs once per bot per tick against every tower it owns.
    /// </remarks>
    private void TryUpgradeTower(PlayerId playerId, IBotMatchContext match)
    {
        var player = match.PlayerState(playerId);
        if (player.IsEliminated)
        {
            return;
        }

        var laneId = match.HomeLaneFor(playerId);
        TowerCombatState? candidate = null;
        TowerDefinition? candidateDefinition = null;
        foreach (var tower in match.TowersOwnedBy(playerId))
        {
            if (!tower.LaneId.Equals(laneId) || tower.Tier >= CategoryTierRules.MaxTier)
            {
                continue;
            }

            var definition = match.FindTower(tower.TowerId);
            if (definition is null || tower.Tier >= player.TowerLineTier(definition.CategoryIndex))
            {
                continue;
            }

            if (candidate is null
                || tower.Tier < candidate.Tier
                || (tower.Tier == candidate.Tier && tower.EntityId.Value < candidate.EntityId.Value))
            {
                candidate = tower;
                candidateDefinition = definition;
            }
        }

        if (candidate is null)
        {
            return;
        }

        var cost = CategoryTierRules.TowerUpgradeCost(candidateDefinition!.Cost.Amount);
        if (player.Gold.Amount - ResolveProfile(match.Content).MinimumGoldReserve < cost)
        {
            return;
        }

        match.TryUpgradeTower(playerId, laneId, candidate.Position);
    }

    /// <summary>The tower line this bot has the most towers standing in, lowest index on a tie.</summary>
    private static int MostBuiltTowerLine(PlayerId playerId, IBotMatchContext match)
    {
        var counts = new int[PlayerEconomyState.CategoryCount];
        foreach (var tower in match.TowersOwnedBy(playerId))
        {
            var line = match.FindTower(tower.TowerId)?.CategoryIndex ?? -1;
            if (line >= 0 && line < counts.Length)
            {
                counts[line]++;
            }
        }

        return IndexOfMax(counts);
    }

    /// <summary>
    /// The busiest category, lowest index on a tie.
    /// </summary>
    /// <remarks>
    /// The tie rule is the reason this is written out rather than being a <c>MaxBy</c>: a bot that
    /// has committed to nothing yet must pick the same category every time, or two otherwise
    /// identical matches diverge on their first tier purchase.
    /// </remarks>
    private static int IndexOfMax(IReadOnlyList<int> counts)
    {
        var best = 0;
        for (var index = 1; index < counts.Count; index++)
        {
            if (counts[index] > counts[best])
            {
                best = index;
            }
        }

        return best;
    }

    /// <summary>
    /// The creep ids this profile is willing to send at this income, before affordability.
    /// </summary>
    /// <remarks>
    /// Lifted out of <see cref="SelectCreep"/> so <see cref="SendSavingsFor"/> can ask what this bot
    /// is TRYING to buy, not just what it can already afford. SelectCreep filters this list by gold;
    /// a bot saving up needs the unfiltered version, and the two must not drift apart.
    /// </remarks>
    private string[] PreferredCreepIds(int income) =>
        profile switch
        {
            BotDecisionProfile.Greedy => income >= 45
                ? new[] { "creep.colossus", "creep.siege", "creep.turret_walker", "creep.warden", "creep.stalker", "creep.shade", "creep.brute", "creep.revenant", creepId.Value }
                : income >= 20
                    ? new[] { "creep.stalker", "creep.shade", "creep.zephyr", "creep.brute", "creep.revenant", creepId.Value }
                    : new[] { "creep.brute", creepId.Value, "creep.wisp" },
            BotDecisionProfile.Balanced => income >= 35
                ? new[] { "creep.warden", "creep.obsidian_brute", "creep.burrower", "creep.shade", "creep.brute", creepId.Value, "creep.swarm" }
                : new[] { "creep.serpent", "creep.brute", creepId.Value, "creep.swarm" },
            BotDecisionProfile.Defensive => income >= 30
                ? new[] { "creep.warden", "creep.obsidian_brute", "creep.burrower", "creep.serpent", "creep.brute", "creep.runner", creepId.Value, "creep.swarm" }
                : new[] { "creep.runner", creepId.Value, "creep.swarm", "creep.wisp" },
            _ => new[] { creepId.Value }
        };

    private CreepDefinition SelectCreep(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var available = player.Gold.Amount - GoldReserveFloor(content);
        var income = player.Income.Amount;
        // Category 1 (creep.wisp/.revenant/.obsidian_brute/.serpent/.turret_walker) was slotted in
        // when it was still a stat tier — Greedy leaned on Turret Walker and Revenant for income
        // efficiency and Wisp as a cheap opener, Balanced and Defensive on Obsidian Brute and
        // Serpent as tankier alternatives to Brute. None of those reasons survive the SUPPORT
        // rework: those five are escorts now, and their membership here is what makes them
        // available AFTER a wall rather than instead of one.
        //
        // These lists express *membership* — which creeps a profile is willing to send in a given
        // income tier. Ordering is not a preference and is no longer authored by hand: the list is
        // sorted by descending cost below before it is used.
        //
        // That sort is load-bearing. The loop tries each id in order and returns the first one
        // `available` gold covers, so any id sitting after a cheaper one is mathematically
        // unreachable — affording the cheaper one never implies being unable to afford the pricier
        // one, so the cheaper entry always wins first. An early hand-ordered version left
        // creep.brute/.shade/.siege/.serpent/.obsidian_brute at zero sends across an entire batch
        // playtest while creep.revenant alone took 207 of 486, and it was only caught by replay
        // analysis. Hand-ordering also silently broke `creepId.Value`, the bot's configured
        // primary creep, which was appended last regardless of price: a bot given the 40-gold
        // Siege as its primary could never actually send it from a tier whose other entries were
        // cheaper. Sorting makes the invariant structural instead of a comment to be honoured.
        // Category 3 ("ELITE", added 2026-07-28) slotted in at cost-sorted positions, same as
        // Category 2 before it. Costs as of the SUPPORT rework: colossus 52, siege 40, warden 34,
        // obsidian_brute 30, stalker 28, serpent 27, burrower 26, shade 24, turret_walker 23,
        // zephyr 22, revenant 19, brute 18, wisp 12, runner 10, swarm 6. Greedy takes the expensive
        // top end where its income allows; Balanced and Defensive gain the two mid-tier tanks
        // (warden, burrower) that suit their health-per-gold bias.
        //
        // Membership still says nothing about composition, which is what the escort window above
        // adds. Five of the ids these lists name — wisp, revenant, obsidian_brute, serpent and
        // turret_walker — stopped being bodies when category 1 became SUPPORT, and until the window
        // existed the bots kept buying them as though they still were.
        var preferredIds = PreferredCreepIds(income);

        var affordable = preferredIds
            .Distinct()
            .Select(id => content.Creeps.FirstOrDefault(creep => creep.Id.Value == id))
            .Where(creep => creep != null && available >= creep.Cost.Amount)
            .OrderByDescending(creep => creep!.Cost.Amount)
            .ThenBy(creep => creep!.Id.Value, System.StringComparer.Ordinal)
            .ToArray();

        // Having just sent a wall, ESCORT it. Gating escorts was necessary and turned out not to be
        // sufficient: the sort above takes the dearest creep a bot can afford, and walls are the
        // dear ones, so a rich bot opened the escort window and then spent it on another Colossus
        // every time. Escorts were only ever reached when the bot was too poor for a wall, which is
        // the exact opposite of when they are worth sending. Preferring them inside the window is
        // what actually produces wall-then-escort rather than merely permitting it.
        var escortsAllowed = lastWallSendTick is { } sentAt && tick.Value - sentAt <= EscortFollowWindowTicks;
        if (escortsAllowed)
        {
            var escort = affordable.FirstOrDefault(creep => !IsWall(creep!));
            if (escort is not null)
            {
                return escort;
            }
        }

        // Otherwise a wall — and specifically a wall, not just "the next thing down the list".
        // Falling back to any affordable creep here would let an unescorted support through on the
        // ticks a bot cannot afford a body, which is the behaviour the window exists to stop.
        foreach (var candidate in affordable)
        {
            if (IsWall(candidate!))
            {
                return candidate!;
            }
        }

        // Throws with a clear message rather than a bare "sequence contains no matching element" if
        // this bot's configured primary creep isn't in content — matching the same "detect missing
        // content before play" principle as ResolveProfile above. BotLaneOptions.PrimaryCreepId is
        // never validated at construction time, so this is the first point a typo would surface.
        return content.Creeps.FirstOrDefault(creep => creep.Id.Equals(creepId))
            ?? throw new InvalidOperationException($"No CreepDefinition found for this bot's configured primary creep '{creepId.Value}'.");
    }

    private int GetSendQuantity(PlayerEconomyState player, ContentCatalog content, CreepDefinition creep)
    {
        if (player.IsEliminated)
        {
            return 0;
        }

        var reserve = GoldReserveFloor(content);

        var available = player.Gold.Amount - reserve;
        if (available < creep.Cost.Amount)
        {
            return 0;
        }

        // An aura is on or off, so a second copy of one buys nothing — two Menders heal a creep
        // once. Sending the profile's usual batch of three would be three times the price for the
        // same effect, which is the sort of waste that makes a category look weak when it is really
        // just being bought wrong. Spire Turret Walker is deliberately NOT included: it carries no
        // aura, so more of them really is more pressure.
        if (creep.Support != CreepSupportRole.None)
        {
            return 1;
        }

        var max = available / creep.Cost.Amount;

        // The base batch is the profile's character: Greedy commits three, Balanced two, Defensive
        // one. It used to be the whole answer, and that is what made two profiles hoard.
        //
        // A flat cap cannot spend a growing income. At the 900 ceiling a seat earns 18 gold a tick
        // while a Defensive bot buys one ~10-gold creep per opportunity, so the difference banks
        // forever: measured across a full match, the Defensive bot died holding 23,932 gold and the
        // Balanced bot 14,934, against a winner who finished on 53. Two of seven opponents were not
        // converting income into pressure at all, which makes every balance number taken against
        // this table a number taken against a table that is not playing.
        var baseBatch = profile switch
        {
            BotDecisionProfile.Greedy => 3,
            BotDecisionProfile.Balanced => 2,
            _ => 1
        };

        // No jitter here, and the two attempts that put one here are worth recording so the next
        // person does not spend the afternoon I did.
        //
        // Send quantity looks like the obvious place for the match seed to reach the simulation, and
        // it is the wrong one twice over. Scaling the batch with the bank — meant to stop the
        // hoarding — starved the build step that runs after the send in TakeTurn, and the Greedy
        // seats finished a 1200-tick match having built ONE tower each with two lanes empty. Adding
        // only a small +-1 breaks `Bot_profiles_produce_different_income_versus_defense_behavior`,
        // because affordability already clamps the profiles unevenly: on that test's catalog Greedy
        // picks a 30g brute and can afford 3, while Defensive picks a 10g runner and is limited by
        // its base of 1, so a jitter that lifts Defensive by one ties it with a Balanced already
        // clamped to 2. The strict Greedy > Balanced > Defensive ordering is the thing that test
        // exists to defend, and it is worth more than the variation.
        //
        // The seed is plumbed as far as this class (see the remarks on `random`) and deliberately
        // not consumed yet. It wants a decision that is not pinned to an exact value by a test and
        // not upstream of the build step's gold — tower cell choice among equally-ranked cells is
        // the strongest candidate.
        return System.Math.Min(baseBatch, max);
    }
}
