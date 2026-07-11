namespace LTW.Simulation.Pathing;

public enum PlacementRejectionReason
{
    None = 0,
    OutsideGrid,
    SpawnCell,
    ExitCell,
    NotWalkable,
    AlreadyOccupied,
    PathBlocked
}
