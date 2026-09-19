using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightBodyState(
    PlanarVector Position,
    double HeadingRadians,
    PlanarVector LinearVelocity,
    double AngularVelocity)
{
    /// <summary>
    /// Unit vector along the ship's heading in the product planar frame. Every
    /// heading-derived vector comes from here so the convention has one home.
    /// </summary>
    internal PlanarVector Forward => new(Math.Cos(HeadingRadians), Math.Sin(HeadingRadians));

    /// <summary>
    /// Unit lateral vector a quarter turn from <see cref="Forward"/>, used for
    /// slip and off-center lever arms.
    /// </summary>
    internal PlanarVector Lateral => new(-Forward.Z, Forward.X);
}
