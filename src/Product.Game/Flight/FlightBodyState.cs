using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightBodyState(
    PlanarVector Position,
    double HeadingRadians,
    PlanarVector LinearVelocity,
    double AngularVelocity)
{
    /// <summary>
    /// Unit vector along the ship's heading, from the one planar frame owner.
    /// </summary>
    internal PlanarVector Forward => PlanarFrame.Forward(HeadingRadians);

    /// <summary>
    /// Unit vector toward the ship's right, for slip and off-center lever arms.
    /// </summary>
    internal PlanarVector Right => PlanarFrame.Right(HeadingRadians);
}
