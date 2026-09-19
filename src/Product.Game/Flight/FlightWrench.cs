using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightWrench(PlanarVector Force, double TorqueY)
{
    internal static FlightWrench Zero { get; } = new(PlanarVector.Zero, 0.0);

    public static FlightWrench operator +(FlightWrench left, FlightWrench right) =>
        new(left.Force + right.Force, left.TorqueY + right.TorqueY);
}
