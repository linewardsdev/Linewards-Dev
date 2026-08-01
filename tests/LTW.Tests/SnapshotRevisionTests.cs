using System;
using System.Collections.Generic;
using System.Text;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

/// <summary>
/// Guards <see cref="LocalVerticalSlice.StateRevision"/>, the signal the Unity client gates its
/// per-frame snapshot rebuild on (OPEN_ITEMS item 36).
/// </summary>
/// <remarks>
/// The property that matters is one-directional, and these tests are written around that asymmetry.
/// The client rebuilds when the revision moved, so a revision that moves too often costs a rebuild
/// nobody needed — wasteful, invisible. A revision that fails to move when the board did leaves the
/// client showing a stale board, which is the defect this whole change could introduce. So
/// <see cref="Revision_never_holds_still_while_the_snapshot_changes"/> is the real guard and
/// everything else is a named example of a case it covers.
/// </remarks>
public sealed class SnapshotRevisionTests
{
    /// <summary>
    /// The case a tick counter gets wrong, which is why the signal is not a tick counter.
    /// </summary>
    /// <remarks>
    /// The client opens a match with a thirty-second build countdown in which the simulation does
    /// not tick at all while the player builds and upgrades. Every command below is accepted and
    /// changes the board with <c>Tick</c> frozen at zero, so a gate reading the tick would show none
    /// of them until the match started — a tower built during the countdown would simply not appear.
    /// </remarks>
    [Fact]
    public void Commands_accepted_between_ticks_move_the_revision()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var player = simulation.LocalPlayerId;
        var lane = simulation.LocalPlayerLaneId;
        simulation.GrantLocalPlaytestGold(player, new Gold(100_000));
        var tickBefore = simulation.GetSnapshot().Tick;
        var line = TowerLineIndex(simulation, SampleVerticalSliceContent.TowerId);

        AssertMoves(simulation, "place", () => simulation.PlaceTower(player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(1, 3)).Accepted);
        // Buying the line's tier has to come first — a tower can only be raised as far as its line
        // has been paid for. That ordering is the reason this reads as a sequence rather than a
        // parameterised set: it is the sequence a player performs during the countdown.
        AssertMoves(simulation, "buy tower tier", () => simulation.BuyCategoryTier(player, CategoryKind.TowerLine, line, 2).Accepted);
        AssertMoves(simulation, "upgrade", () => simulation.UpgradeTower(player, lane, new GridPosition(1, 3)).Accepted);
        AssertMoves(simulation, "buy send tier", () => simulation.BuyCategoryTier(player, CategoryKind.SendCategory, 0, 2).Accepted);
        AssertMoves(simulation, "send", () => simulation.QueueSend(player, SampleVerticalSliceContent.CreepId).Accepted);
        AssertMoves(simulation, "sell", () => simulation.SellTowerAt(player, lane, new GridPosition(1, 3)).Accepted);

