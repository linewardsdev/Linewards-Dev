using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// The Passive profile builds like a bot and attacks like nobody — the guided practice table.
/// </summary>
/// <remarks>
/// The practice match wants the neighbouring lanes to look alive so the player has a maze and a
/// leak gate to watch, without any creep ever arriving that the player did not send themselves.
/// Both halves are asserted, because a profile that only did the second would leave seven empty
/// lanes around the player, and one that only did the first is just Defensive.
/// </remarks>
public sealed class BotPassiveProfileTests
{
    private readonly ITestOutputHelper output;

    public BotPassiveProfileTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void Passive_bot_builds_a_maze_and_never_sends()
    {
        var seat = new PlayerId(2);
        var options = LocalMatchOptions.Default.WithLane(seat.Value, profile: BotDecisionProfile.Passive);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        // The expanded-lane opening turn runs inside the constructor, so its events are already
        // pending before the first advance; drain from here so the count includes them.
        var placed = 0;
        var sent = 0;
        var sendTiers = 0;
        void Tally()
        {
            foreach (var simulationEvent in slice.DrainEvents())
            {
                switch (simulationEvent)
                {
                    case TowerPlacedEvent tower when tower.PlayerId.Equals(seat):
                        placed++;
                        break;
                    case CreepQueuedEvent send when send.SenderId.Equals(seat):
                        sent++;
                        break;
                    case CategoryTierPurchasedEvent tier when tier.PlayerId.Equals(seat) && tier.CategoryKind == CategoryKind.SendCategory:
                        sendTiers++;
                        break;
                }
            }
        }

        Tally();
        for (var tick = 0; tick < 600; tick++)
        {
            slice.AdvanceOneTick();
            Tally();
        }

        var snapshot = slice.GetSnapshot();
        var standing = snapshot.Towers.Count(tower => tower.OwnerId.Equals(seat));
        var player = snapshot.Players.Players.Single(candidate => candidate.PlayerId.Equals(seat));
        output.WriteLine($"P2 placed {placed} towers ({standing} standing), sent {sent}, bought {sendTiers} send tiers, gold {player.Gold.Amount}");

        Assert.True(placed >= 2, $"a Passive bot placed only {placed} tower(s) in 600 ticks; the practice lane's neighbours should still maze");
        Assert.Equal(0, sent);
        Assert.Equal(0, sendTiers);
        Assert.All(Enumerable.Range(0, PlayerEconomyState.CategoryCount), category => Assert.Equal(1, player.SendCategoryTier(category)));
        Assert.Contains(slice.GetBotDiagnostics().Profiles, profile => profile.PlayerId.Equals(seat) && profile.Profile == BotDecisionProfile.Passive);
        Assert.DoesNotContain(slice.GetBotDiagnostics().RecentDecisions, decision => decision.PlayerId.Equals(seat));
    }

    /// <summary>
    /// The public decision entry point refuses too, not just the match-driven path.
    /// </summary>
    [Fact]
    public void Passive_bot_decides_nothing_even_with_gold_to_spare()
    {
        var content = SampleVerticalSliceContent.Create();
        var bot = new BotController(BotDecisionProfile.Passive, SampleVerticalSliceContent.CreepId);
        var rich = new PlayerEconomyState(new PlayerId(2), new Gold(1000), new Income(50), new Lives(10));

        Assert.Null(bot.Decide(rich, content, new SimulationTick(100)).Command);
    }

    [Fact]
    public void WithAllBotsPassive_maps_every_non_local_seat_and_leaves_the_original_alone()
    {
        var original = new LocalMatchOptions(seed: 7, laneCount: 8, localPlayerId: 3)
            .WithLane(5, primaryCreepId: SampleVerticalSliceContent.SwarmCreepId);
        var practice = original.WithAllBotsPassive();

        Assert.NotSame(original, practice);
        Assert.Equal(original.Seed, practice.Seed);
        Assert.Equal(original.LaneCount, practice.LaneCount);
        Assert.Equal(original.LocalPlayerId, practice.LocalPlayerId);

        foreach (var playerId in Enumerable.Range(1, practice.LaneCount).Select(value => new PlayerId(value)))
        {
            if (playerId.Equals(practice.LocalPlayerId))
            {
                Assert.False(practice.IsBotEnabledFor(playerId));
                continue;
            }

            Assert.True(practice.IsBotEnabledFor(playerId));
            Assert.Equal(BotDecisionProfile.Passive, practice.BotProfileFor(playerId));
            Assert.Equal(original.PrimaryCreepFor(playerId), practice.PrimaryCreepFor(playerId));
        }

        // The original still reads as the shipped table: the default profile mix, untouched.
        Assert.Equal(BotDecisionProfile.Balanced, original.BotProfileFor(new PlayerId(2)));
        Assert.Equal(BotDecisionProfile.Defensive, original.BotProfileFor(new PlayerId(3)));
        Assert.Equal(BotDecisionProfile.Greedy, original.BotProfileFor(new PlayerId(4)));
        Assert.Equal(SampleVerticalSliceContent.SwarmCreepId, original.PrimaryCreepFor(new PlayerId(5)));
    }

    /// <summary>
    /// The lookup contract the Unity client codes against.
    /// </summary>
    [Fact]
    public void Passive_profile_id_round_trips_through_BotProfileIds()
    {
        Assert.Equal("bot.passive", BotProfileIds.Passive.Value);
        Assert.Equal(BotProfileIds.Passive, BotProfileIds.For(BotDecisionProfile.Passive));
    }
}
