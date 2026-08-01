using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Pathing;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.Simulation.Bridge;

public sealed class LocalVerticalSlice
{
    private const int StartingLives = 220;

    private readonly ContentCatalog content;
    private readonly EconomyService economy;
    private readonly GridPathService pathService;
    private readonly CombatService combat;
    private readonly CommandContentValidator commandValidator;
    private readonly Dictionary<LaneId, LaneGrid> grids;
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> routes;
    // Not readonly: creeps, towers and lane ownership are fixed for a match but category tiers are
    // not, and combat reads them through this. Refreshed once per tick in AdvanceOneTick.
    private CombatContent combatContent;
    private readonly LocalMatchOptions options;
    private readonly LocalMatchTopology topology;
    private readonly List<ISimulationEvent> pendingEvents = new();
    private readonly Dictionary<PlayerId, BotController> bots;
    private readonly List<AcceptedCommandRecord> acceptedCommands = new();
    private readonly List<BotDecisionRecord> botDecisionRecords = new();

    private EconomyPlayerSet players;
    private CombatState combatState;
    private SimulationTick tick;
    private long nextEntityId = 1;
    private bool matchStarted;
    private bool matchEnded;

    public MatchSummary? MatchSummary { get; private set; }

    public int IncomeIntervalTicks => economy.IncomeIntervalTicks;

    /// <summary>Send-only telemetry, not a reproducible replay — see ReplayRecord's remarks.</summary>
    public ReplayRecord GetReplayRecord() => new ReplayRecord(options.Seed, content.Version, content.Maps[0].Id, players.Players.Select(player => player.PlayerId).ToArray(), tick, acceptedCommands);

    /// <summary>
    /// Total creep entities held in combat state, unfiltered by HasLeaked. GetSnapshot's creep list goes
    /// through GetCreepSnapshots, which already excludes spent lane-transfer entities, so it cannot show
    /// whether one is still sitting in CombatState. Exists to verify the count stays bounded to live
    /// creeps instead of accumulating a tombstone per lane hop — see OPEN_ITEMS.md's retired 2026-07-29 review, "every lane hop leaves a permanent spent entity".
    /// </summary>
    public int DiagnosticCombatEntityCount() => combatState.Creeps.Count;

    public BotDiagnosticsSnapshot GetBotDiagnostics()
    {
        var profiles = bots
            .Select(bot => new BotProfileSnapshot(bot.Key, bot.Value.Profile, bot.Value.PrimaryCreepId))
            .OrderBy(profile => profile.PlayerId.Value)
            .ToArray();
        var recentDecisions = botDecisionRecords
            .Skip(System.Math.Max(0, botDecisionRecords.Count - 12))
            .ToArray();
        return new BotDiagnosticsSnapshot(profiles, recentDecisions);
    }

    public LocalVerticalSlice(ContentCatalog content, bool enableBots = true)
        : this(content, LocalMatchOptions.Default, enableBots)
    {
    }

