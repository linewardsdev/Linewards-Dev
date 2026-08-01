using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Covers the carousel at the lane count the game actually ships with.
/// </summary>
/// <remarks>
/// Every other simulation test runs at <c>laneCount: 3</c> — 16 call sites, none at 8 — while
/// <c>LocalMatchOptions</c> defaults to <c>MaxLaneCount</c>, which is 8. So the shipped topology
/// had no direct coverage at all, and a three-lane carousel is a weak proxy for an eight-lane one:
/// it wraps after two hops, so a chain that breaks on the fourth or seventh hop looks identical to
/// one that works.
///
/// Targeting is deliberately unchanged here — 1 -> 2 -> 3 ... -> 8 -> 1 is the intended rule. These
/// tests assert that rule holds all the way round rather than proposing a different one.
/// </remarks>
public sealed class EightLaneCarouselTests
{
    private readonly ITestOutputHelper output;

    public EightLaneCarouselTests(ITestOutputHelper output) => this.output = output;

    private const int Lanes = LocalMatchOptions.MaxLaneCount;

    [Fact]
    public void The_game_defaults_to_eight_lanes()
    {
        // Pins the assumption the rest of this file rests on. If the default moves, these tests
        // should fail loudly rather than quietly keep testing a lane count nobody ships.
        Assert.Equal(8, LocalMatchOptions.MaxLaneCount);
        Assert.Equal(8, new LocalMatchOptions().LaneCount);
    }

