using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Owns what the body readout cannot show for one admitted turn: acceleration in
/// the ship's own frame, actuator effort, and the load the local field puts on
/// the hull. Every value is derived from the readouts either side of the Engine
/// step on the fixed-step clock; nothing here samples wall-clock time, and
/// nothing here feeds back into flight state.
/// </summary>
internal sealed class FlightTelemetry
{
    private FlightTelemetrySnapshot current = FlightTelemetrySnapshot.Neutral;

    internal FlightTelemetrySnapshot Current => current;

    internal void Capture(
        FlightBodyState frame,
        FlightReadout after,
        FlightForces forces,
        FlightControlOutput control,
        ulong fixedStepCount,
        uint admittedSteps,
        TimeSpan fixedStep)
    {
        ValidateStep(fixedStep);

        double elapsed = admittedSteps * fixedStep.TotalSeconds;
        PlanarVector velocityChange = after.LinearVelocity - frame.LinearVelocity;
        current = new FlightTelemetrySnapshot(
            fixedStepCount,
            admittedSteps,
            velocityChange.Dot(frame.Forward) / elapsed,
            velocityChange.Dot(frame.Right) / elapsed,
            (after.AngularVelocity - frame.AngularVelocity) / elapsed,
            control.DriveEffort,
            control.SteeringEffort,
            control.DriveSaturated,
            control.SteeringSaturated,
            forces.Field.Force.Magnitude,
            // Impacts report through this same value once local geometry exists.
            PlanarVector.Zero);
    }

    internal void Reset() => current = FlightTelemetrySnapshot.Neutral;

    private static void ValidateStep(TimeSpan fixedStep)
    {
        if (fixedStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedStep));
        }
    }
}
