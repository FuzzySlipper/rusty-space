using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// What a fixed step did to the ship, in the ship's own frame. Body readouts
/// show where the ship ended up; this shows how hard it was pushed, how much of
/// that push the actuators could actually deliver, and whether the two sides of
/// the effector pair agreed about it.
/// </summary>
/// <remarks>
/// <see cref="CollisionImpulse"/> is what the Engine's contacts gave the hull on
/// the turn this was captured from, stated in the hull's own frame the same way
/// every other value here is: the push is the Engine's answer about a body, and
/// this is that answer put where the ship's instruments can read it. A turn with
/// no contact reports nothing.
/// </remarks>
internal readonly record struct FlightTelemetrySnapshot(
    ulong FixedStepCount,
    uint AdmittedSteps,
    double ForwardAcceleration,
    double LateralAcceleration,
    double YawAcceleration,
    double DriveEffort,
    double SteeringEffort,
    bool DriveSaturated,
    bool SteeringSaturated,
    double Coupling,
    double FieldLoad,
    double HeadingAsymmetry,
    PlanarVector CollisionImpulse,
    double CollisionMagnitude,
    PartId? StruckPart)
{
    internal static FlightTelemetrySnapshot Neutral { get; } = new(
        0UL,
        0U,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        DriveSaturated: false,
        SteeringSaturated: false,
        // No turn has been captured yet, so no coupling was in effect.
        Coupling: 0.0,
        FieldLoad: 0.0,
        HeadingAsymmetry: 0.0,
        CollisionImpulse: PlanarVector.Zero,
        CollisionMagnitude: 0.0,
        StruckPart: null);
}