    public LocalVerticalSlice(ContentCatalog content, LocalMatchOptions options, bool enableBots = true)
    {
        this.content = content;
        this.options = options;
        topology = new LocalMatchTopology(options.LaneCount);
        // sendCooldownTicks: 0 — gold is the only thing that gates a send.
        //
        // This was 30 ticks (7.5 seconds at 4 ticks/second). The rule had been described in the design
        // docs for a long time but never actually enforced until the seats/authority pass switched it
        // on, and once it ran it was clearly wrong for the game: 7.5s between any two sends made cheap
        // chaff like the 5-gold Crystal Wisp impossible to use as chaff, and the dock had to grow a
        // countdown just to explain why a card you could plainly afford refused to work.
        //
        // The enforcement in EconomyService is left intact and still tested with explicit values, so
        // the rule can be turned back on by changing this one number. CreepDefinition.IgnoresSendCooldown
        // also stays: it is inert at 0, but if a cooldown ever returns, Category 2 remains exempt.
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 0, sellRefundPercent: 50, leakLifeLoss: 1, incomeCeiling: 600, incomeTaperStart: 300));
        pathService = new GridPathService();
        combat = new CombatService();
        commandValidator = new CommandContentValidator();

        var map = content.Maps[0];
        grids = new Dictionary<LaneId, LaneGrid>();
        routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        foreach (var laneId in topology.Lanes)
        {
            grids[laneId] = new LaneGrid(map);
            routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
        }
        combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            topology.LaneOwners);
        players = CreateStartingPlayers(topology.Players);
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        bots = enableBots ? CreateBots(topology.Players) : new Dictionary<PlayerId, BotController>();
        tick = new SimulationTick(0);
    }

    /// <summary>
    /// The active content catalog, so the client can read authored values (tower costs and the
    /// like) rather than keeping its own copy of them.
    /// </summary>
    public ContentCatalog Content => content;

    /// <summary>
    /// Number of cells creeps must walk in a lane, which is the measure of how well it has been mazed.
    /// </summary>
    /// <remarks>
    /// Exposed because mazing is the core skill this game is about, and it was previously impossible to
    /// assert on from outside — so nothing noticed that the bots never did it.
    /// </remarks>
    public int RouteLength(LaneId laneId) => routes.TryGetValue(laneId, out var route) ? route.Count : 0;

    /// <summary>
    /// The seat the local client drives. Presentation and input code should ask for this rather
    /// than assuming player 1, so the same client can be seated anywhere in the match — the
    /// prerequisite for remote players each driving their own seat.
    /// </summary>
    public PlayerId LocalPlayerId => options.LocalPlayerId;

    /// <summary>
    /// The lane the local seat defends. Always derived from the seat rather than hardcoded.
    /// </summary>
    public LaneId LocalPlayerLaneId => topology.HomeLaneFor(options.LocalPlayerId);

    public VerticalSliceCommandResult PreviewPlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        return ValidateTowerPlacement(playerId, laneId, towerId, position).Result;
    }

    public VerticalSliceCommandResult PlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var validation = ValidateTowerPlacement(playerId, laneId, towerId, position);
        if (!validation.Result.Accepted)
        {
            return validation.Result;
        }

        var grid = validation.Grid!;
        var placement = validation.Placement!;
        var tower = validation.Tower!;
        var player = validation.Player!;

        var towerEntityId = NextEntityId();
        grids[laneId] = grid.WithOccupied(position);
        routes[laneId] = placement.Route;
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - tower.Cost.Amount)));
        // The owner's line tier is baked in HERE, at build time. A tier bought later raises what
        // new towers are built at and leaves this one where it is until it is paid for
        // individually — see UpgradeTower.
        var builtTier = player.TowerLineTier(tower.CategoryIndex);
        combatState = new CombatState(
            combatState.Creeps,
            combatState.Towers.Concat(new[] { new TowerCombatState(towerEntityId, towerId, playerId, laneId, position, builtTier) }));
        pendingEvents.Add(new TowerPlacedEvent(tick, playerId, laneId, towerEntityId, towerId, position));
        return VerticalSliceCommandResult.Accept();
    }

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId) => QueueSend(playerId, creepId, 1);

    /// <summary>
    /// Local editor/playtest helper for stress and screenshot scenarios. This deliberately sits on the
    /// vertical-slice bridge rather than in EconomyService so production economy rules stay unchanged.
    /// </summary>
    public void GrantLocalPlaytestGold(PlayerId playerId, Gold amount)
    {
        if (amount.Amount <= 0)
        {
            return;
        }

        var player = players.Get(playerId);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + amount.Amount)));
    }

    /// <summary>
    /// Clears a player's send cooldown so editor/playtest scenarios can queue several sends in one
    /// tick. Sits alongside <see cref="GrantLocalPlaytestGold"/> for the same reason: the
    /// production rule in EconomyService stays untouched.
    /// </summary>
    /// <remarks>
    /// The review capture states each queue a batch of sends back to back to build up load for a
    /// screenshot. Once the send cooldown started actually being enforced, everything after the
    /// first send in a state was rejected with CooldownActive and the captured board went nearly
    /// empty — the rule is right, but a capture scenario is not a player and should not be rate
    /// limited into producing nothing.
    /// </remarks>
    public void ClearLocalPlaytestSendCooldown(PlayerId playerId)
    {
        var player = players.Get(playerId);
        players = players.Replace(player.WithNextSendAvailableTick(tick));
    }

    /// <summary>
    /// Which player must send in order for the creeps to arrive in <paramref name="laneId"/>,
    /// or null if no active player currently routes there.
    /// </summary>
    /// <remarks>
    /// Review captures frame one lane and need to put creeps in it. Working the sender out from
    /// outside means duplicating the routing rule — a send goes to the home lane of the sender's
    /// next active opponent — along with the lane count, and it silently stops being true once a
    /// player is eliminated, because NextActiveOpponent then skips past them. The capture harness
    /// did exactly that and quietly filled the wrong lane. Asking the live topology instead keeps
    /// the answer correct as the match state changes.
    /// </remarks>
    public PlayerId? LocalPlaytestSenderForLane(LaneId laneId)
    {
        foreach (var candidate in topology.Players)
        {
            if (players.Get(candidate).IsEliminated)
            {
                continue;
            }

            var target = topology.NextActiveOpponent(candidate, id => !players.Get(id).IsEliminated);
            if (target is not null && topology.HomeLaneFor(target.Value).Equals(laneId))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Local editor/playtest helper for screenshot review. Creates a wounded creep that has just
    /// transferred into the next opponent lane, and emits the same leak/spawn event pairing the
    /// Unity renderer uses to display TRANSFER arrival cues.
    /// </summary>
    public VerticalSliceCommandResult CreateLocalPlaytestDamagedTransferCreep(ContentId creepId, int health)
    {
        var creep = content.Creeps.FirstOrDefault(definition => definition.Id.Equals(creepId));
        if (creep is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId);
        }

        var senderId = new PlayerId(1);
        var sourceLaneId = new LaneId(1);
        var targetLaneId = new LaneId(2);
        var sourceDefenderId = combatContent.GetLaneOwner(sourceLaneId);
        var targetDefenderId = combatContent.GetLaneOwner(targetLaneId);
        var clampedHealth = System.Math.Clamp(health, 1, System.Math.Max(1, creep.MaxHealth - 1));
        var leakedEntityId = NextEntityId();
        var sourceCreep = combat.SpawnCreep(leakedEntityId, creep, senderId, sourceLaneId).WithHealth(clampedHealth);
        var transferred = combat.TransferCreep(NextEntityId(), sourceCreep, targetLaneId).WithMovement(pathIndex: 3, movementProgress: 0);

        combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
        pendingEvents.Add(new LeakEvent(tick, senderId, sourceDefenderId, leakedEntityId, new Lives(1), creep.LeakBounty));
        pendingEvents.Add(new CreepSpawnedEvent(tick, transferred.EntityId, transferred.CreepId, transferred.SenderId, targetDefenderId));
        return VerticalSliceCommandResult.Accept();
    }

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId, int quantity)
    {
        var command = new QueueSendCommand(playerId, tick, creepId, quantity);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var creep = content.Creeps.First(definition => definition.Id.Equals(creepId));
        var targetPlayerId = topology.NextActiveOpponent(playerId, candidate => !players.Get(candidate).IsEliminated);
        if (targetPlayerId is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var send = economy.QueueSend(players, playerId, creep, quantity, tick, targetPlayerId.Value);
        if (!send.Accepted)
        {
            return VerticalSliceCommandResult.Reject(send.RejectionReason);
        }

        players = send.Players;
        var laneId = topology.HomeLaneFor(send.TargetPlayerId!.Value);
        // The sender's send-category tier is baked into health here, at purchase time. A tier
        // bought later does not reach these creeps.
        //
        // Match escalation is folded in the same way and for the same reason. The two are combined
        // multiplicatively rather than added, so a tier-3 send keeps being worth 225% of whatever
        // the escalated baseline is instead of the two bonuses diluting each other late in a match.
        var healthPercent = CategoryTierRules.CreepHealthPercentFor(players.Get(playerId).SendCategoryTier(creep.CategoryIndex))
            * MatchEscalationRules.CreepHealthPercentFor(tick.Value) / 100;
        var spawned = Enumerable.Range(0, quantity).Select(_ => combat.SpawnCreep(NextEntityId(), creep, playerId, laneId, healthPercent)).ToArray();
        combatState = new CombatState(combatState.Creeps.Concat(spawned), combatState.Towers);
        acceptedCommands.Add(new AcceptedCommandRecord(tick, playerId, creepId, quantity));
        pendingEvents.Add(new CreepQueuedEvent(tick, playerId, send.TargetPlayerId.Value, creepId, quantity));
        foreach (var spawnedCreep in spawned) pendingEvents.Add(new CreepSpawnedEvent(tick, spawnedCreep.EntityId, creepId, playerId, send.TargetPlayerId.Value));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Buys the next tier for one of a player's six categories.
    /// </summary>
    /// <remarks>
    /// Tiers must be bought in order, so the escalating cost is actually paid rather than skipped:
    /// a request for tier 3 from tier 1 is rejected even when the player could afford it outright.
    ///
    /// Gold is deducted and the tier written in the same step, against the same state read, so a
    /// purchase cannot half-apply. The new tier reaches combat on the next tick, when
    /// AdvanceOneTick refreshes CombatContent — towers already standing start hitting harder, and
    /// creeps already walking keep the health they spawned with.
    /// </remarks>
    public VerticalSliceCommandResult BuyCategoryTier(PlayerId playerId, CategoryKind categoryKind, int categoryIndex, int targetTier)
    {
        var command = new BuyCategoryTierCommand(playerId, tick, categoryKind, categoryIndex, targetTier);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var currentTier = categoryKind == CategoryKind.TowerLine
            ? player.TowerLineTier(categoryIndex)
            : player.SendCategoryTier(categoryIndex);
        if (targetTier != currentTier + 1)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidTier);
        }

        var cost = CategoryTierRules.CostFor(categoryKind, targetTier);
        if (player.Gold.Amount < cost)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientGold);
        }

        var purchased = player.WithGold(new Gold(player.Gold.Amount - cost));
        purchased = categoryKind == CategoryKind.TowerLine
            ? purchased.WithTowerLineTier(categoryIndex, targetTier)
            : purchased.WithSendCategoryTier(categoryIndex, targetTier);
        players = players.Replace(purchased);

        pendingEvents.Add(new CategoryTierPurchasedEvent(tick, playerId, categoryKind, categoryIndex, targetTier, new Gold(cost)));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Raises one placed tower by a tier, for gold.
    /// </summary>
    /// <remarks>
    /// The counterpart to a line tier. Buying ARCANE tier 2 raises what new Arrows are built at;
    /// this is how the Arrows already standing catch up, one at a time and one payment each. That
    /// is the decision the feature exists to create — upgrade the twelve towers already on the
    /// board, or spend the same gold building new ones that arrive at the higher tier already.
    ///
    /// The owner's line tier is the CEILING, so the category purchase stays the thing that unlocks
    /// progression and this stays the thing that realises it. A tower at the ceiling reports
    /// InvalidTier rather than silently taking the gold.
    /// </remarks>
    public VerticalSliceCommandResult UpgradeTower(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate =>
            candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position));
        if (tower is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner);
        }

        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var definition = content.Towers.First(candidate => candidate.Id.Equals(tower.TowerId));
        var ceiling = player.TowerLineTier(definition.CategoryIndex);
        if (tower.Tier >= ceiling || tower.Tier >= CategoryTierRules.MaxTier)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidTier);
        }

        var cost = CategoryTierRules.TowerUpgradeCost(definition.Cost.Amount);
        if (player.Gold.Amount < cost)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientGold);
        }

        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - cost)));
        var upgraded = tower.WithTier(tower.Tier + 1);
        combatState = combatState.ReplaceTower(upgraded);
        pendingEvents.Add(new TowerUpgradedEvent(tick, playerId, laneId, tower.EntityId, tower.TowerId, position, upgraded.Tier, new Gold(cost)));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Raises every tower this player owns in one line, spending as far as their gold reaches.
    /// </summary>
    /// <remarks>
    /// Deliberately a loop over <see cref="UpgradeTower"/> rather than a command of its own, so
    /// every rejection rule — ownership, elimination, the line's tier ceiling, affordability — is
    /// enforced by the code path a single upgrade already uses. A batch is therefore indistinguishable
    /// from the taps it stands in for, and there is no second copy of those rules to drift.
    ///
    /// Note this adds nothing to acceptedCommands, because UpgradeTower does not: that list is
    /// send-only telemetry rather than a reproducible command log (see ReplayRecord's remarks), so
    /// upgrades have never appeared in it, single or batched.
    ///
    /// It lives in the simulation rather than in the Unity adapter because this is real logic —
    /// which towers qualify, in what order, and how far the gold goes — and the client has no test
    /// framework, so logic placed there cannot be regression-tested at all.
    /// </remarks>
    public BatchUpgradeOutcome UpgradeTowerLine(PlayerId playerId, LaneId laneId, int lineIndex) =>
        RunUpgrades(playerId, laneId, candidate => candidate.LineIndex == lineIndex);

    /// <summary>
    /// Raises a hand-picked set of towers, spending as far as the player's gold reaches.
    /// </summary>
    /// <remarks>
    /// Shares everything with the whole-line version except which towers it considers, so a
    /// selection spanning two lines obeys each line's own tier ceiling without special-casing.
    /// </remarks>
    public BatchUpgradeOutcome UpgradeTowers(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        RunUpgrades(playerId, laneId, candidate => positions.Contains(candidate.Position));

    private BatchUpgradeOutcome RunUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {
        if (players.Get(playerId).IsEliminated)
        {
            return new BatchUpgradeOutcome(0, 0, 0);
        }

        var eligible = EligibleUpgrades(playerId, laneId, include);

        var upgraded = 0;
        var spent = 0;
        foreach (var candidate in eligible)
        {
            var result = UpgradeTower(playerId, laneId, candidate.Position);
            if (result.Accepted)
            {
                upgraded++;
                spent += candidate.Cost;
                continue;
            }

            if (result.RejectionReason == CommandRejectionReason.InsufficientGold)
            {
                // Cheapest first means nothing later in the list is affordable either.
                break;
            }
        }

        return new BatchUpgradeOutcome(upgraded, eligible.Count, spent);
    }

    /// <summary>
    /// What a whole-line upgrade would cost right now, without spending anything.
    /// </summary>
    /// <remarks>
    /// Shares its eligibility rule with <see cref="UpgradeTowerLine"/> through
    /// <see cref="EligibleLineUpgradeCosts"/>, so the price a card shows and the price the batch
    /// charges cannot disagree — the exact drift that had Arrow priced at three different numbers
    /// across the palette, the panel and the simulation.
    /// </remarks>
    public BatchUpgradeQuote QuoteTowerLineUpgrade(PlayerId playerId, LaneId laneId, int lineIndex) =>
        QuoteUpgrades(playerId, laneId, candidate => candidate.LineIndex == lineIndex);

    /// <summary>What raising a hand-picked set of towers would cost right now.</summary>
    public BatchUpgradeQuote QuoteTowerUpgrades(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        QuoteUpgrades(playerId, laneId, candidate => positions.Contains(candidate.Position));

    private BatchUpgradeQuote QuoteUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {
        var eligible = EligibleUpgrades(playerId, laneId, include);
        var gold = players.Get(playerId).Gold.Amount;

        var affordable = 0;
        var running = 0;
        foreach (var candidate in eligible)
        {
            if (running + candidate.Cost > gold)
            {
                // Cheapest first, so nothing later is affordable either.
                break;
            }

            running += candidate.Cost;
            affordable++;
        }

        return new BatchUpgradeQuote(eligible.Count, eligible.Sum(candidate => candidate.Cost), affordable, running);
    }

    /// <summary>
    /// What selling a hand-picked set of towers would return, without removing anything.
    /// </summary>
    public BatchSellQuote QuoteTowerSales(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions)
    {
        var owned = OwnedTowersAt(playerId, laneId, positions);
        return new BatchSellQuote(
            owned.Count,
            owned.Sum(tower => economy.CalculateSellRefund(
                content.Towers.First(definition => definition.Id.Equals(tower.TowerId))).Amount));
    }

    /// <summary>
    /// Sells a hand-picked set of towers.
    /// </summary>
    /// <remarks>
    /// Every sale goes through <see cref="SellTower"/>, which is what keeps the lane's route in
    /// step: removing a tower reopens its cell and the route is recomputed from the grid. Batching
    /// the removals and repathing once at the end would be faster and wrong — creeps would be
    /// walking a route computed against a maze that no longer exists for the duration.
    ///
    /// Unlike the upgrade batch there is nothing to run out of, so this always completes.
    /// </remarks>
    public BatchSellOutcome SellTowers(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions)
    {
        if (players.Get(playerId).IsEliminated)
        {
            return new BatchSellOutcome(0, 0);
        }

        var goldBefore = players.Get(playerId).Gold.Amount;
        var sold = 0;
        foreach (var tower in OwnedTowersAt(playerId, laneId, positions))
        {
            if (SellTower(playerId, tower).Accepted)
            {
                sold++;
            }
        }

        return new BatchSellOutcome(sold, players.Get(playerId).Gold.Amount - goldBefore);
    }

    /// <summary>
    /// The towers this player actually owns at those cells, newest first per cell.
    /// </summary>
    /// <remarks>
    /// Resolved to a list before anything acts on it, because selling mutates combatState as it
    /// goes. Ordering matches <see cref="SellTowerAt"/>'s so a batch of one sells the same tower a
    /// single tap would.
    /// </remarks>
    private List<TowerCombatState> OwnedTowersAt(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId)
                && candidate.LaneId.Equals(laneId)
                && positions.Contains(candidate.Position))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .ToList();

    /// <summary>
    /// The towers in one line this player could raise right now, cheapest first.
    /// </summary>
    /// <remarks>
    /// The single definition of "eligible" for every quote and every spend, whether the batch came
    /// from a whole line or from a hand-picked selection. Splitting them was the
    /// obvious shape and the wrong one: a card that prices a batch by one rule and a batch that
    /// charges by another is precisely how Arrow ended up costing 20, 25 and 14 gold in three
    /// different places in this client.
    /// </remarks>
    private List<BatchUpgradeCandidate> EligibleUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {

        // Resolved to a list up front. UpgradeTower replaces towers in combatState as it goes, so
        // enumerating this lazily while spending would walk entries whose tier has already moved.
        return combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId))
            .Select(candidate =>
            {
                var definition = content.Towers.First(entry => entry.Id.Equals(candidate.TowerId));
                return new BatchUpgradeCandidate(
                    candidate.Position,
                    candidate.Tier,
                    definition.CategoryIndex,
                    CategoryTierRules.TowerUpgradeCost(definition.Cost.Amount));
            })
            // The ceiling is read per candidate because a selection can span lines, and each line
            // has its own tier. Reading one ceiling up front only worked while every candidate was
            // guaranteed to share a line.
            .Where(candidate => candidate.Tier < players.Get(playerId).TowerLineTier(candidate.LineIndex)
                && candidate.Tier < CategoryTierRules.MaxTier
                && include(candidate))
            // Cheapest first, so "as many as you can afford" actually maximises how many go up.
            // Position breaks ties, so the order is stable rather than dependent on tower iteration.
            .OrderBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.Position.Y)
            .ThenBy(candidate => candidate.Position.X)
            .ToList();
    }

    /// <summary>
    /// Income this player would actually gain by sending now, taper included.
    /// </summary>
    /// <remarks>
    /// A creep's authored <c>IncomeGain</c> stops being the true number once a player crosses
    /// <see cref="EconomyRules.IncomeTaperStart"/>, so the send dock has to ask rather than print the
    /// authored value. The dock already reads COST from here for the same reason — a hardcoded 6G on
    /// the Swarm card is what let it enable at 6 gold and then be rejected for needing 18.
    /// </remarks>
    public int IncomeGainForSend(PlayerId playerId, ContentId creepId, int quantity)
    {
        var creep = content.Creeps.FirstOrDefault(definition => definition.Id.Equals(creepId));
        return creep is null ? 0 : economy.IncomeGainFor(players.Get(playerId).Income, creep, quantity);
    }

    /// <summary>
    /// Gold to raise the tower at this cell, or 0 when there is nothing there to raise.
    /// </summary>
    public int TowerUpgradeCostAt(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate =>
            candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position));
        return tower is null
            ? 0
            : CategoryTierRules.TowerUpgradeCost(content.Towers.First(candidate => candidate.Id.Equals(tower.TowerId)).Cost.Amount);
    }

    /// <summary>
    /// Every player's category tiers, in the form combat reads them.
    /// </summary>
    private IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> CurrentPlayerTiers()
    {
        var tiers = new Dictionary<PlayerId, PlayerCategoryTiers>();
        foreach (var player in players.Players)
        {
            tiers[player.PlayerId] = PlayerCategoryTiers.From(player);
        }

        return tiers;
    }

    public VerticalSliceCommandResult SellLastTower(PlayerId playerId)
    {
        var tower = combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .FirstOrDefault();
        return tower is null ? VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner) : SellTower(playerId, tower);
    }

    public VerticalSliceCommandResult SellTowerAt(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        var tower = combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .FirstOrDefault();
        return tower is null ? VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner) : SellTower(playerId, tower);
    }

    private VerticalSliceCommandResult SellTower(PlayerId playerId, TowerCombatState tower)
    {
        var towerDefinition = content.Towers.First(definition => definition.Id.Equals(tower.TowerId));
        var refund = economy.CalculateSellRefund(towerDefinition);
        var player = players.Get(playerId);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + refund.Amount)));
        combatState = combatState.RemoveTower(tower.EntityId);
        grids[tower.LaneId] = grids[tower.LaneId].WithoutOccupied(tower.Position);
        routes[tower.LaneId] = pathService.FindRoute(grids[tower.LaneId]).Route;
        pendingEvents.Add(new TowerSoldEvent(tick, playerId, tower.LaneId, tower.EntityId, refund));
        return VerticalSliceCommandResult.Accept();
    }

    public void AdvanceOneTick()
    {
        if (matchEnded)
        {
            return;
        }

        StartMatch();

        // Send decision runs before tower building (reordered 2026-07-28 — see
        // GD_TUNING_LOG.md). TryPlaceBotTower has no cap and previously ran first, so it
        // would absorb a bot's surplus gold into "one more tower" before a pricier preferred
        // creep (e.g. creep.serpent at 20g, creep.obsidian_brute at 30g) ever had a chance to
        // become affordable — confirmed via replay analysis showing those creeps at 0 uses
        // even though they were reachable on paper. Giving the send its claim on gold first,
        // with towers only spending what's left, fixes the starvation without adding a new
        // tunable cap.
        //
        // OrderBy(bot.Key.Value): Dictionary<PlayerId, BotController> enumeration order is
        // documented-unspecified, and it feeds NextEntityId() assignment (via QueueSend/PlaceTower)
        // here, which is the final tie-breaker in SelectTarget, Pulse's splash Take(2) and ChainArc.
        // It happens to be insertion order today (no removals from this dictionary), but a sim that
        // records and replays should not rely on that — SeedExpandedLaneBotOpeners already sorts for
        // the same reason (OPEN_ITEMS.md's retired 2026-07-29 review, "determinism: one real hazard").
        foreach (var bot in bots.OrderBy(bot => bot.Key.Value))
        {
            if (HasMinimumDefenseCoverage(bot.Key, bot.Value) && !IsLaneUnderPressure(bot.Key, bot.Value))
            {
                var decision = bot.Value.Decide(players.Get(bot.Key), content, tick);
                if (decision.Command is QueueSendCommand send)
                {
                    var sendResult = QueueSend(send.PlayerId, send.CreepId, send.Quantity);
                    if (sendResult.Accepted)
                    {
                        botDecisionRecords.Add(new BotDecisionRecord(tick, send.PlayerId, bot.Value.Profile, send.CreepId, send.Quantity));
                    }
                }
            }

            TryPlaceBotTower(bot.Key, bot.Value);
            TryBuyBotTier(bot.Key, bot.Value);
            TryUpgradeBotTower(bot.Key, bot.Value);
        }

        tick = new SimulationTick(tick.Value + 1);
        if (economy.IsIncomeTick(tick))
        {
            foreach (var player in players.Players.Where(player => !player.IsEliminated))
            {
                pendingEvents.Add(new IncomeTickEvent(tick, player.PlayerId, new Gold(player.Income.Amount)));
            }
        }

        players = economy.ApplyIncomeTick(players, tick);
        // Read pre-combat rather than post-combat (result.State below) deliberately: a leaked creep is read
        // out of this snapshot when building its transfer below, and that is only safe because MoveCreeps
        // runs first inside combat.Advance and the two damage phases after it both skip HasLeaked creeps —
        // so a creep's health cannot change between here and its leak this same tick. If that phase order
        // ever changes, a transferred creep would silently carry its start-of-tick health instead of what
        // it actually had when it left the lane.
        var creepsBeforeCombat = combatState.Creeps.ToDictionary(creep => creep.EntityId, creep => creep);
        // Tiers bought since the last tick reach combat here. Rebuilt rather than mutated so a
        // snapshot handed out earlier in the tick keeps the tiers it was taken with; the creep,
        // tower and lane dictionaries are carried across by reference, so this is one small
        // allocation, not a re-index of the catalog.
        combatContent = combatContent.WithPlayerTiers(CurrentPlayerTiers());
        var result = combat.Advance(combatState, combatContent, routes, tick);
        combatState = result.State;
        foreach (var simulationEvent in result.Events)
        {
            if (simulationEvent is CreepDamagedEvent damaged)
            {
                ApplySignalGold(damaged);
            }

            if (simulationEvent is CreepKilledEvent killed && creepsBeforeCombat.TryGetValue(killed.CreepEntityId, out var killedCreep))
                players = economy.ApplyKillBounty(players, killed.DefenderId, content.Creeps.First(creep => creep.Id.Equals(killedCreep.CreepId))).Players;
            if (simulationEvent is LeakEvent leak && creepsBeforeCombat.TryGetValue(leak.CreepEntityId, out var leakedCreep))
            {
                var creep = content.Creeps.First(definition => definition.Id.Equals(leakedCreep.CreepId));
                var defenderLivesBefore = players.Get(leak.DefenderId).Lives.Amount;
                players = economy.ApplyLeak(players, leak.SenderId, leak.DefenderId, creep, leak.LivesLost).Players;
                if (defenderLivesBefore > 0 && players.Get(leak.DefenderId).Lives.Amount == 0)
                {
                    pendingEvents.Add(new PlayerEliminatedEvent(tick, leak.DefenderId));
                    WipeEliminatedLane(leak.DefenderId);
                }

                // The spent entity is removed unconditionally, not just when it transfers. Reaching a lane
                // end is not death (health carries forward per the design note in OPEN_ITEMS.md's retired 2026-07-29 review, "every lane hop leaves a permanent spent entity"),
                // but the entity that just left this lane is done regardless of whether a next lane exists
                // for it: on transfer its successor is the new entity below, and if every other seat is
                // already eliminated (nextLaneId is null) it simply has nowhere left to go. Leaving it in
                // CombatState either way makes it a tombstone: still HasLeaked, still holding the health it
                // exited with, invisible to every filter except one (the bot pressure check, item 10) that
                // forgot to exclude HasLeaked — which is what let these accumulate for a whole match.
                combatState = combatState.RemoveCreep(leak.CreepEntityId);

                var nextLaneId = NextActiveOpponentLaneId(leakedCreep.LaneId, leakedCreep.SenderId);
                if (nextLaneId is not null)
                {
                    var laneId = nextLaneId.Value;
                    var nextDefenderId = combatContent.GetLaneOwner(laneId);
                    var transferred = combat.TransferCreep(NextEntityId(), leakedCreep, laneId);
                    combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
                    pendingEvents.Add(new CreepSpawnedEvent(tick, transferred.EntityId, transferred.CreepId, transferred.SenderId, nextDefenderId));
                }
            }

            pendingEvents.Add(simulationEvent);
        }

        var summary = economy.TryCreateMatchSummary(players, tick);
        if (!matchEnded && summary is not null) { matchEnded = true; MatchSummary = summary; pendingEvents.Add(new MatchEndedEvent(tick, summary.WinnerId)); }
    }

    public VerticalSliceSnapshot GetSnapshot() =>
        new VerticalSliceSnapshot(
            tick,
            players,
            combat.GetCreepSnapshots(combatState, combatContent, routes),
            combatState.Towers,
            combat.GetTowerAimSnapshots(combatState, combatContent, routes));

    public IReadOnlyList<ISimulationEvent> DrainEvents()
    {
        var drained = pendingEvents.ToArray();
        pendingEvents.Clear();
        return drained;
    }

    public void Reset()
    {
        players = CreateStartingPlayers(topology.Players);
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        var map = content.Maps[0];
        foreach (var laneId in grids.Keys.ToArray())
        {
            grids[laneId] = new LaneGrid(map);
            routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
        }
        pendingEvents.Clear();
        tick = new SimulationTick(0);
        nextEntityId = 1;
        matchStarted = false;
        matchEnded = false;
        MatchSummary = null;
        acceptedCommands.Clear();
        botDecisionRecords.Clear();
    }

    public void StartMatch()
    {
        if (matchStarted)
        {
            return;
        }

        matchStarted = true;
        SeedExpandedLaneBotOpeners();
    }

    private static EconomyPlayerSet CreateStartingPlayers(IReadOnlyList<PlayerId> playerIds)
    {
        return new EconomyPlayerSet(playerIds
            .Select(playerId => new PlayerEconomyState(playerId, new Gold(100), new Income(10), new Lives(StartingLives))));
    }

    private Dictionary<PlayerId, BotController> CreateBots(IReadOnlyList<PlayerId> playerIds)
    {
        // The "never bot lane 1" rule this used to hardcode is now expressed by the seat: whichever
        // lane the local human occupies is excluded by IsBotEnabledFor, so lane 1 correctly becomes
        // bot-driven when the human is seated elsewhere.
        return playerIds
            .Where(playerId => options.IsBotEnabledFor(playerId))
            .ToDictionary(
                playerId => playerId,
                playerId => new BotController(
                    options.BotProfileFor(playerId),
                    options.PrimaryCreepFor(playerId) ?? content.Creeps[0].Id));
    }

    private void SeedExpandedLaneBotOpeners()
    {
        if (options.LaneCount <= 3 || bots.Count == 0)
        {
            return;
        }

        foreach (var bot in bots.OrderBy(bot => bot.Key.Value))
        {
            TryPlaceBotTower(bot.Key, bot.Value);
            var sendResult = QueueSend(bot.Key, bot.Value.PrimaryCreepId, quantity: 1);
            if (sendResult.Accepted)
            {
                botDecisionRecords.Add(new BotDecisionRecord(tick, bot.Key, bot.Value.Profile, bot.Value.PrimaryCreepId, quantity: 1));
            }
        }
    }

    /// <summary>
    /// Minimum tower count before a non-Greedy bot is allowed to send at all — a floor gate
    /// checked by <see cref="HasMinimumDefenseCoverage"/>, never a ceiling on how much a bot can
    /// build (see <see cref="TryPlaceBotTower"/>, which keeps building past this number as long as
    /// gold and candidate positions allow).
    /// </summary>
    private static int MinimumTowerCoverage(BotDecisionProfile profile) => profile switch
    {
        BotDecisionProfile.Balanced => 3,
        BotDecisionProfile.Defensive => 4,
        _ => 0
    };

    /// <summary>
    /// Greedy bots are designed to send from the start (they prioritize income, not a defensive
    /// package); Balanced and Defensive are designed to finish their opening tower package before
    /// creating any send pressure. That intent was previously enforced only indirectly, through
    /// gold-reserve thresholds tuned against specific tower costs — cheap enough towers could
    /// leave just enough spare gold to opportunistically afford a cheap creep mid-build-out. This
    /// checks the actual intent directly instead, so it holds regardless of the current cost
    /// balance.
    /// </summary>
    private bool HasMinimumDefenseCoverage(PlayerId playerId, BotController bot)
    {
        if (bot.Profile == BotDecisionProfile.Greedy)
        {
            return true;
        }

        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        return ownedTowerCount >= MinimumTowerCoverage(bot.Profile);
    }

    /// <summary>
    /// True once a non-Greedy bot's own lane is carrying enough incoming creep health that it
    /// should hold/build instead of spending gold on sends — the reactive replacement for the old
    /// fixed opening-tower-count gate, driven by actual lane threat rather than a tick schedule.
    /// Greedy is exempt: sending is its primary lever (see docs/ARCHITECTURE.md's Bot Controller
    /// section), not something pressure should suppress.
    /// </summary>
    private bool IsLaneUnderPressure(PlayerId playerId, BotController bot)
    {
        if (bot.Profile == BotDecisionProfile.Greedy)
        {
            return false;
        }

        var myLane = topology.HomeLaneFor(playerId);

        // !HasLeaked is load-bearing, not defensive tidiness. A creep that finishes a lane is not
        // despawned — it transfers to the next opponent's lane as a NEW entity, and the spent entity stays
        // in CombatState tagged with the lane it exited, still holding the health it left with. So without
        // this filter, incomingHealth counts every creep that has ever finished walking this lane and grows
        // monotonically for the whole match. Once it crosses PressureThreshold the bot stops sending and
        // never sends again.
        //
        // Every other creep filter in the codebase already excludes HasLeaked (eight sites in
        // CombatService, plus GetCreepSnapshots), which is why nothing looked wrong on screen — this was
        // the only consumer that saw the spent entities.
        //
        // Measured consequence of the fix, and it corrects an earlier diagnosis of mine: the "two mazing
        // bots stalemate forever" finding was attributed to defence out-scaling attack. It was not. With
        // this filter the same seed completes at tick 926 instead of running past 80,000. It is also the
        // real cause of the bot that sat on 3,700 gold with a frozen tower count, which I previously
        // blamed on the placement ceiling alone.
        var incomingHealth = combatState.Creeps
            .Where(creep => creep.LaneId.Equals(myLane) && !creep.IsDead && !creep.HasLeaked)
            .Sum(creep => creep.Health);
        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        return incomingHealth >= bot.PressureThreshold(content, ownedTowerCount);
    }

    /// <summary>
    /// Buys a bot the next tier it can afford, in whichever category it has already committed to.
    /// </summary>
    /// <remarks>
    /// Bots must buy tiers or the feature makes them strictly worse opponents: they maze well
    /// enough now that a tier-3 human against a tier-1 bot defence would be a walkover.
    ///
    /// Which category: whichever the bot has ALREADY invested in — the line it has built the most
    /// towers in, or the category it has sent the most creeps from. That mirrors what the tiers do
    /// (deepen a commitment rather than broaden one) and needs no new tuning knob. Ties go to the
    /// lowest index, so the choice stays deterministic for replays.
    ///
    /// Which side: a tower tier while its own lane is under pressure, a send tier otherwise. The
    /// pressure signal is the same one the send gate above uses.
    ///
    /// Spending is gated on the profile's gold reserve floor exactly as TryPlaceBotTower is, so a
    /// bot cannot upgrade itself out of being able to defend. Because this runs after
    /// TryPlaceBotTower, towers and sends both get first claim on gold and tiers are bought from
    /// what is genuinely surplus — a bot that is still building never stalls to save for a tier.
    /// </remarks>
    private void TryBuyBotTier(PlayerId playerId, BotController bot)
    {
        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return;
        }

        var kind = BotTierPreference(bot);
        var categoryIndex = kind == CategoryKind.TowerLine
            ? MostBuiltTowerLine(playerId)
            : MostSentCreepCategory(playerId);

        var targetTier = (kind == CategoryKind.TowerLine
            ? player.TowerLineTier(categoryIndex)
            : player.SendCategoryTier(categoryIndex)) + 1;
        if (targetTier > CategoryTierRules.MaxTier)
        {
            return;
        }

        // Gated on the reserve floor only, exactly like TryPlaceBotTower. An additional "must still
        // afford the next tower afterwards" term was tried and measured worse on both counts it was
        // meant to help: it did not recover the mazing it was added for (lane 2 stayed at 22 cells)
        // and it pushed match completion from 3422 ticks to 5078 by starving the creep tiers that
        // let an attack close a game out. Running after TryPlaceBotTower already gives towers first
        // claim on the tick's gold, which turns out to be the whole of the protection worth having.
        var cost = CategoryTierRules.CostFor(kind, targetTier);
        if (player.Gold.Amount - bot.GoldReserveFloor(content) < cost)
        {
            return;
        }

        BuyCategoryTier(playerId, kind, categoryIndex, targetTier);
    }

    /// <summary>
    /// Brings one of a bot's placed towers up to the line tier it has already bought.
    /// </summary>
    /// <remarks>
    /// Without this a bot buys a line tier and never realises it: the tier only reaches towers
    /// built afterwards, so a bot that has finished building would carry a tier it paid for and
    /// gets nothing from. That would make the tier a pure waste of its gold and the bot a weaker
    /// opponent than before the feature existed.
    ///
    /// Upgrades the LOWEST-tier tower first, so a bot levels its whole line evenly rather than
    /// pouring everything into one tower — and ties break on entity id so the choice stays
    /// deterministic for replays. Gated on the profile's gold reserve floor exactly as building
    /// and tier-buying are, and runs after both, so upgrades come from what is genuinely spare.
    /// </remarks>
    private void TryUpgradeBotTower(PlayerId playerId, BotController bot)
    {
        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return;
        }

        var laneId = topology.HomeLaneFor(playerId);
        var candidate = combatState.Towers
            .Where(tower => tower.OwnerId.Equals(playerId) && tower.LaneId.Equals(laneId))
            .Where(tower => tower.Tier < CategoryTierRules.MaxTier)
            .Where(tower => tower.Tier < player.TowerLineTier(
                content.Towers.First(definition => definition.Id.Equals(tower.TowerId)).CategoryIndex))
            .OrderBy(tower => tower.Tier)
            .ThenBy(tower => tower.EntityId.Value)
            .FirstOrDefault();
        if (candidate is null)
        {
            return;
        }

        var cost = CategoryTierRules.TowerUpgradeCost(
            content.Towers.First(definition => definition.Id.Equals(candidate.TowerId)).Cost.Amount);
        if (player.Gold.Amount - bot.GoldReserveFloor(content) < cost)
        {
            return;
        }

        UpgradeTower(playerId, laneId, candidate.Position);
    }

    /// <summary>
    /// Which side of the roster a bot spends its upgrade gold on, from its own authored profile.
    /// </summary>
    /// <remarks>
    /// Driven by the profile's existing Aggression and DefenseBias rather than a new heuristic, so
    /// a Greedy bot (aggression 90, bias 10) deepens its sends, a Defensive one (20/80) deepens its
    /// towers, and the choice is content-tunable alongside every other bot knob.
    ///
    /// This replaced an earlier rule of "tower tier while under pressure, send tier otherwise",
    /// which read sensibly and measured terribly: bots are under pressure most of the time, so they
    /// poured almost everything into defence, and two of them facing each other could no longer
    /// finish a match at ANY tower multiplier — the run that exposed this stalemated past 6000
    /// ticks even after tower scaling was cut to 112%. Preference turned out to dominate the
    /// multiplier completely: holding the multiplier at 115/130 and only changing which side bots
    /// buy took the same match from a stalemate to 3336 ticks, which is faster than the 3627 the
    /// game takes with no tiers at all.
    ///
    /// A tie (Balanced, 50/50) breaks toward the SEND side deliberately. Defence already compounds
    /// for free through an unbounded tower count, so the attacking side is the one that needs the
    /// help, and it is the side that lets a match end.
    /// </remarks>
    private CategoryKind BotTierPreference(BotController bot)
    {
        var profile = bot.ResolveProfile(content);
        return profile.DefenseBias > profile.Aggression ? CategoryKind.TowerLine : CategoryKind.SendCategory;
    }

    /// <summary>
    /// The tower line this player has the most towers standing in, lowest index on a tie.
    /// </summary>
    private int MostBuiltTowerLine(PlayerId playerId)
    {
        var counts = new int[PlayerEconomyState.CategoryCount];
        foreach (var tower in combatState.Towers.Where(tower => tower.OwnerId.Equals(playerId)))
        {
            var line = content.Towers.First(definition => definition.Id.Equals(tower.TowerId)).CategoryIndex;
            if (line >= 0 && line < counts.Length)
            {
                counts[line]++;
            }
        }

        return IndexOfMax(counts);
    }

    /// <summary>
    /// The send category this player has sent the most creeps from, lowest index on a tie.
    /// </summary>
    private int MostSentCreepCategory(PlayerId playerId)
    {
        var counts = new int[PlayerEconomyState.CategoryCount];
        foreach (var record in botDecisionRecords.Where(record => record.PlayerId.Equals(playerId)))
        {
            var creep = content.Creeps.FirstOrDefault(definition => definition.Id.Equals(record.ContentId));
            if (creep is not null && creep.CategoryIndex >= 0 && creep.CategoryIndex < counts.Length)
            {
                counts[creep.CategoryIndex] += record.Quantity;
            }
        }

        return IndexOfMax(counts);
    }

    private static int IndexOfMax(int[] counts)
    {
        var best = 0;
        for (var index = 1; index < counts.Length; index++)
        {
            if (counts[index] > counts[best])
            {
                best = index;
            }
        }

        return best;
    }

    private void TryPlaceBotTower(PlayerId playerId, BotController bot)
    {
        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        var towerId = BotTowerForSlot(bot.Profile, ownedTowerCount);
        var towerCost = content.Towers.First(tower => tower.Id.Equals(towerId)).Cost.Amount;
        var player = players.Get(playerId);
        if (player.Gold.Amount - bot.GoldReserveFloor(content) < towerCost)
        {
            return;
        }

        var laneId = topology.HomeLaneFor(playerId);
        var position = BestMazingPlacement(playerId, laneId, towerId, content.Towers.First(t => t.Id.Equals(towerId)).RangeCells);
        if (position is not null)
        {
            PlaceTower(playerId, laneId, towerId, position.Value);
        }
    }

    /// <summary>
    /// Picks the cell that best lengthens the creep route while still covering it — mazing.
    /// </summary>
    /// <remarks>
    /// This replaced a hardcoded list of nine positions in columns 1 and 5, chosen with no reference to
    /// the route at all. That arrangement had two consequences worth stating, because both distorted
    /// every balance measurement taken against these bots. It never mazed, so bots defended a straight
    /// lane no human would leave straight; and once those nine cells were occupied the bot could never
    /// build again, which is why a bot in an earlier probe sat on 3,700 gold with its tower count frozen
    /// at nine.
    ///
    /// Scoring is deliberately simple and explainable rather than clever:
    ///   route length gained x MazeLengthWeight   — how much longer the creeps' walk becomes
    ///   + route cells this tower covers          — how much of that walk it can actually shoot
    /// Length dominates, because a cell that adds ten steps of walking helps every tower already built,
    /// while coverage only helps this one. Cells that would block the route entirely are rejected by
    /// GridPathService before they are ever scored.
    ///
    /// Cost: one BFS per candidate cell per placement. The grid is 7x16 and a bot places a tower at most
    /// once per tick, so this is bounded and small, but it is the reason the search is a single pass over
    /// empty cells rather than a lookahead.
    /// </remarks>
    private GridPosition? BestMazingPlacement(PlayerId playerId, LaneId laneId, ContentId towerId, int rangeCells)
    {
        if (!grids.TryGetValue(laneId, out var grid) || !routes.TryGetValue(laneId, out var currentRoute))
        {
            return null;
        }

        var map = content.Maps[0];
        GridPosition? best = null;
        var bestScore = int.MinValue;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var candidate = new GridPosition(x, y);
                var validation = ValidateTowerPlacement(playerId, laneId, towerId, candidate);
                if (!validation.Result.Accepted)
                {
                    continue;
                }

                var route = validation.Placement!.Route;
                var lengthGain = route.Count - currentRoute.Count;
                var covered = 0;
                for (var index = 0; index < route.Count; index++)
                {
                    var cell = route[index];
                    if (System.Math.Abs(cell.X - candidate.X) + System.Math.Abs(cell.Y - candidate.Y) <= rangeCells)
                    {
                        covered++;
                    }
                }

                var score = lengthGain * MazeLengthWeight + covered;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// How many route cells of coverage one extra step of creep walking is worth.
    /// </summary>
    /// <remarks>
    /// Above 1 so lengthening wins ties against merely covering more. Extra route length multiplies
    /// across every tower the bot owns and every one it will build later, whereas coverage from a single
    /// placement only ever helps that placement.
    /// </remarks>
    private const int MazeLengthWeight = 4;

    // Cycled by ownedTowerCount rather than switched on a few slots with a repeating tail arm, for two
    // reasons (OPEN_ITEMS.md's retired 2026-07-29 review, "bots can only build 5 of the 15 towers"): the old shape could only ever reach 5 of the 15 towers (Arrow,
    // Control, Pulse, Prism plus whatever the tail arm was), so every mechanic added since the 15-tower
    // expansion was measured against a bot that never builds it; and its tail arm repeated a single
    // tower forever once reached (Defensive -> endless Prism, Greedy -> endless Arrow), which is why
    // BotMazingTests' "keeps building past nine towers" assertion passed on a bot spamming one tower.
    // Each profile's array is a flavour (Defensive leans control/area/support, Greedy leans cheap, both
    // fully reachable but not the only towers that profile builds), and the three arrays' union covers
    // all 15 towers, not just each profile's own list.
    // Each array's first few entries deliberately match the old hardcoded switch's early slots
    // exactly (same tower, same cost, same order) rather than reshuffling from slot 0. Income only
    // ticks every 50 simulation ticks (IncomeIntervalTicks), so gold is flat between jumps and a
    // bot's opening tower-count gate (HasMinimumDefenseCoverage) clears on whichever jump first
    // covers the cumulative cost — even a few gold of difference in an early slot can push that
    // past a 50-tick boundary and shift the observable timing by up to a full income cycle. Keeping
    // the opening identical avoids re-tuning every test that depends on early bot timing; the fix
    // for item 15 only needs the array to stop repeating forever once it reaches its old tail.
    private static readonly ContentId[] DefensiveBuildOrder =
    {
        SampleVerticalSliceContent.ControlTowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.RepairDroneTowerId,
        SampleVerticalSliceContent.ElderCanopyTowerId,
        SampleVerticalSliceContent.ThornSnareTowerId,
        SampleVerticalSliceContent.BarricadeTowerId
    };

    private static readonly ContentId[] BalancedBuildOrder =
    {
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.ControlTowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.GatlingTowerId,
        SampleVerticalSliceContent.SaplingTowerId,
        SampleVerticalSliceContent.TeslaTowerId,
        SampleVerticalSliceContent.BloomheartTowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.FoundryTowerId,
        SampleVerticalSliceContent.SporeCloudTowerId
    };

    private static readonly ContentId[] GreedyBuildOrder =
    {
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.SaplingTowerId,
        SampleVerticalSliceContent.GatlingTowerId,
        SampleVerticalSliceContent.UtilityTowerId,
        SampleVerticalSliceContent.SporeCloudTowerId
    };

    private static ContentId BotTowerForSlot(BotDecisionProfile profile, int ownedTowerCount)
    {
        var buildOrder = profile switch
        {
            BotDecisionProfile.Defensive => DefensiveBuildOrder,
            BotDecisionProfile.Balanced => BalancedBuildOrder,
            _ => GreedyBuildOrder
        };

        return buildOrder[ownedTowerCount % buildOrder.Length];
    }


    private TowerPlacementValidation ValidateTowerPlacement(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var command = new PlaceTowerCommand(playerId, tick, laneId, towerId, position);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return TowerPlacementValidation.Reject(contentResult.RejectionReason);
        }

        // PlayerId.IsValid only means "positive", so a player id outside this match still reaches
        // here and used to throw KeyNotFoundException from the economy lookup further down.
        // Reject instead: over the wire this is just a bad command, not a program error.
        if (!topology.HasPlayer(playerId))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidPlayer);
        }

        if (!grids.TryGetValue(laneId, out var grid))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidLane);
        }

        // A player may only build in their own home lane. Until now nothing enforced this: the
        // lane id was taken on trust from the caller, so PlaceTower(P1, lane 5, ...) succeeded,
        // charged P1's gold, and left a P1-owned tower defending P5's lane. That was invisible
        // locally only because the Unity client hardcodes lane 1 for the single human seat, but
        // it becomes a live exploit the moment remote clients submit their own commands (a
        // player could reshape an opponent's maze, or spend into their lane to grief the route).
        // Selling already checked ownership (see SellTowerAt's OwnerId filter); building didn't.
        if (!laneId.Equals(topology.HomeLaneFor(playerId)))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.NotOwner);
        }

        var placement = pathService.ValidatePlacement(grid, position);
        if (!placement.IsValid)
        {
            return TowerPlacementValidation.Reject(ToCommandRejection(placement.RejectionReason));
        }

        var tower = content.Towers.First(definition => definition.Id.Equals(towerId));
        var player = players.Get(playerId);
        if (player.Gold.Amount < tower.Cost.Amount)
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InsufficientGold);
        }

        return TowerPlacementValidation.Accept(grid, placement, tower, player);
    }

    private EntityId NextEntityId() => new EntityId(nextEntityId++);

    /// <summary>
    /// Pays a tower's owner for landing a hit, where the tower's content says it earns.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in CombatService so economy types stay out of combat, which is the
    /// separation the architecture keeps everywhere else.
    ///
    /// The amount is read from the tower's definition. It used to be a constant applied to any
    /// tower whose content id contained "relay", "utility" or "economy" — correct for the one tower
    /// that has ever earned, and a trap for any future tower whose id happened to contain one of
    /// those words.
    /// </remarks>
    private void ApplySignalGold(CreepDamagedEvent damaged)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate => candidate.EntityId.Equals(damaged.TowerEntityId));
        if (tower is null)
        {
            return;
        }

        var definition = content.Towers.FirstOrDefault(candidate => candidate.Id.Equals(tower.TowerId));
        if (definition is null || definition.SignalGoldPerHit <= 0)
        {
            return;
        }

        var player = players.Get(tower.OwnerId);
        if (player.IsEliminated)
        {
            return;
        }

        var amount = new Gold(definition.SignalGoldPerHit);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + amount.Amount)));
        pendingEvents.Add(new TowerEarnedGoldEvent(tick, tower.OwnerId, tower.LaneId, tower.EntityId, tower.Position, amount));
    }

    /// <summary>
    /// Clears a defeated seat's lane so the match can carry on around it.
    /// </summary>
    /// <remarks>
    /// Elimination was only ever an ECONOMY fact: an eliminated player earns no income, cannot
    /// send, and is skipped when choosing a target or the next lane after a leak. CombatService
    /// knows nothing about it, so a defeated seat's towers kept firing forever and creeps kept
    /// walking a lane whose owner had already lost — still leaking, still deducting lives from
    /// somebody on zero.
    ///
    /// Three things have to happen together, and the third is the one that is easy to miss:
    ///
    /// 1. The towers go. They belong to a player who is out.
    /// 2. The creeps in the lane go. They were attacking a seat that no longer exists.
    /// 3. The lane's GRID and ROUTE are rebuilt empty. Towers occupy cells and are what lengthens
    ///    the route, so removing them without rebuilding leaves the maze standing as an invisible
    ///    wall — creeps would keep walking the long way round obstacles that are no longer there.
    ///
    /// Creeps the eliminated player SENT are deliberately left alone. They are in other people's
    /// lanes, they were paid for, and they are somebody else's problem now.
    ///
    /// The in-flight creeps are removed rather than pushed on to the next lane. Both readings are
    /// defensible — the carousel exists precisely to move creeps onward — but "wiped" is the
    /// literal ask, and forwarding them would hand the attacker free continued pressure as a reward
    /// for the kill. Worth revisiting once it can be seen in play.
    /// </remarks>
    private void WipeEliminatedLane(PlayerId playerId)
    {
        var laneId = topology.HomeLaneFor(playerId);

        // Announced per entity, not as one bulk event, so the presentation layer can play the sell
        // and death cues it already has rather than having everything blink out in a single frame.
        // These go straight onto pendingEvents: the economy hooks that pay kill bounty and refunds
        // read combat's OWN event list, so nothing here pays out for a wipe.
        foreach (var tower in combatState.Towers.Where(tower => tower.OwnerId.Equals(playerId)).ToArray())
        {
            pendingEvents.Add(new TowerSoldEvent(tick, playerId, laneId, tower.EntityId, new Gold(0)));
        }

        foreach (var creep in combatState.Creeps.Where(creep => creep.LaneId.Equals(laneId)).ToArray())
        {
            pendingEvents.Add(new CreepKilledEvent(tick, creep.EntityId, playerId, new Gold(0)));
        }

        combatState = combatState.WipeLane(laneId, playerId);

        var map = content.Maps[0];
        grids[laneId] = new LaneGrid(map);
        routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
    }

    private LaneId? NextActiveOpponentLaneId(LaneId currentLaneId, PlayerId senderId)
    {
        return topology.NextActiveOpponentLaneAfterLeak(
            currentLaneId,
            senderId,
            defenderId => !players.Get(defenderId).IsEliminated);
    }

    private static CommandRejectionReason ToCommandRejection(PlacementRejectionReason reason)
    {
        return reason switch
        {
            PlacementRejectionReason.OutsideGrid => CommandRejectionReason.InvalidLane,
            PlacementRejectionReason.AlreadyOccupied => CommandRejectionReason.CellOccupied,
            PlacementRejectionReason.PathBlocked => CommandRejectionReason.PathBlocked,
            _ => CommandRejectionReason.PathBlocked
        };
    }


    private sealed class TowerPlacementValidation
    {
        private TowerPlacementValidation(
            VerticalSliceCommandResult result,
            LaneGrid? grid,
            PlacementValidationResult? placement,
            TowerDefinition? tower,
            PlayerEconomyState? player)
        {
            Result = result;
            Grid = grid;
            Placement = placement;
            Tower = tower;
            Player = player;
        }

        public VerticalSliceCommandResult Result { get; }

        public LaneGrid? Grid { get; }

        public PlacementValidationResult? Placement { get; }

        public TowerDefinition? Tower { get; }

        public PlayerEconomyState? Player { get; }

        public static TowerPlacementValidation Accept(LaneGrid grid, PlacementValidationResult placement, TowerDefinition tower, PlayerEconomyState player) =>
            new TowerPlacementValidation(VerticalSliceCommandResult.Accept(), grid, placement, tower, player);

        public static TowerPlacementValidation Reject(CommandRejectionReason reason) =>
            new TowerPlacementValidation(VerticalSliceCommandResult.Reject(reason), null, null, null, null);
    }
}
