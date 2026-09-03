using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Covers the brake on the send/income feedback loop, and the escalation that had to land with it.
/// </summary>
/// <remarks>
/// Income and gold were a closed positive loop: a send permanently raised income, income paid gold
/// every 50 ticks, gold bought more sends. Nothing bounded it, so an eight-lane match reached income
/// 16,048 on 81,112 banked gold with 4,111 creeps alive at once — a mobile performance problem
/// before it is a balance one, and it made every balance measurement a measurement taken inside a
/// runaway.
///
/// The escalation tests live in this file rather than their own because the two changes are only
/// correct together: unbounded income was the game's de-facto closing mechanism, so capping it
/// without adding a real one turns a decided match into a five-hour stalemate.
/// </remarks>
public sealed class IncomeCeilingTests
{
    private readonly ITestOutputHelper output;

    public IncomeCeilingTests(ITestOutputHelper output) => this.output = output;

    private static EconomyService Service(int ceiling = 600, int taperStart = 300) =>
        new(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 0, sellRefundPercent: 50, leakLifeLoss: 1, incomeCeiling: ceiling, incomeTaperStart: taperStart));

    private static CreepDefinition Creep(int incomeGain) =>
        SampleVerticalSliceContent.Create().Creeps.First(definition => definition.IncomeGain.Amount == incomeGain);

    /// <summary>
    /// Below the knee a send grants exactly its authored income, unchanged.
    /// </summary>
    /// <remarks>
    /// The whole point of the knee. A taper that scaled from zero income was tried first and is what
    /// this pins against: it halved a gain-2 creep at income 100, which is inside the opening, and
    /// measurably weakened the bots — their maze fell from 22 cells to 20 in BotMazingTests purely
    /// because they had less gold to build with.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(299)]
    [InlineData(300)]
    public void Below_the_knee_a_send_grants_its_full_authored_income(int income)
    {
        var service = Service();
        var creep = Creep(2);

        Assert.Equal(2, service.IncomeGainFor(new Income(income), creep, quantity: 1));
        Assert.Equal(6, service.IncomeGainFor(new Income(income), creep, quantity: 3));
    }

    /// <summary>
    /// Across the band the gain falls off, and it reaches zero exactly at the ceiling.
    /// </summary>
    /// <remarks>
    /// Reaching zero is the property that matters, not the shape. A floor on the gain does not fix
    /// the runaway — any strictly positive gain keeps compounding, because the number of sends per
    /// interval grows with gold. Flooring at 1 only delays it: modelled income 423 at tick 3,200 but
    /// 2,374 by tick 4,800.
    /// </remarks>
    [Fact]
    public void The_gain_falls_off_across_the_band_and_is_zero_at_the_ceiling()
    {
        var service = Service();
        var creep = Creep(2);

        var atKnee = service.IncomeGainFor(new Income(300), creep, quantity: 3);
        var midBand = service.IncomeGainFor(new Income(450), creep, quantity: 3);
        var nearTop = service.IncomeGainFor(new Income(590), creep, quantity: 3);

        output.WriteLine($"gain for 3x a gain-2 creep: knee={atKnee} mid={midBand} near-top={nearTop}");

        Assert.Equal(6, atKnee);
        Assert.Equal(3, midBand);
        Assert.True(nearTop < midBand, $"the taper is not still falling near the top ({nearTop} vs {midBand})");

        Assert.Equal(0, service.IncomeGainFor(new Income(600), creep, quantity: 3));
        Assert.Equal(0, service.IncomeGainFor(new Income(9_999), creep, quantity: 3));
    }

    /// <summary>
    /// The cheapest creeps keep earning their full 1 across the whole band.
    /// </summary>
    /// <remarks>
    /// This is why the taper rounds up rather than down. Rounding down zeroes a gain-1 creep at half
    /// the band and quietly deletes the low end of the roster — the exact failure the stat rebalance
    /// immediately before this change was fixing, so reintroducing it here as a rounding artefact
    /// would be a poor trade.
    /// </remarks>
    [Fact]
    public void A_gain_one_creep_is_never_rounded_down_to_worthless()
    {
        var service = Service();
        var creep = Creep(1);

        for (var income = 0; income < 600; income += 25)
        {
            Assert.True(service.IncomeGainFor(new Income(income), creep, quantity: 1) >= 1,
                $"a gain-1 creep granted nothing at income {income}, so the low end of the roster is dead there");
        }
    }

    /// <summary>
    /// Repeated sends converge on the ceiling and never pass it, however large each one is.
    /// </summary>
    /// <remarks>
    /// Asserted as convergence rather than a single clamped send, because a single send cannot reach
    /// the ceiling by design — at income 595 a 20x gain-5 send is tapered from a nominal 100 down to
    /// 2. Writing this as "one big send lands on 600" tested the clamp in QueueSend while asserting
    /// something the taper makes unreachable.
    /// </remarks>
    [Fact]
    public void Repeated_sends_converge_on_the_ceiling_and_never_exceed_it()
    {
        var service = Service();
        var creep = Creep(5);
        var players = new EconomyPlayerSet(new[]
        {
            new PlayerEconomyState(new PlayerId(1), new Gold(1_000_000), new Income(10), new Lives(220)),
            new PlayerEconomyState(new PlayerId(2), new Gold(1_000_000), new Income(10), new Lives(220)),
        });

        // Convergence is geometric — each send closes about a third of the remaining headroom — so
        // this settles in roughly twenty. The rest are there to prove it STAYS at the ceiling rather
        // than creeping past it, and the count is kept low enough that the gold reserve above
        // actually covers every send; running dry rejects the send and fails on the wrong assertion.
        for (var send = 0; send < 100; send++)
        {
            var result = service.QueueSend(players, new PlayerId(1), creep, quantity: 20, new SimulationTick(10), new PlayerId(2));
            Assert.True(result.Accepted);
            players = result.Players;
            Assert.True(players.Get(new PlayerId(1)).Income.Amount <= 600, "income passed the ceiling");
        }

        Assert.Equal(600, players.Get(new PlayerId(1)).Income.Amount);
    }

    /// <summary>
    /// Escalation leaves a normal match alone and only ramps once one has overstayed.
    /// </summary>
    [Theory]
    // Restated for StartTick 700 and 80% per 100 ticks (2026-08-19). Still the same three
    // questions: inert before the start, one interval after it, and a long way in.
    [InlineData(0, 100)]
    [InlineData(600, 100)]
    [InlineData(700, 100)]
    [InlineData(800, 180)]
    [InlineData(3_000, 1_940)]
    public void Escalation_is_inert_until_its_start_tick(long tick, int expected)
    {
        Assert.Equal(expected, MatchEscalationRules.CreepHealthPercentFor(tick));
    }

    /// <summary>
    /// A real eight-lane match no longer runs away, and still ends.
    /// </summary>
    /// <remarks>
    /// The regression guard the rest of this file exists to support, asserted on the two numbers that
    /// actually hurt: income and how many creeps are alive at once. Measured before this change on
    /// this same seed: income 16,048, 4,111 peak creeps, completing at tick 3,132.
    ///
    /// The bounds are deliberately loose. This runs the whole simulation, so pinning exact values
    /// would make it fail on any unrelated balance edit — the property under test is "bounded", not
    /// "bounded at precisely this number".
    /// </remarks>
    [Fact]
    public void An_eight_lane_match_stays_bounded_and_still_finishes()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 8));
        slice.StartMatch();

        var peakCreeps = 0;
        for (var tick = 0; tick < 8_000 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            peakCreeps = System.Math.Max(peakCreeps, slice.GetSnapshot().Creeps.Count);
        }

        var peakIncome = slice.GetSnapshot().Players.Players.Max(player => player.Income.Amount);
        output.WriteLine($"completed at {slice.MatchSummary?.CompletedAtTick.Value}, peak creeps {peakCreeps}, peak income {peakIncome}");

        Assert.NotNull(slice.MatchSummary);
        // Against the constant, not a literal. The ceiling moved from 600 to 900 when category tier
        // costs began escalating, and a hardcoded bound fails on the change rather than on the
        // property — the property being that income stops at whatever the ceiling is, which is what
        // keeps creep counts survivable.
        Assert.True(
            peakIncome <= EconomyRules.DefaultIncomeCeiling,
            $"income reached {peakIncome} against a ceiling of {EconomyRules.DefaultIncomeCeiling}, so the ceiling is not holding");
        Assert.True(peakCreeps < 1_500, $"{peakCreeps} creeps were alive at once (was 4,111 before the ceiling); this is a device performance problem");
    }
}
