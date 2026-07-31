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

        if (command is QueueSendCommand queueSend)
        {
            if (!queueSend.CreepId.IsValid)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidContentId);
            }

            if (queueSend.Quantity <= 0)
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
