using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightBodyState(
    PlanarVector Position,
    double HeadingRadians,
    PlanarVector LinearVelocity,
    double AngularVelocity);
