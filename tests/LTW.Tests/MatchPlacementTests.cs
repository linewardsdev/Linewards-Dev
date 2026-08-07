using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// That the end-of-match summary records where each seat finished.
/// </summary>
/// <remarks>
/// The results screen rendered every defeated seat as `OUT` and nothing else, so a seven-way loss
/// read as a seven-way tie. The ordering was known while the match ran and thrown away at the end:
/// <c>TryCreateMatchSummary</c> sees only the final player set, where every eliminated seat looks
/// identical — same zero lives, no record of when it got there.
///
/// Reverse elimination order is the ranking the mode implies. Surviving longer is the objective, so
/// the last seat to fall placed second.
/// </remarks>
public sealed class MatchPlacementTests
{
    private readonly ITestOutputHelper output;

    public MatchPlacementTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void A_finished_match_ranks_every_seat_from_the_winner_down()
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(seed: 1, laneCount: LocalMatchOptions.MaxLaneCount));
        slice.StartMatch();

        for (var tick = 0; tick < 12_000 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
        }

        Assert.NotNull(slice.MatchSummary);
        var summary = slice.MatchSummary!;
        foreach (var player in summary.Players.OrderBy(p => p.Placement))
        {
            output.WriteLine($"  {player.Placement}: P{player.PlayerId.Value} lives {player.Lives.Amount} eliminated {player.IsEliminated}");
        }

        // The winner is first, and only the winner.
        var winner = summary.Players.Single(p => p.PlayerId.Equals(summary.WinnerId));
        Assert.Equal(1, winner.Placement);
        Assert.Single(summary.Players, p => p.Placement == 1);

        // Every seat is ranked, and the ranks are exactly 1..N with no gaps or repeats. A duplicate
        // is the failure this is really guarding: two seats sharing a placement is the same "it
        // reads as a tie" defect in a different disguise.
        var placements = summary.Players.Select(p => p.Placement).OrderBy(v => v).ToArray();
        Assert.Equal(Enumerable.Range(1, summary.Players.Count).ToArray(), placements);
    }

    [Fact]
    public void Surviving_longer_places_higher()
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(seed: 1, laneCount: LocalMatchOptions.MaxLaneCount));
        slice.StartMatch();

        // Recorded as the match runs, because the summary is the thing under test — deriving the
        // expected order from the same summary would assert it against itself.
        var eliminatedAt = new System.Collections.Generic.Dictionary<int, int>();
        for (var tick = 0; tick < 12_000 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            foreach (var player in slice.GetSnapshot().Players.Players)
            {
                if (player.IsEliminated && !eliminatedAt.ContainsKey(player.PlayerId.Value))
                {
                    eliminatedAt[player.PlayerId.Value] = tick;
                }
            }
        }

        var summary = slice.MatchSummary!;
        var losers = summary.Players
            .Where(p => !p.PlayerId.Equals(summary.WinnerId))
            .OrderBy(p => p.Placement)
            .ToArray();

        for (var i = 1; i < losers.Length; i++)
        {
            var better = losers[i - 1];
            var worse = losers[i];
            output.WriteLine($"  {better.Placement} P{better.PlayerId.Value} fell at {eliminatedAt[better.PlayerId.Value]}, {worse.Placement} P{worse.PlayerId.Value} fell at {eliminatedAt[worse.PlayerId.Value]}");
            Assert.True(
                eliminatedAt[better.PlayerId.Value] > eliminatedAt[worse.PlayerId.Value],
                $"P{better.PlayerId.Value} placed above P{worse.PlayerId.Value} but fell earlier");
        }
    }

    /// <summary>A summary built without elimination order reports no placement rather than a wrong one.</summary>
    [Fact]
    public void An_unranked_summary_reports_zero_rather_than_guessing()
    {
        var summary = new LTW.Simulation.Economy.PlayerEconomySummary(
            new PlayerId(3),
            new Gold(10),
            new Income(5),
            new Lives(0),
            isEliminated: true);

        Assert.Equal(0, summary.Placement);
    }
}
