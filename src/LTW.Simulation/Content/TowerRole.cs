namespace LTW.Simulation.Content;

/// <summary>
/// What problem a tower is for, so a build plan can ask for a job rather than name a model.
/// </summary>
/// <remarks>
/// Bot build orders used to be lists of tower ids, which tied a profile to specific towers and so
/// to the lines those towers happen to belong to. That is why a forced category pick broke the
/// bots outright: `Balanced` opened Arrow (Arcane), Gatling (Foundry), Sapling (Grove), so a seat
/// locked to one line had everything after its first tower rejected and stopped building.
///
/// Naming the job instead makes one order work for every line — each line fills the same shape from
/// its own roster — and it surfaces a gap as a gap: a line with nothing to answer <see cref="Brake"/>
/// is visibly missing an answer to speed, which is exactly the hole Arcane turned out to have and
/// which took a human playtest to notice.
/// </remarks>
public enum TowerRole
{
    /// <summary>Single-target damage. Every line has one; it is the fallback when a line cannot fill a role.</summary>
    Dps,

    /// <summary>Hits more than one creep: splash, chains, clouds.</summary>
    Aoe,

    /// <summary>Slows creeps walking through its range.</summary>
    Brake,

    /// <summary>Earns gold from its own fire.</summary>
    Economy,

    /// <summary>Helps other towers or the lane rather than attacking well itself.</summary>
    Support,

    /// <summary>Cheap enough that its job is occupying a cell.</summary>
    Wall
}
