using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightReadout(
    PlanarVector Position,
    double HeadingRadians,
    PlanarVector LinearVelocity,
    double AngularVelocity,
    double Mass,
    double YawInertia);
