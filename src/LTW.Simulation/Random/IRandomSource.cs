namespace LTW.Simulation.Random;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();
}
