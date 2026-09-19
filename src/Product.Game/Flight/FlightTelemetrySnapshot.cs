using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// What a fixed step did to the ship, in the ship's own frame. Body readouts
/// show where the ship ended up; this shows how hard it was pushed and how much
/// of that push the actuators could actually deliver.
/// </summary>
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
    PlanarVector CollisionImpulse)
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
        CollisionImpulse: PlanarVector.Zero);
}
