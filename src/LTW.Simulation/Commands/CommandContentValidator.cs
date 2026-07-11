using System.Linq;
using LTW.Simulation.Content;

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

        if (command is SellTowerCommand sellTower && !sellTower.TowerEntityId.IsValid)
        {
            return CommandResult.Reject(CommandRejectionReason.InvalidEntity);
        }

        return CommandResult.Accept();
    }
}
