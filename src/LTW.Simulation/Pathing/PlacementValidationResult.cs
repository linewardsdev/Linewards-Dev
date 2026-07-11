using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

public sealed class PlacementValidationResult
{
    private PlacementValidationResult(bool isValid, PlacementRejectionReason rejectionReason, IReadOnlyList<GridPosition> route)
    {
        IsValid = isValid;
        RejectionReason = rejectionReason;
        Route = route;
    }

    public bool IsValid { get; }

    public PlacementRejectionReason RejectionReason { get; }

    public IReadOnlyList<GridPosition> Route { get; }

    public static PlacementValidationResult Valid(IReadOnlyList<GridPosition> route) =>
        new PlacementValidationResult(true, PlacementRejectionReason.None, route);

    public static PlacementValidationResult Reject(PlacementRejectionReason rejectionReason) =>
        new PlacementValidationResult(false, rejectionReason, System.Array.Empty<GridPosition>());
}
