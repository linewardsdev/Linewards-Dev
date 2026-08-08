using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Content;

public sealed class ContentValidationResult
{
    private ContentValidationResult(IReadOnlyList<string> errors)
    {
        // Copies rather than storing the caller's list as-is (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items) — the same
        // defensive-copy convention used elsewhere for exactly this reason.
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static ContentValidationResult Valid { get; } = new ContentValidationResult(new string[0]);

    public static ContentValidationResult Invalid(IReadOnlyList<string> errors) => new ContentValidationResult(errors);
}

public sealed class ContentValidator
{
    public ContentValidationResult Validate(ContentCatalog catalog)
    {
        var errors = new List<string>();

        AddDuplicateIdErrors(errors, "tower", catalog.Towers.Select(tower => tower.Id));
        AddDuplicateIdErrors(errors, "creep", catalog.Creeps.Select(creep => creep.Id));
        AddDuplicateIdErrors(errors, "tech", catalog.Techs.Select(tech => tech.Id));
        AddDuplicateIdErrors(errors, "map", catalog.Maps.Select(map => map.Id));
        AddDuplicateIdErrors(errors, "bot profile", catalog.BotProfiles.Select(profile => profile.Id));

        foreach (var tower in catalog.Towers)
        {
            if (tower.Cost.Amount == 0)
            {
                errors.Add($"Tower '{tower.Id}' must have a positive cost.");
            }

            if (tower.RangeCells <= 0)
            {
                errors.Add($"Tower '{tower.Id}' must have positive range.");
            }

            if (tower.Damage <= 0)
            {
                errors.Add($"Tower '{tower.Id}' must have positive damage.");
            }

            if (tower.AttackCooldownTicks <= 0)
            {
                errors.Add($"Tower '{tower.Id}' must have a positive attack cooldown.");
            }
        }

        foreach (var creep in catalog.Creeps)
        {
            if (creep.Cost.Amount == 0)
            {
                errors.Add($"Creep '{creep.Id}' must have a positive cost.");
            }

            if (creep.MaxHealth <= 0)
            {
                errors.Add($"Creep '{creep.Id}' must have positive health.");
            }

            if (creep.SpeedPerSecond <= 0)
            {
                errors.Add($"Creep '{creep.Id}' must have positive speed.");
            }
        }

        var towerIds = new HashSet<ContentId>(catalog.Towers.Select(tower => tower.Id));
        var creepIds = new HashSet<ContentId>(catalog.Creeps.Select(creep => creep.Id));

        foreach (var tech in catalog.Techs)
        {
            if (tech.Cost.Amount == 0)
            {
                errors.Add($"Tech '{tech.Id}' must have a positive cost.");
            }

            foreach (var towerId in tech.UnlocksTowerIds)
            {
                if (!towerIds.Contains(towerId))
                {
                    errors.Add($"Tech '{tech.Id}' references missing tower '{towerId}'.");
                }
            }

            foreach (var creepId in tech.UnlocksCreepIds)
            {
                if (!creepIds.Contains(creepId))
                {
                    errors.Add($"Tech '{tech.Id}' references missing creep '{creepId}'.");
                }
            }
        }

        foreach (var map in catalog.Maps)
        {
            ValidateMap(errors, map);
        }

        foreach (var profile in catalog.BotProfiles)
        {
            if (profile.Aggression < 0 || profile.DefenseBias < 0 || profile.MinimumGoldReserve < 0 || profile.MinimumTowerCoverage < 0)
            {
                errors.Add($"Bot profile '{profile.Id}' cannot contain negative tuning values.");
            }

            // Roles rather than tower ids. A build order names jobs now, so the thing worth validating
            // is that some tower somewhere can do each job — an unfillable role would leave every bot
            // falling back to Dps forever without ever saying why.
            foreach (var role in profile.BuildOrder)
            {
                if (!catalog.Towers.Any(tower => tower.Role == role))
                {
                    errors.Add($"Bot profile '{profile.Id}' asks for role {role}, which no tower in this catalog fills.");
                }
            }
        }

        return errors.Count == 0 ? ContentValidationResult.Valid : ContentValidationResult.Invalid(errors);
    }

    private static void AddDuplicateIdErrors(List<string> errors, string kind, IEnumerable<ContentId> ids)
    {
        foreach (var duplicate in ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add($"Duplicate {kind} ID '{duplicate}'.");
        }
    }

    private static void ValidateMap(List<string> errors, MapDefinition map)
    {
        if (map.Width <= 1 || map.Height <= 1)
        {
            errors.Add($"Map '{map.Id}' must be at least 2x2.");
        }

        if (!Contains(map, map.Spawn))
        {
            errors.Add($"Map '{map.Id}' spawn is outside the grid.");
        }

        if (!Contains(map, map.Exit))
        {
            errors.Add($"Map '{map.Id}' exit is outside the grid.");
        }

        if (map.Spawn.Equals(map.Exit))
        {
            errors.Add($"Map '{map.Id}' spawn and exit cannot be the same cell.");
        }

        var blocked = new HashSet<GridPosition>();
        foreach (var cell in map.BlockedCells)
        {
            if (!Contains(map, cell))
            {
                errors.Add($"Map '{map.Id}' has a blocked cell outside the grid at {cell}.");
            }

            if (!blocked.Add(cell))
            {
                errors.Add($"Map '{map.Id}' repeats blocked cell {cell}.");
            }
        }

        if (blocked.Contains(map.Spawn))
        {
            errors.Add($"Map '{map.Id}' spawn cannot be blocked.");
        }

        if (blocked.Contains(map.Exit))
        {
            errors.Add($"Map '{map.Id}' exit cannot be blocked.");
        }
    }

    private static bool Contains(MapDefinition map, GridPosition position) =>
        position.X >= 0 && position.Y >= 0 && position.X < map.Width && position.Y < map.Height;
}
