using Rusty.Engine.Debugging;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;

namespace Rusty.Space.Product.Debugging;

/// <summary>
/// Read-only live-debug view of the flight spine. Every command reports state
/// and none writes back, so handling is tuned from what the product actually
/// decided this turn instead of from a rebuild and a guess.
/// </summary>
/// <remarks>
/// Public because the Engine emits catalog dispatch into the product
/// composition, which sees Product.Game as a referenced assembly. Only the
/// composition root constructs it; the commands read, never mutate.
/// </remarks>
public sealed class FlightDebugModule : IDebugCommandModule
{
    private readonly SpaceFlight flight;

    internal FlightDebugModule(SpaceFlight flight)
        => this.flight = flight ?? throw new ArgumentNullException(nameof(flight));

    [DebugCommand("space.forces", Description = "Shows the last admitted turn's push split by the source that produced it.")]
    public string Forces()
    {
        FlightForces forces = flight.Contributions;
        return FormattableString.Invariant(
            $"""
            fixed step {flight.FixedStepCount}
            main drive  force ({forces.MainDrive.Force.X:F3}, {forces.MainDrive.Force.Z:F3})  torque {forces.MainDrive.TorqueY:F3}
            steering    force ({forces.Steering.Force.X:F3}, {forces.Steering.Force.Z:F3})  torque {forces.Steering.TorqueY:F3}
            field       force ({forces.Field.Force.X:F3}, {forces.Field.Force.Z:F3})  torque {forces.Field.TorqueY:F3}
            gentle      force ({forces.GentleCurrent.Force.X:F3}, {forces.GentleCurrent.Force.Z:F3})  torque {forces.GentleCurrent.TorqueY:F3}
            swift       force ({forces.SwiftCurrent.Force.X:F3}, {forces.SwiftCurrent.Force.Z:F3})  torque {forces.SwiftCurrent.TorqueY:F3}
            orbital     force ({forces.OrbitalPull.Force.X:F3}, {forces.OrbitalPull.Force.Z:F3})  torque {forces.OrbitalPull.TorqueY:F3}
            damage      force ({forces.DamageBias.Force.X:F3}, {forces.DamageBias.Force.Z:F3})  torque {forces.DamageBias.TorqueY:F3}
            total       force ({forces.Total.Force.X:F3}, {forces.Total.Force.Z:F3})  torque {forces.Total.TorqueY:F3}
            """);
    }

    [DebugCommand("space.field", Description = "Shows the stellar field sample the last admitted turn resolved against.")]
    public string Field()
    {
        FieldSample sample = flight.LastFieldSample;
        return FormattableString.Invariant(
            $"""
            flow        ({sample.FlowVelocity.X:F3}, {sample.FlowVelocity.Z:F3})
            intensity   {sample.Intensity:F3}
            turbulence  ({sample.Turbulence.X:F3}, {sample.Turbulence.Z:F3})
            gradient    {sample.Gradient.AbsoluteMagnitude:F4}
            """);
    }

    [DebugCommand("space.controller", Description = "Shows commanded intent and the actuator state it produced.")]
    public string Controller()
    {
        FlightTelemetrySnapshot telemetry = flight.Telemetry;
        return FormattableString.Invariant(
            $"""
            commanded   throttle {flight.LastCommand.Throttle:F2}  turn {flight.LastCommand.Turn:F2}
            drive       effort {telemetry.DriveEffort:F3}  saturated {telemetry.DriveSaturated}
            steering    effort {telemetry.SteeringEffort:F3}  saturated {telemetry.SteeringSaturated}
            """);
    }

    [DebugCommand("space.telemetry", Description = "Shows the last admitted turn's acceleration in the ship's own frame and the load upon it.")]
    public string Telemetry()
    {
        FlightTelemetrySnapshot telemetry = flight.Telemetry;
        return FormattableString.Invariant(
            $"""
            fixed step  {telemetry.FixedStepCount} over {telemetry.AdmittedSteps} admitted step(s)
            accel       forward {telemetry.ForwardAcceleration:F3}  lateral {telemetry.LateralAcceleration:F3}  yaw {telemetry.YawAcceleration:F3}
            field load  {telemetry.FieldLoad:F3}
            impulse     ({telemetry.CollisionImpulse.X:F3}, {telemetry.CollisionImpulse.Z:F3})
            """);
    }
}
