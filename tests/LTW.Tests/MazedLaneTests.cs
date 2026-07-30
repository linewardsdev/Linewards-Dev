using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Guards the fixture the other harnesses now measure against, because a maze that quietly stopped being
/// a maze would silently restore the straight-lane distortion it exists to remove.
/// </summary>
public sealed class MazedLaneTests
{
    private readonly ITestOutputHelper output;

    public MazedLaneTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void The_maze_route_is_much_longer_than_the_straight_one()
    {
        var straight = MazedLane.StraightRoute();
        var mazed = MazedLane.Build();

        output.WriteLine($"straight {straight.Count} cells, mazed {mazed.Route.Count} cells, {mazed.MazeCells.Count} maze cells");

        Assert.True(
            mazed.Route.Count > straight.Count * 2,
            $"maze only reached {mazed.Route.Count} cells against {straight.Count} straight");
    }

    [Fact]
    public void The_maze_route_is_walkable_and_reaches_the_exit()
    {
        var mazed = MazedLane.Build();

        Assert.Equal(mazed.Map.Spawn, mazed.Route[0]);
        Assert.Equal(mazed.Map.Exit, mazed.Route[^1]);

        // Every step is orthogonal and exactly one cell.
        for (var index = 1; index < mazed.Route.Count; index++)
        {
            var previous = mazed.Route[index - 1];
            var current = mazed.Route[index];
            var distance = System.Math.Abs(current.X - previous.X) + System.Math.Abs(current.Y - previous.Y);
            Assert.Equal(1, distance);
        }
    }

    [Fact]
    public void Maze_cells_never_sit_on_the_route()
    {
        var mazed = MazedLane.Build();

        Assert.DoesNotContain(mazed.MazeCells, cell => mazed.Route.Contains(cell));
    }

    [Fact]
    public void A_cell_beside_the_route_is_free_and_adjacent()
    {
        var mazed = MazedLane.Build();

        foreach (var fraction in new[] { 0.25d, 0.5d, 0.75d })
        {
            var cell = mazed.CellBesideRoute(fraction);

            Assert.DoesNotContain(cell, mazed.Route);
            Assert.DoesNotContain(cell, mazed.MazeCells);
            Assert.Contains(
                mazed.Route,
                routeCell => System.Math.Abs(routeCell.X - cell.X) + System.Math.Abs(routeCell.Y - cell.Y) == 1);
        }
    }
}
