using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

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
        ShipEffort ship,
        double coupling,
        ulong fixedStepCount,
        uint admittedSteps,
        TimeSpan fixedStep,
        HullStrike strike)
    {
        double elapsed = admittedSteps * fixedStep.TotalSeconds;
        PlanarVector velocityChange = after.LinearVelocity - frame.LinearVelocity;
        current = new FlightTelemetrySnapshot(
            fixedStepCount,
            admittedSteps,
            velocityChange.Dot(frame.Forward) / elapsed,
            velocityChange.Dot(frame.Right) / elapsed,
            (after.AngularVelocity - frame.AngularVelocity) / elapsed,
            control.DriveEffort,
            ship.HeadingEffort,
            control.DriveSaturated,
            ship.HeadingSaturated,
            coupling,
            forces.Field.Force.Magnitude,
            ship.HeadingAsymmetry,
            // What the Engine's contacts gave the hull, in the hull's own frame:
            // the same push the body already answers to, stated where the ship's
            // instruments can read it. The strike handed in is the last one the hull
            // took, so the reading outlives the contact that caused it.
            strike.Impact.LocalImpulse,
            strike.Impact.Magnitude,
            strike.Damage?.Part);
    }

    internal void Reset() => current = FlightTelemetrySnapshot.Neutral;
}
