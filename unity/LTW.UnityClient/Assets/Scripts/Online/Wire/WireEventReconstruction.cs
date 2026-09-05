#nullable enable

using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Newtonsoft.Json.Linq;

namespace LTW.UnityClient.Online.Wire
{
    /// <summary>
    /// Turns one wire <see cref="EventDto"/> back into the real <c>LTW.Simulation.Events</c> object
    /// it started as, so a wire-backed match can feed <c>UnitySimulationDriver.LatestEvents</c> the
    /// same way local play always has.
    /// </summary>
    /// <remarks>
    /// The server never built a hand-written flat DTO per event kind — <c>EventDto.Data</c> is the
    /// sender's own concrete type, serialized generically via its runtime type (see
    /// LTW.MatchServer's <c>EventDto</c> own remarks for why: a hand-maintained mirror is a second
    /// copy of a shape that already exists). That means every event's full field data is already on
    /// the wire today, nested exactly the way each `LTW.Simulation.Primitives` value type serializes
    /// itself (<c>PlayerId</c>/<c>LaneId</c>/<c>EntityId</c>/<c>SimulationTick</c> as
    /// <c>{"value": N}</c>, <c>ContentId</c> as <c>{"value": "..."}</c>, <c>GridPosition</c> as
    /// <c>{"x": N, "y": N}</c>, <c>Gold</c>/<c>Lives</c> as <c>{"amount": N}</c>) — this only had to
    /// parse that shape back out, not change what the server sends.
    ///
    /// Only the event kinds that actually drive presentation (audio cues, tower recoil/attack VFX —
    /// see <c>UnityVerticalSliceRenderer.RenderEvents</c>) are handled; every other kind (bot/replay
    /// telemetry like <c>CommandRejectedEvent</c>, or presentation this pass did not reach like
    /// <c>CreepHealedEvent</c>/<c>TowerEarnedGoldEvent</c>) returns null and is dropped, the same as
    /// it was before this existed — a real but smaller remaining gap, not a regression.
    /// </remarks>
    internal static class WireEventReconstruction
    {
        internal static ISimulationEvent? TryBuild(EventDto dto)
        {
            if (dto.Data is not JObject data)
            {
                return null;
            }

            return dto.Kind switch
            {
                nameof(TowerPlacedEvent) => new TowerPlacedEvent(
                    Tick(data, "tick"), PlayerId(data, "playerId"), LaneId(data, "laneId"),
                    EntityId(data, "towerEntityId"), ContentId(data, "towerId"), GridPosition(data, "position")),

                nameof(TowerSoldEvent) => new TowerSoldEvent(
                    Tick(data, "tick"), PlayerId(data, "playerId"), LaneId(data, "laneId"),
                    EntityId(data, "towerEntityId"), Gold(data, "refund")),

                nameof(CategoryTierPurchasedEvent) => new CategoryTierPurchasedEvent(
                    Tick(data, "tick"), PlayerId(data, "playerId"), (CategoryKind)Int(data, "categoryKind"),
                    Int(data, "categoryIndex"), Int(data, "tier"), Gold(data, "cost")),

                nameof(TowerUpgradedEvent) => new TowerUpgradedEvent(
                    Tick(data, "tick"), PlayerId(data, "playerId"), LaneId(data, "laneId"),
                    EntityId(data, "towerEntityId"), ContentId(data, "towerId"), GridPosition(data, "position"),
                    Int(data, "tier"), Gold(data, "cost")),

                nameof(TowerFiredEvent) => new TowerFiredEvent(
                    Tick(data, "tick"), LaneId(data, "laneId"), EntityId(data, "towerEntityId"),
                    GridPosition(data, "towerPosition"), EntityId(data, "targetCreepEntityId"),
                    GridPosition(data, "targetPosition"), Tick(data, "impactTick"), GridPosition(data, "impactPosition")),

                nameof(CreepDamagedEvent) => new CreepDamagedEvent(
                    Tick(data, "tick"), PlayerId(data, "defenderId"), LaneId(data, "laneId"),
                    EntityId(data, "towerEntityId"), GridPosition(data, "towerPosition"),
                    EntityId(data, "creepEntityId"), Int(data, "damageDealt")),

                nameof(CreepKilledEvent) => new CreepKilledEvent(
                    Tick(data, "tick"), EntityId(data, "creepEntityId"), PlayerId(data, "defenderId"), Gold(data, "bountyAwarded")),

                nameof(IncomeTickEvent) => new IncomeTickEvent(
                    Tick(data, "tick"), PlayerId(data, "playerId"), Gold(data, "goldAwarded")),

                nameof(LeakEvent) => new LeakEvent(
                    Tick(data, "tick"), PlayerId(data, "senderId"), PlayerId(data, "defenderId"),
                    EntityId(data, "creepEntityId"), Lives(data, "livesLost"), Gold(data, "bountyAwarded")),

                nameof(PlayerEliminatedEvent) => new PlayerEliminatedEvent(Tick(data, "tick"), PlayerId(data, "playerId")),

                nameof(MatchEndedEvent) => new MatchEndedEvent(Tick(data, "tick"), PlayerId(data, "winnerId")),

                _ => null,
            };
        }

        private static int Int(JObject data, string field) => data[field]!.Value<int>();

        private static LTW.Simulation.Primitives.SimulationTick Tick(JObject data, string field) =>
            new(data[field]!["value"]!.Value<long>());

        private static PlayerId PlayerId(JObject data, string field) =>
            new(data[field]!["value"]!.Value<int>());

        private static LaneId LaneId(JObject data, string field) =>
            new(data[field]!["value"]!.Value<int>());

        private static EntityId EntityId(JObject data, string field) =>
            new(data[field]!["value"]!.Value<long>());

        private static ContentId ContentId(JObject data, string field) =>
            new(data[field]!["value"]!.Value<string>()!);

        private static LTW.Simulation.Primitives.Gold Gold(JObject data, string field) =>
            new(data[field]!["amount"]!.Value<int>());

        private static LTW.Simulation.Primitives.Lives Lives(JObject data, string field) =>
            new(data[field]!["amount"]!.Value<int>());

        private static LTW.Simulation.Primitives.GridPosition GridPosition(JObject data, string field)
        {
            var value = data[field]!;
            return new LTW.Simulation.Primitives.GridPosition(value["x"]!.Value<int>(), value["y"]!.Value<int>());
        }
    }
}
