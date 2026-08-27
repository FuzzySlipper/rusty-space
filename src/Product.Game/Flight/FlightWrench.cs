using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightWrench(PlanarVector Force, double TorqueY)
{
    internal static FlightWrench Zero { get; } = new(PlanarVector.Zero, 0.0);
}
