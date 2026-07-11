using System.Reflection;
using LTW.Simulation;

namespace LTW.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void SimulationAssemblyDoesNotReferenceUnity()
    {
        Assembly simulationAssembly = typeof(AssemblyMarker).Assembly;

        string[] referencedAssemblies = simulationAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referencedAssemblies,
            assemblyName => assemblyName.StartsWith("Unity", StringComparison.Ordinal));
    }
}
