using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.Simulation.Seats;

/// <summary>
/// Drives one seat by replaying another player's recorded commands, tick by tick, instead of
/// deciding anything itself. MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-02: a real opponent with no
/// server and no network.
/// </summary>
/// <remarks>
/// Reissues through <see cref="IBotMatchContext"/> — the exact same narrow surface a
/// <c>BotController</c> acts through — rather than calling <c>LocalVerticalSlice</c> directly, so
/// a recorded seat can never do anything a live bot could not already do to this match.
///
/// A recorded command's own <see cref="RecordedCommand.LaneId"/> is NOT reused: it names the
/// SOURCE player's home lane in the match the recording came from, which is meaningless (and
/// often plain wrong — a different seat number) in a new match. <see cref="GridPosition"/> IS
/// reused as recorded: lane grids are lane-relative, not lane-specific — confirmed directly by
/// <c>GraphicsAuditCaptureRunner.PlaceLineups</c>, which places towers at the same (x, y) pairs
/// across three different lanes. So this driver re-homes the lane through
/// <see cref="IBotMatchContext.HomeLaneFor"/> for the TARGET seat and leaves positions alone.
/// </remarks>
public sealed class RecordedSeatDriver
{
    private readonly IReadOnlyList<RecordedCommand> commands;
    private int next;

    /// <summary>
    /// Filters <paramref name="recording"/> down to one source seat's commands, sorted the same
    /// way <c>LocalVerticalSlice.Replay</c> sorts its own — by <see cref="RecordedCommand.Tick"/>,
    /// then <see cref="RecordedCommand.Sequence"/> — so a recording built from any match, not only
    /// one this process just played, drives correctly.
    /// </summary>
    public RecordedSeatDriver(MatchReplayRecord recording, PlayerId sourcePlayerId, string displayName)
    {
        commands = recording.Commands
            .Where(command => command.PlayerId.Equals(sourcePlayerId))
            .OrderBy(command => command.Tick.Value)
            .ThenBy(command => command.Sequence)
            .ToArray();
        DisplayName = displayName;
    }

    /// <summary>What a client shows for this ghost — see <see cref="Seat.DisplayName"/>.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Reissues every command this seat's source recorded at <paramref name="currentTick"/>,
    /// re-homed onto <paramref name="targetPlayerId"/>.
    /// </summary>
    /// <returns>
    /// False once every recorded command has been played — the caller's signal
    /// (<c>LocalVerticalSlice.AdvanceOneTick</c>) to retire this driver and bot-fill the seat from
    /// here, per MP-02's "a recorded seat whose stream ends early is bot-filled from that tick".
    /// True while there is still recorded play left, even on a tick with nothing to reissue.
    /// </returns>
    public bool TakeTurn(PlayerId targetPlayerId, SimulationTick currentTick, IBotMatchContext match)
    {
        while (next < commands.Count && commands[next].Tick.Equals(currentTick))
        {
            Apply(commands[next], targetPlayerId, match);
            next++;
        }

        return next < commands.Count;
    }

    private static void Apply(RecordedCommand command, PlayerId targetPlayerId, IBotMatchContext match)
    {
        switch (command.Kind)
        {
            case RecordedCommandKind.PlaceTower:
                match.TryPlaceTower(targetPlayerId, match.HomeLaneFor(targetPlayerId), command.ContentId!.Value, command.Position!.Value);
                break;
            case RecordedCommandKind.SellTower:
                match.TrySellTower(targetPlayerId, match.HomeLaneFor(targetPlayerId), command.Position!.Value);
                break;
            case RecordedCommandKind.UpgradeTower:
                match.TryUpgradeTower(targetPlayerId, match.HomeLaneFor(targetPlayerId), command.Position!.Value);
                break;
            case RecordedCommandKind.BuyCategoryTier:
                match.TryBuyCategoryTier(targetPlayerId, command.Category!.Value, command.CategoryIndex!.Value, command.TargetTier!.Value);
                break;
            case RecordedCommandKind.Send:
                match.TrySend(targetPlayerId, command.ContentId!.Value, command.Quantity!.Value);
                break;
        }

        // Deliberately not checked for acceptance, unlike LocalVerticalSlice.Apply's replay path.
        // That method replays a match against ITSELF and treats a rejection as a determinism
        // defect worth throwing on. This one replays a DIFFERENT match's play into a new one,
        // where the board, gold and neighbours can genuinely differ — a rejected command here
        // just means the ghost's original move no longer applies, which is expected, not a bug.
    }
}
