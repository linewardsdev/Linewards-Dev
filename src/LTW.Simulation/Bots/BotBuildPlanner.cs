using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bots;

/// <summary>
/// What a bot builds next, and where it puts it.
/// </summary>
/// <remarks>
/// Stateless on purpose: both halves are pure functions of the profile's authored build order and
/// the board as the context reports it, so a placement can be reasoned about — and reproduced —
/// without a bot instance or a match. That matters here more than usual, because all-bot matches
/// are fully deterministic and several tests pin exact outcomes to them.
/// </remarks>
public static class BotBuildPlanner
{
    /// <summary>
    /// How many route cells of coverage one extra step of creep walking is worth.
    /// </summary>
    /// <remarks>
    /// Above 1 so lengthening wins ties against merely covering more. Extra route length multiplies
    /// across every tower the bot owns and every one it will build later, whereas coverage from a
    /// single placement only ever helps that placement.
    /// </remarks>
    public const int MazeLengthWeight = 4;

    /// <summary>
    /// The tower this profile builds at this point in its build-out, or null if it does not build.
    /// </summary>
    /// <remarks>
    /// The list cycles rather than running out, so a bot keeps building for as long as gold and
    /// legal cells last — see <see cref="BotProfileDefinition.BuildOrder"/> for why that shape
    /// replaced a switch with a repeating tail arm.
    ///
    /// An empty build order returns null rather than falling back to some default tower. A catalog
    /// that authors no build order for a profile has said its bots do not build, and inventing a
    /// tower for them would be the simulation making a balance decision the content declined to.
    /// </remarks>
    public static ContentId? NextTower(BotProfileDefinition profile, int ownedTowerCount)
    {
        var buildOrder = profile.BuildOrder;
        if (buildOrder.Count == 0)
        {
            return null;
        }

        return buildOrder[ownedTowerCount % buildOrder.Count];
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
    ///
    /// The scan is row-major from (0,0) and ties go to the first cell reached — <c>score &lt;= bestScore</c>
    /// keeps the incumbent — which is what makes the choice reproducible rather than merely optimal.
    /// </remarks>
    public static GridPosition? BestMazingPlacement(
        IBotMatchContext match,
        PlayerId playerId,
        LaneId laneId,
        ContentId towerId,
        int rangeCells)
    {
        var currentRoute = match.RouteFor(laneId);
        if (currentRoute.Count == 0)
        {
            return null;
        }

        var map = match.Content.Maps[0];
        GridPosition? best = null;
        var bestScore = int.MinValue;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var candidate = new GridPosition(x, y);
                var probe = match.ProbePlacement(playerId, laneId, towerId, candidate);
                if (!probe.Accepted)
                {
                    continue;
                }

                var route = probe.Route;
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
}
