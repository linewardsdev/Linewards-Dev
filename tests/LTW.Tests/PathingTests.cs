using System.Diagnostics;
using LTW.Simulation.Content;
using LTW.Simulation.Pathing;
using LTW.Simulation.Primitives;
using Xunit.Abstractions;

namespace LTW.Tests;

public sealed class PathingTests
{
    private readonly ITestOutputHelper output;

    public PathingTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Legal_placement_produces_valid_route()
    {
        var service = new GridPathService();
        var grid = new LaneGrid(OpenMap(width: 6, height: 5));

        var result = service.ValidatePlacement(grid, new GridPosition(2, 2));

        Assert.True(result.IsValid);
        Assert.Equal(PlacementRejectionReason.None, result.RejectionReason);
        Assert.Equal(grid.Spawn, result.Route[0]);
        Assert.Equal(grid.Exit, result.Route[^1]);
        Assert.DoesNotContain(new GridPosition(2, 2), result.Route);
    }

    [Fact]
    public void Vertical_lane_route_prefers_forward_progress_before_right_detours()
    {
        var service = new GridPathService();
        var grid = new LaneGrid(VerticalMap(width: 7, height: 18));

        var result = service.ValidatePlacement(grid, new GridPosition(3, 2));

        Assert.True(result.IsValid);
        Assert.Equal(new GridPosition(3, 0), result.Route[0]);
        Assert.Equal(new GridPosition(3, 1), result.Route[1]);
        Assert.Equal(new GridPosition(2, 1), result.Route[2]);
    }

    [Fact]
    public void Blocking_placement_is_rejected_before_state_changes()
    {
        var service = new GridPathService();
        var map = OpenMap(width: 5, height: 3);
        var grid = new LaneGrid(map, new[]
        {
            new GridPosition(2, 0),
            new GridPosition(2, 2)
        });

        var result = service.ValidatePlacement(grid, new GridPosition(2, 1));

        Assert.False(result.IsValid);
        Assert.Equal(PlacementRejectionReason.PathBlocked, result.RejectionReason);
        Assert.False(grid.IsOccupied(new GridPosition(2, 1)));
    }

    [Fact]
    public void Same_map_and_placement_sequence_produces_same_route()
    {
        static IReadOnlyList<GridPosition> RunSequence()
        {
            var service = new GridPathService();
            var grid = new LaneGrid(OpenMap(width: 8, height: 6));

            foreach (var placement in new[]
                     {
                         new GridPosition(2, 2),
                         new GridPosition(3, 2),
                         new GridPosition(4, 2),
                         new GridPosition(4, 3)
                     })
            {
                var validation = service.ValidatePlacement(grid, placement);
                Assert.True(validation.IsValid);
                grid = grid.WithOccupied(placement);
            }

            return service.FindRoute(grid).Route;
        }

        var first = RunSequence();
        var second = RunSequence();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Placement_validation_reports_unity_friendly_rejection_reasons()
    {
        var service = new GridPathService();
        var grid = new LaneGrid(OpenMap(width: 4, height: 4), new[] { new GridPosition(1, 1) });

        Assert.Equal(PlacementRejectionReason.OutsideGrid, service.ValidatePlacement(grid, new GridPosition(5, 0)).RejectionReason);
        Assert.Equal(PlacementRejectionReason.SpawnCell, service.ValidatePlacement(grid, grid.Spawn).RejectionReason);
        Assert.Equal(PlacementRejectionReason.ExitCell, service.ValidatePlacement(grid, grid.Exit).RejectionReason);
        Assert.Equal(PlacementRejectionReason.AlreadyOccupied, service.ValidatePlacement(grid, new GridPosition(1, 1)).RejectionReason);
    }

    [Fact]
    public void Path_cache_invalidates_only_requested_lane()
    {
        var service = new GridPathService();
        var cache = new LanePathCache(service);
        var grid = new LaneGrid(OpenMap(width: 6, height: 5));
        var laneOne = new LaneId(1);
        var laneTwo = new LaneId(2);

        var laneOneFirst = cache.GetOrCalculate(laneOne, grid);
        var laneTwoFirst = cache.GetOrCalculate(laneTwo, grid);

        cache.Invalidate(laneOne);

        var laneOneSecond = cache.GetOrCalculate(laneOne, grid.WithOccupied(new GridPosition(2, 2)));
        var laneTwoSecond = cache.GetOrCalculate(laneTwo, grid.WithOccupied(new GridPosition(2, 2)));

        Assert.NotEqual(laneOneFirst, laneOneSecond);
        Assert.Same(laneTwoFirst, laneTwoSecond);
    }

    [Fact]
    public void Heavy_placement_scenario_records_benchmark_result()
    {
        var service = new GridPathService();
        var grid = new LaneGrid(OpenMap(width: 18, height: 12));
        var accepted = 0;
        var rejected = 0;
        var stopwatch = Stopwatch.StartNew();

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var position = new GridPosition(x, y);
                var validation = service.ValidatePlacement(grid, position);
                if (validation.IsValid)
                {
                    accepted++;
                    grid = grid.WithOccupied(position);
                }
                else
                {
                    rejected++;
                }
            }
        }

        stopwatch.Stop();
        output.WriteLine($"Heavy placement benchmark: accepted={accepted}, rejected={rejected}, elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:0.###}");

        Assert.True(accepted > 0);
        Assert.True(rejected > 0);
        Assert.True(service.FindRoute(grid).Found);
    }

    private static MapDefinition OpenMap(int width, int height)
    {
        return new MapDefinition(
            new ContentId($"map.{width}x{height}"),
            "Open Test Map",
            width,
            height,
            new GridPosition(0, height / 2),
            new GridPosition(width - 1, height / 2),
            Array.Empty<GridPosition>());
    }

    private static MapDefinition VerticalMap(int width, int height)
    {
        return new MapDefinition(
            new ContentId($"map.vertical-{width}x{height}"),
            "Vertical Test Map",
            width,
            height,
            new GridPosition(width / 2, 0),
            new GridPosition(width / 2, height - 1),
            Array.Empty<GridPosition>());
    }
}
