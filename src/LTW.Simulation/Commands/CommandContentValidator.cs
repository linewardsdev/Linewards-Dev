using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;

namespace LTW.Simulation.Commands;

public sealed class CommandContentValidator
{
    public CommandResult Validate(ISimulationCommand command, ContentCatalog content)
    {
        if (!command.PlayerId.IsValid)
        {
            return CommandResult.Reject(CommandRejectionReason.InvalidPlayer);
        }

        if (command is PlaceTowerCommand placeTower)
        {
            if (!placeTower.LaneId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidLane);
            }

            if (!placeTower.TowerId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (!content.Towers.Any(tower => tower.Id.Equals(placeTower.TowerId)))
            {
                return CommandResult.Reject(CommandRejectionReason.UnknownTower);
            }
        }

        // Same content checks as a direct send, minus quantity, which an enqueue does not carry.
        // Routed through the validator rather than checked at the bridge so a server rejects a bad
        // enqueue exactly as it rejects a bad send, with the same reason codes.
        if (command is EnqueueSendCommand enqueue)
        {
            if (!enqueue.CreepId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (!content.Creeps.Any(creep => creep.Id.Equals(enqueue.CreepId)))
            {
                return CommandResult.Reject(CommandRejectionReason.UnknownCreep);
            }
        }

        // A cancel names a creep, so it takes the same content checks as the enqueue it undoes.
        // Whether anything of that creep is actually queued is a STATE question, not a content one,
        // and belongs at the bridge where the queue lives — this only answers "is that a real creep".
        if (command is CancelQueuedSendCommand cancel)
        {
            if (!cancel.CreepId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (!content.Creeps.Any(creep => creep.Id.Equals(cancel.CreepId)))
            {
                return CommandResult.Reject(CommandRejectionReason.UnknownCreep);
            }
        }

        if (command is QueueSendCommand queueSend)
        {
            if (!queueSend.CreepId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            // Upper bound, not just the existing lower one: an unchecked quantity reaches
            // EconomyService.SendCostFor's unchecked int multiply and LocalVerticalSlice's
            // Enumerable.Range(0, quantity) spawn loop untouched. A large-enough value overflows
            // the cost calculation to something small or negative (letting the send through nearly
            // free, or crediting gold) and/or exhausts memory trying to spawn that many creeps —
            // see docs/SECURITY_AUDIT_2026-09-05.md's C1. Capped at the same depth a seat's queue
            // itself enforces one entry at a time (LocalVerticalSlice.MaxQueuedSendsPerCreep) —
            // nothing legitimate ever asks for more in one call.
            if (queueSend.Quantity <= 0 || queueSend.Quantity > Bridge.LocalVerticalSlice.MaxQueuedSendsPerCreep)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidQuantity);
            }

            if (!content.Creeps.Any(creep => creep.Id.Equals(queueSend.CreepId)))
            {
                return CommandResult.Reject(CommandRejectionReason.UnknownCreep);
            }
        }

        if (command is BuyTechCommand buyTech)
        {
            if (!buyTech.TechId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (!content.Techs.Any(tech => tech.Id.Equals(buyTech.TechId)))
            {
                return CommandResult.Reject(CommandRejectionReason.UnknownTech);
            }
        }

        // Shape only. Whether this player can AFFORD the tier, and whether it is the next one in
        // order, both need the player's current state, which this validator does not receive —
        // those checks live in LocalVerticalSlice.BuyCategoryTier alongside the gold deduction.
        if (command is BuyCategoryTierCommand buyTier)
        {
            if (buyTier.CategoryIndex < 0 || buyTier.CategoryIndex >= PlayerEconomyState.CategoryCount)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (buyTier.TargetTier <= PlayerEconomyState.BaseTier || buyTier.TargetTier > CategoryTierRules.MaxTier)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidTier);
            }
        }

        if (command is SellTowerCommand sellTower && !sellTower.TowerEntityId.IsValid)
        {
            return CommandResult.Reject(CommandRejectionReason.InvalidEntity);
        }

        return CommandResult.Accept();
    }
}
