using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

public readonly record struct FlightReadout(
    PlanarVector Position,
    double HeadingRadians,
    PlanarVector LinearVelocity,
    double AngularVelocity,
    double Mass,
    double YawInertia);