        // The whole point: not one tick passed for any of it.
        Assert.Equal(tickBefore, simulation.GetSnapshot().Tick);
    }

    /// <summary>Advancing time is a change too, even on an empty board with no bots.</summary>
    [Fact]
    public void A_tick_moves_the_revision()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var before = simulation.StateRevision;

        simulation.AdvanceOneTick();

        Assert.NotEqual(before, simulation.StateRevision);
    }

    /// <summary>
    /// Reading the simulation must not count as changing it, or the gate never closes.
    /// </summary>
    /// <remarks>
    /// This is the half that makes the optimisation an optimisation. If any of these reads moved the
    /// revision, the client would rebuild on every frame exactly as it did before and the change
    /// would be pure overhead — passing its other tests the whole time.
    /// </remarks>
    [Fact]
    public void Reading_the_simulation_does_not_move_the_revision()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        for (var index = 0; index < 40; index++)
        {
            simulation.AdvanceOneTick();
        }

        simulation.DrainEvents();
        var before = simulation.StateRevision;

        for (var index = 0; index < 10; index++)
        {
            simulation.GetSnapshot();
            simulation.GetReplayRecord();
            simulation.GetBotDiagnostics();
            simulation.DrainEvents();
            simulation.PreviewPlaceTower(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, SampleVerticalSliceContent.TowerId, new GridPosition(1, 3));
            simulation.QuoteTowerLineUpgrade(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, 0);
            simulation.RouteLength(simulation.LocalPlayerLaneId);
        }

        Assert.Equal(before, simulation.StateRevision);
    }

    /// <summary>
    /// A rejected command changes nothing, so it must not claim to.
    /// </summary>
    /// <remarks>
    /// Not a correctness requirement — an extra rebuild is only waste — but it is worth pinning,
    /// because a rejection path that mutated first and validated second would show up here and
    /// nowhere else.
    /// </remarks>
    [Fact]
    public void A_rejected_command_leaves_the_revision_alone()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var before = simulation.StateRevision;

        Assert.False(simulation.PlaceTower(new PlayerId(1), new LaneId(5), SampleVerticalSliceContent.TowerId, new GridPosition(1, 3)).Accepted);
        Assert.False(simulation.UpgradeTower(new PlayerId(1), new LaneId(1), new GridPosition(4, 4)).Accepted);
        Assert.False(simulation.SellTowerAt(new PlayerId(1), new LaneId(1), new GridPosition(4, 4)).Accepted);

        Assert.Equal(before, simulation.StateRevision);
    }

    /// <summary>
    /// A reset is a change, and must not rewind the counter into a value already handed out.
    /// </summary>
    /// <remarks>
    /// If the revision restarted at zero on reset, a client that had been showing the old match's
    /// opening board would compare the new match's opening revision against a value it already held
    /// and conclude there was nothing to draw.
    /// </remarks>
    [Fact]
    public void Reset_moves_the_revision_forward_rather_than_back()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        for (var index = 0; index < 30; index++)
        {
            simulation.AdvanceOneTick();
        }

        var before = simulation.StateRevision;
        simulation.Reset();

        Assert.True(simulation.StateRevision > before);
    }

    /// <summary>
    /// The guard the client's freshness actually rests on: across a real match, the snapshot is
    /// never observed to change while the revision holds still.
    /// </summary>
    /// <remarks>
    /// Written as an exhaustive comparison rather than a set of examples because the failure this
    /// protects against is a mutator nobody thought to test — the driver skips
    /// <c>GetSnapshot</c> whenever this number is unchanged, so any state the number misses becomes
    /// a board that silently lags. It walks a seed-1 eight-lane bot match (which sends, builds,
    /// mazes, kills, leaks and eliminates on its own), and interleaves human commands at ticks where
    /// none would otherwise land, so the between-ticks case is exercised inside a running match and
    /// not only on a still board. Every observation compares a digest of the ENTIRE snapshot surface
    /// against the revision at that instant.
    ///
    /// The converse — revision moved, digest did not — is deliberately allowed and does happen: gold
    /// ticking up changes a seat without changing any tower. That direction only costs a rebuild.
    /// </remarks>
    [Fact]
    public void Revision_never_holds_still_while_the_snapshot_changes()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        var player = simulation.LocalPlayerId;
        var lane = simulation.LocalPlayerLaneId;
        simulation.StartMatch();

        var lastRevision = simulation.StateRevision;
        var lastDigest = SnapshotDigest(simulation.GetSnapshot());
        var observations = 0;
        var revisionHolds = 0;

        void Observe(string what)
        {
            var revision = simulation.StateRevision;
            var digest = SnapshotDigest(simulation.GetSnapshot());
            observations++;
            if (revision == lastRevision)
            {
                revisionHolds++;
                Assert.True(
                    digest == lastDigest,
                    $"The snapshot changed after {what} while StateRevision held at {revision}. " +
                    "Something the snapshot reports is mutating outside the four writes the revision counts, " +
                    "and the Unity client will show a stale board for it.");
            }

            lastRevision = revision;
            lastDigest = digest;
        }

        var placed = new List<GridPosition>();
        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();
            Observe("a tick");

            // Reading between ticks must be inert, and this is where that gets proven inside a live
            // match rather than on a still board.
            Observe("reading the snapshot again");
            simulation.DrainEvents();
            Observe("draining events");

            if (tick % 40 != 0)
            {
                continue;
            }

            // Commands, applied with the tick frozen: the opening-countdown case, mid-match.
            simulation.GrantLocalPlaytestGold(player, new Gold(5_000));
            Observe("a gold grant");

            var cell = new GridPosition(1 + (tick / 40) % 5, 2 + (tick / 40) % 9);
            if (simulation.PlaceTower(player, lane, SampleVerticalSliceContent.TowerId, cell).Accepted)
            {
                placed.Add(cell);
            }

            Observe("a placement");

            simulation.UpgradeTower(player, lane, cell);
            Observe("an upgrade");

            simulation.BuyCategoryTier(player, CategoryKind.TowerLine, 0, 1 + (tick / 40) % 4);
            Observe("a tower tier");

            simulation.ClearLocalPlaytestSendCooldown(player);
            simulation.QueueSend(player, SampleVerticalSliceContent.CreepId, 2);
            Observe("a send");

            // The one mutation that touches creeps and towers and NOTHING else. Every other command
            // here also moves a seat's gold, so without this the combat half of the signal would be
            // covered only incidentally, by the seat change riding alongside it.
            simulation.CreateLocalPlaytestDamagedTransferCreep(SampleVerticalSliceContent.CreepId, 7);
            Observe("a transfer creep appearing");

            if (placed.Count > 2)
            {
                simulation.SellTowerAt(player, lane, placed[0]);
                placed.RemoveAt(0);
                Observe("a sale");
            }
        }

        // The test is worthless if the interesting case never came up: a run where the revision moved
        // on every single observation would assert nothing at all.
        Assert.True(observations > 1_000, $"only {observations} observations");
        Assert.True(revisionHolds > 100, $"the revision only held still {revisionHolds} times in {observations} observations, so the staleness case was barely exercised");
    }

    /// <summary>The upgrade line a tower belongs to, read from content rather than assumed.</summary>
    private static int TowerLineIndex(LocalVerticalSlice simulation, ContentId towerId)
    {
        foreach (var tower in simulation.Content.Towers)
        {
            if (tower.Id.Equals(towerId))
            {
                return tower.CategoryIndex;
            }
        }

        throw new InvalidOperationException($"{towerId.Value} is not in the catalog.");
    }

    private static void AssertMoves(LocalVerticalSlice simulation, string what, Func<bool> command)
    {
        var before = simulation.StateRevision;
        Assert.True(command(), $"{what} was rejected, so this test is not measuring what it claims");
        Assert.True(simulation.StateRevision != before, $"{what} changed the board without moving StateRevision");
    }

    /// <summary>Everything <see cref="VerticalSliceSnapshot"/> exposes, as one comparable string.</summary>
    private static string SnapshotDigest(VerticalSliceSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append('t').Append(snapshot.Tick.Value);

        foreach (var player in snapshot.Players.Players)
        {
            builder.Append("|p").Append(player.PlayerId.Value)
                .Append(':').Append(player.Gold.Amount)
                .Append(':').Append(player.Income.Amount)
                .Append(':').Append(player.Lives.Amount)
                .Append(':').Append(player.NextSendAvailableTick.Value)
                .Append(':').Append(player.IsEliminated);
            for (var index = 0; index < PlayerEconomyState.CategoryCount; index++)
            {
                builder.Append(':').Append(player.TowerLineTier(index)).Append('/').Append(player.SendCategoryTier(index));
            }
        }

        foreach (var tower in snapshot.Towers)
        {
            builder.Append("|w").Append(tower.EntityId.Value)
                .Append(':').Append(tower.TowerId.Value)
                .Append(':').Append(tower.OwnerId.Value)
                .Append(':').Append(tower.LaneId.Value)
                .Append(':').Append(tower.Position.X).Append(',').Append(tower.Position.Y)
                .Append(':').Append(tower.NextAttackTick.Value)
                .Append(':').Append(tower.Tier)
                .Append(':').Append(tower.ShellImpactTick?.Value ?? -1)
                .Append(':').Append(tower.ShellImpactCell?.X ?? -1).Append(',').Append(tower.ShellImpactCell?.Y ?? -1);
        }

        foreach (var creep in snapshot.Creeps)
        {
            builder.Append("|c").Append(creep.EntityId.Value)
                .Append(':').Append(creep.CreepId.Value)
                .Append(':').Append(creep.SenderId.Value)
                .Append(':').Append(creep.LaneId.Value)
                .Append(':').Append(creep.Position.X).Append(',').Append(creep.Position.Y)
                .Append(':').Append(creep.NextPosition.X).Append(',').Append(creep.NextPosition.Y)
                .Append(':').Append(creep.Health)
                .Append(':').Append(creep.MaxHealth)
                .Append(':').Append(creep.SpeedPerSecond)
                .Append(':').Append(creep.MovementProgress)
                .Append(':').Append(creep.MovementCost);
        }

        foreach (var aim in snapshot.TowerAimTargets)
        {
            builder.Append("|a").Append(aim.TowerEntityId.Value)
                .Append(':').Append(aim.TargetPosition.X).Append(',').Append(aim.TargetPosition.Y);
        }

        return builder.ToString();
    }
}
