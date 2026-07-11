using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class EconomyPlayerSet
{
    private readonly PlayerEconomyState[] players;

    public EconomyPlayerSet(IEnumerable<PlayerEconomyState> players)
    {
        this.players = players?.ToArray() ?? throw new ArgumentNullException(nameof(players));
        if (this.players.Length < 2)
        {
            throw new ArgumentException("At least two players are required.", nameof(players));
        }

        if (this.players.GroupBy(player => player.PlayerId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Player IDs must be unique.", nameof(players));
        }
    }

    public IReadOnlyList<PlayerEconomyState> Players => players.ToArray();

    public PlayerEconomyState Get(PlayerId playerId)
    {
        foreach (var player in players)
        {
            if (player.PlayerId.Equals(playerId))
            {
                return player;
            }
        }

        throw new KeyNotFoundException($"Unknown player '{playerId}'.");
    }

    public EconomyPlayerSet Replace(PlayerEconomyState state)
    {
        var next = players.ToArray();
        for (var index = 0; index < next.Length; index++)
        {
            if (next[index].PlayerId.Equals(state.PlayerId))
            {
                next[index] = state;
                return new EconomyPlayerSet(next);
            }
        }

        throw new KeyNotFoundException($"Unknown player '{state.PlayerId}'.");
    }

    public PlayerId GetCarouselTarget(PlayerId senderId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (!players[index].PlayerId.Equals(senderId))
            {
                continue;
            }

            for (var offset = 1; offset <= players.Length; offset++)
            {
                var candidate = players[(index + offset) % players.Length];
                if (!candidate.IsEliminated)
                {
                    return candidate.PlayerId;
                }
            }
        }

        throw new InvalidOperationException($"No active carousel target for sender '{senderId}'.");
    }

    public IReadOnlyList<PlayerEconomyState> ActivePlayers =>
        players.Where(player => !player.IsEliminated).ToArray();
}