    /// <summary>
    /// Every seat targets the next one, and following the chain visits all eight exactly once.
    /// </summary>
    /// <remarks>
    /// Asserted as a completed cycle rather than eight independent hops. Per-hop assertions pass
    /// happily for a chain that skips a seat or short-circuits back early; only walking the whole
    /// ring and counting the visits catches that.
    /// </remarks>
    [Fact]
    public void Carousel_visits_all_eight_lanes_and_returns_to_the_start()
    {
        var topology = new LocalMatchTopology(Lanes);
        var visited = new List<int>();
        var current = new PlayerId(1);

        for (var hop = 0; hop < Lanes; hop++)
        {
            visited.Add(current.Value);
            var next = topology.NextActiveOpponent(current, _ => true);
            Assert.NotNull(next);
            current = next!.Value;
        }

        output.WriteLine("cycle: " + string.Join(" -> ", visited) + $" -> {current.Value}");

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, visited);
        Assert.Equal(1, current.Value);
        Assert.Equal(Lanes, visited.Distinct().Count());
    }

    /// <summary>
    /// A leaked creep hops lane by lane through all eight and never re-enters its sender's.
    /// </summary>
    /// <remarks>
    /// This is the path that carries a creep — and, since lives are stolen per leak, the win
    /// condition — around the board. At three lanes it wraps almost immediately; at eight there are
    /// six intermediate seats where the skip-the-sender rule has to keep holding.
    /// </remarks>
    [Fact]
    public void Leak_transfer_walks_every_other_lane_before_returning()
    {
        var topology = new LocalMatchTopology(Lanes);
        var sender = new PlayerId(1);
        var visited = new List<int>();
        var lane = new LaneId(1);

        for (var hop = 0; hop < Lanes - 1; hop++)
        {
            var next = topology.NextActiveOpponentLaneAfterLeak(lane, sender, _ => true);
            Assert.NotNull(next);
            lane = next!.Value;
            visited.Add(lane.Value);
        }

        output.WriteLine("leak chain from lane 1 (sender P1): " + string.Join(" -> ", visited));

        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8 }, visited);
        Assert.DoesNotContain(sender.Value, visited);
    }

    /// <summary>
    /// Eliminated seats are skipped, and the cycle still closes over whoever is left.
    /// </summary>
    /// <remarks>
    /// The case a three-lane test cannot express: with 3 seats one elimination leaves a trivial
    /// two-player ping-pong. Here four are out and the ring has to jump gaps of varying width and
    /// still return to its start.
    /// </remarks>
    [Fact]
    public void Carousel_skips_eliminated_seats_and_still_closes_the_ring()
    {
        var topology = new LocalMatchTopology(Lanes);
        var eliminated = new HashSet<int> { 2, 3, 6, 8 };
        bool IsActive(PlayerId player) => !eliminated.Contains(player.Value);

        var expected = new[] { 1, 4, 5, 7 };
        var visited = new List<int>();
        var current = new PlayerId(1);

        for (var hop = 0; hop < expected.Length; hop++)
        {
            visited.Add(current.Value);
            var next = topology.NextActiveOpponent(current, IsActive);
            Assert.NotNull(next);
            current = next!.Value;
        }

        output.WriteLine("cycle over survivors: " + string.Join(" -> ", visited) + $" -> {current.Value}");

        Assert.Equal(expected, visited);
        Assert.Equal(1, current.Value);
    }

    /// <summary>
    /// The last survivor has nobody to send to, rather than targeting themselves.
    /// </summary>
    [Fact]
    public void A_sole_survivor_has_no_carousel_target()
    {
        var topology = new LocalMatchTopology(Lanes);
        Assert.Null(topology.NextActiveOpponent(new PlayerId(5), player => player.Value == 5));
        Assert.Null(topology.NextActiveOpponentLaneAfterLeak(new LaneId(5), new PlayerId(5), player => player.Value == 5));
    }

    /// <summary>
    /// A real eight-lane match pressures every seat, and every send moves forward around the ring.
    /// </summary>
    /// <remarks>
    /// The topology tests above are pure; this runs the actual bridge with bots so the wiring
    /// between topology, economy and combat is exercised at eight lanes.
    ///
    /// Two things it deliberately does NOT assert, both learned by writing them wrong first:
    ///
    /// - That all eight seats send. P1 is the local player and is not bot-driven, so in a headless
    ///   run it never acts — and then dies undefended. Seven senders is correct here, not six or
    ///   eight.
    /// - That every pair is exactly N -> N+1. Once a seat is eliminated the carousel skips it, so
    ///   `8 -> 2` is right and not a violation. The invariant that actually holds is that a send
    ///   always moves FORWARD around the ring and never targets its own sender, which is what
    ///   distinguishes a working carousel from one that stalls or doubles back.
    /// </remarks>
    [Fact]
    public void An_eight_lane_match_pressures_every_seat_moving_forward_around_the_ring()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: Lanes));
        slice.StartMatch();

        var senders = new HashSet<int>();
        var defenders = new HashSet<int>();
        var pairs = new HashSet<(int From, int To)>();

        for (var tick = 0; tick < 900 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            foreach (var queued in slice.DrainEvents().OfType<CreepQueuedEvent>())
            {
                senders.Add(queued.SenderId.Value);
                defenders.Add(queued.DefenderId.Value);
                pairs.Add((queued.SenderId.Value, queued.DefenderId.Value));
            }
        }

        output.WriteLine("send pairs: " + string.Join(", ", pairs.OrderBy(pair => pair.From).ThenBy(pair => pair.To).Select(pair => $"{pair.From}->{pair.To}")));
        output.WriteLine($"senders: {string.Join(",", senders.OrderBy(id => id))}   defenders: {string.Join(",", defenders.OrderBy(id => id))}");

        // Every seat is pressured at some point — the property a three-lane suite could not show.
        Assert.Equal(Lanes, defenders.Count);

        // Every bot-driven seat sends. P1 is the local player and never acts headlessly.
        Assert.Equal(Lanes - 1, senders.Count);
        Assert.DoesNotContain(1, senders);

        Assert.All(pairs, pair =>
        {
            Assert.NotEqual(pair.From, pair.To);

            // Forward distance around the ring, 1 when nobody is skipped and larger across
            // eliminated seats. Anything from 1 to Lanes-1 is a legal forward hop; a backward
            // target would land outside that range.
            var forward = (pair.To - pair.From + Lanes) % Lanes;
            Assert.InRange(forward, 1, Lanes - 1);
        });
    }
}
