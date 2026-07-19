using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

/// <summary>
/// Owns the local vertical-slice lane topology. The current playtest layout is one
/// defender per lane: player 1 owns lane 1, player 2 owns lane 2, and so on.
/// Keeping that mapping here avoids spreading the player-id/lane-id convention
/// across economy, combat, bots, and Unity presentation code.
/// </summary>
public sealed class LocalMatchTopology
{
    private readonly PlayerId[] players;
    private readonly IReadOnlyDictionary<LaneId, PlayerId> laneOwners;

    public LocalMatchTopology(int laneCount)
    {
        LaneCount = Math.Clamp(laneCount, LocalMatchOptions.MinLaneCount, LocalMatchOptions.MaxLaneCount);
        players = Enumerable.Range(1, LaneCount).Select(playerId => new PlayerId(playerId)).ToArray();
        laneOwners = players.ToDictionary(HomeLaneFor, playerId => playerId);
    }

    public int LaneCount { get; }

    public IReadOnlyList<PlayerId> Players => players.ToArray();

    public IReadOnlyList<LaneId> Lanes => players.Select(HomeLaneFor).ToArray();

    public IReadOnlyDictionary<LaneId, PlayerId> LaneOwners => laneOwners;

    public LaneId HomeLaneFor(PlayerId playerId)
    {
        EnsurePlayer(playerId);
        return new LaneId(playerId.Value);
    }

    public PlayerId OwnerFor(LaneId laneId)
    {
        if (laneOwners.TryGetValue(laneId, out var owner))
        {
            return owner;
        }

        throw new KeyNotFoundException($"Unknown lane '{laneId}'.");
    }

    public PlayerId? NextActiveOpponent(PlayerId senderId, Func<PlayerId, bool> isActive)
    {
        EnsurePlayer(senderId);
        if (isActive is null)
        {
            throw new ArgumentNullException(nameof(isActive));
        }

        var senderIndex = senderId.Value - 1;
        for (var offset = 1; offset <= players.Length; offset++)
        {
            var candidate = players[(senderIndex + offset) % players.Length];
            if (!candidate.Equals(senderId) && isActive(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public LaneId? NextActiveOpponentLaneAfterLeak(LaneId currentLaneId, PlayerId senderId, Func<PlayerId, bool> isActive)
    {
        EnsureLane(currentLaneId);
        EnsurePlayer(senderId);
        if (isActive is null)
        {
            throw new ArgumentNullException(nameof(isActive));
        }

        for (var offset = 1; offset <= players.Length; offset++)
        {
            var laneId = new LaneId((currentLaneId.Value - 1 + offset) % players.Length + 1);
            var defenderId = OwnerFor(laneId);
            if (!defenderId.Equals(senderId) && isActive(defenderId))
            {
                return laneId;
            }
        }

        return null;
    }

    private void EnsurePlayer(PlayerId playerId)
    {
        if (playerId.Value < 1 || playerId.Value > LaneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId), playerId, $"Player must be between 1 and {LaneCount}.");
        }
    }

    private void EnsureLane(LaneId laneId)
    {
        if (laneId.Value < 1 || laneId.Value > LaneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(laneId), laneId, $"Lane must be between 1 and {LaneCount}.");
        }
    }
}
