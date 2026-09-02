using LTW.Simulation.Primitives;

namespace LTW.Simulation.Authority;

/// <summary>
/// Bounds how often a seat may ask for something, as opposed to how much it may have.
/// </summary>
/// <remarks>
/// The distinction matters and is easy to collapse. The send queue caps ten of a creep per seat —
/// that bounds queue DEPTH. It says nothing about a client sending ten thousand enqueue requests a
/// second, each of which runs content validation and is refused. Depth is a game rule; request rate
/// is an abuse control, and `ARCHITECTURE.md` puts abuse controls server-side.
///
/// Explicitly NOT the send cooldown. <c>EconomyRules.SendCooldownTicks</c> is a game rule about how
/// often a seat may attack. Queueing is not attacking, and making a player wait to queue would undo
/// most of why the queue exists — the whole point is stating intent freely and letting the economy
/// meter it. This limits messages, not moves.
/// </remarks>
public interface ICommandRateLimiter
{
    /// <summary>
    /// Whether <paramref name="playerId"/> may issue one more request at <paramref name="tick"/>,
    /// consuming budget if so.
    /// </summary>
    bool TryConsume(PlayerId playerId, SimulationTick tick);

    /// <summary>Clears every seat's budget, for a rematch that reuses this instance.</summary>
    void Reset();
}

/// <summary>
/// A per-seat token bucket measured in simulation ticks.
/// </summary>
/// <remarks>
/// Ticks rather than wall-clock, so the limit means the same thing in a batch playtest running at
/// 300x as it does on a phone at 4 ticks a second. A wall-clock limiter would silently throttle the
/// harness and let a real client through, which is the wrong way round for something whose job is
/// to be tested.
///
/// The defaults are sized against the largest legitimate burst the game can produce, which is
/// larger than it first looks: the roster is about fifteen creeps and the queue holds ten of each,
/// so a player filling every card issues roughly 150 requests and every one of them is honest. A
/// first attempt at 30 would have throttled exactly that player, which is the failure this comment
/// warns about — a limiter a real player can feel is mistuned.
///
/// 240 burst with two tokens back per tick is a sustained eight requests a second at the shipped
/// tick rate, well past a human thumb and well short of a script. It stops abuse and cannot shape
/// play.
/// </remarks>
public sealed class TokenBucketRateLimiter : ICommandRateLimiter
{
    private readonly System.Collections.Generic.Dictionary<int, (double Tokens, long LastTick)> buckets = new();
    private readonly double burst;
    private readonly double tokensPerTick;

    public TokenBucketRateLimiter(double burst = 240d, double tokensPerTick = 2d)
    {
        this.burst = burst;
        this.tokensPerTick = tokensPerTick;
    }

    public bool TryConsume(PlayerId playerId, SimulationTick tick)
    {
        if (!buckets.TryGetValue(playerId.Value, out var bucket))
        {
            bucket = (burst, tick.Value);
        }

        // Refilled from elapsed ticks rather than on a timer, so a seat that was idle for a hundred
        // ticks arrives with a full bucket and one that has been hammering does not.
        var elapsed = tick.Value - bucket.LastTick;
        var tokens = System.Math.Min(burst, bucket.Tokens + (elapsed * tokensPerTick));
        if (tokens < 1d)
        {
            buckets[playerId.Value] = (tokens, tick.Value);
            return false;
        }

        buckets[playerId.Value] = (tokens - 1d, tick.Value);
        return true;
    }

    /// <summary>
    /// Clears every seat's bucket.
    /// </summary>
    /// <remarks>
    /// Found from the same "reset doesn't reset correctly" report class as
    /// <c>LocalVerticalSlice.Reset</c>'s <c>sendQueues</c>/<c>eliminatedAtTick</c> comment, and for
    /// the identical reason: this keys off <c>PlayerId</c> rather than living inside the player set,
    /// so resetting <c>players</c> does nothing to it. Here the failure was worse than a stray
    /// carry-over value — <c>LastTick</c> is an ABSOLUTE tick, and a rematch resets the tick counter
    /// back to 0 without recreating this limiter (readonly field, same instance reused). The first
    /// enqueue of the new match then computed <c>elapsed = 0 - <old LastTick></c>, a large negative
    /// number, driving the refill calculation deeply negative and rejecting every send with
    /// <c>CooldownActive</c> for thousands of ticks — reported live as "send cooldown bug on
    /// replay," and indistinguishable in play from a stuck cooldown despite this limiter explicitly
    /// not being one (see the class remarks above).
    /// </remarks>
    public void Reset() => buckets.Clear();
}
