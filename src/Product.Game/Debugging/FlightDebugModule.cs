using Rusty.Engine.Debugging;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

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
            main drive  force ({forces.MainDrive.Force.X:F3}, {forces.MainDrive.Force.Z:F3})  torque {forces.MainDrive.YawTorque:F3}
            steering    force ({forces.Steering.Force.X:F3}, {forces.Steering.Force.Z:F3})  torque {forces.Steering.YawTorque:F3}
            field       force ({forces.Field.Force.X:F3}, {forces.Field.Force.Z:F3})  torque {forces.Field.YawTorque:F3}
            gentle      force ({forces.GentleCurrent.Force.X:F3}, {forces.GentleCurrent.Force.Z:F3})  torque {forces.GentleCurrent.YawTorque:F3}
            swift       force ({forces.SwiftCurrent.Force.X:F3}, {forces.SwiftCurrent.Force.Z:F3})  torque {forces.SwiftCurrent.YawTorque:F3}
            orbital     force ({forces.OrbitalPull.Force.X:F3}, {forces.OrbitalPull.Force.Z:F3})  torque {forces.OrbitalPull.YawTorque:F3}
            damage      force ({forces.DamageBias.Force.X:F3}, {forces.DamageBias.Force.Z:F3})  torque {forces.DamageBias.YawTorque:F3}
            total       force ({forces.Total.Force.X:F3}, {forces.Total.Force.Z:F3})  torque {forces.Total.YawTorque:F3}
            """);
    }

    [DebugCommand("space.attitude", Description = "Shows the hull's heading, turn rate, and motion as the last admitted turn read them back.")]
    public string Attitude()
    {
        FlightReadout readout = flight.Readout;
        return FormattableString.Invariant(
            $"""
            heading     {readout.HeadingRadians:F4}
            yaw rate    {readout.AngularVelocity:F4}
            position    ({readout.Position.X:F2}, {readout.Position.Z:F2})
            velocity    ({readout.LinearVelocity.X:F3}, {readout.LinearVelocity.Z:F3})
            speed       {readout.LinearVelocity.Magnitude:F3}
            """);
    }

    [DebugCommand("space.hardware", Description = "Shows the fitted parts, where each one pushes on the hull, and what its actuator reached.")]
    public string Hardware()
    {
        InstalledShip ship = flight.Ship;
        double heading = flight.Readout.HeadingRadians;
        PlanarVector thrust = ship.MainThrustCenter(heading);
        PlanarVector coupling = ship.FieldCouplingCenter(heading);
        PlanarVector steering = ship.SteeringAuthorityCenter(heading);
        PlanarVector stabilization = ship.StabilizationCenter(heading);
        FlightTelemetrySnapshot telemetry = flight.Telemetry;
        return FormattableString.Invariant(
            $"""
            fit         {ship.LoadoutName}
            part        identity                      mount (x, z)     health  temp   actuator    limit
            {Part("emitter", ship.Emitter)}
            {Part("main drive", ship.MainDrive)}
            {Part("stab port", ship.PortStabilizer)}
            {Part("stab stbd", ship.StarboardStabilizer)}
            centers     thrust ({thrust.X:F2}, {thrust.Z:F2})   coupling ({coupling.X:F2}, {coupling.Z:F2})
            centers     steering ({steering.X:F2}, {steering.Z:F2})   stabilization ({stabilization.X:F2}, {stabilization.Z:F2})
            heading     effort {telemetry.SteeringEffort:F3}   asymmetry {telemetry.HeadingAsymmetry:F3}   saturated {telemetry.SteeringSaturated}
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
            coupling    {telemetry.Coupling:F3}
            field load  {telemetry.FieldLoad:F3}
            impulse     ({telemetry.CollisionImpulse.X:F3}, {telemetry.CollisionImpulse.Z:F3})
            """);
    }

    [DebugCommand("space.coupling", Description = "Shows the coupling actuator, the trim demand driving it, and the attitude hold switch.")]
    public string Coupling()
    {
        FlightCommand command = flight.LastCommand;
        return FormattableString.Invariant(
            $"""
            coupling    {flight.Coupling:F3}
            trim        demand {command.CouplingTrim:F2}  (Q winds out, E winds in)
            attitude    {(command.StabilizerEnabled ? "hold engaged" : "hold disengaged")}
            emergency   {(command.EmergencyUncouple ? "uncouple held" : "clear")}
            """);
    }

    [DebugCommand("space.substeps", Description = "Shows how each push source changed across the substeps of the last admitted turn.")]
    public string Substeps()
    {
        FlightForces first = flight.FirstSubstepContributions;
        FlightForces last = flight.Contributions;
        return FormattableString.Invariant(
            $"""
            substeps    {flight.Telemetry.AdmittedSteps} in the last admitted turn
            turns       {flight.UpdateSequence} admitted, {flight.FixedStepCount} fixed steps simulated
            source      first substep              last substep
            main drive  {Row(first.MainDrive)}  {Row(last.MainDrive)}
            steering    {Row(first.Steering)}  {Row(last.Steering)}
            field       {Row(first.Field)}  {Row(last.Field)}
            gentle      {Row(first.GentleCurrent)}  {Row(last.GentleCurrent)}
            swift       {Row(first.SwiftCurrent)}  {Row(last.SwiftCurrent)}
            orbital     {Row(first.OrbitalPull)}  {Row(last.OrbitalPull)}
            damage      {Row(first.DamageBias)}  {Row(last.DamageBias)}
            total       {Row(first.Total)}  {Row(last.Total)}
            """);
    }

    private static string Part(string role, InstalledPart part) =>
        FormattableString.Invariant(
            $"{role,-11} {part.Id.Value,-27} ({part.Definition.Mount.X,6:F2}, {part.Definition.Mount.Z,5:F2})")
            + FormattableString.Invariant(
                $"  {part.Health,5:F2} {part.Temperature,5:F2} {part.ActuatorValue,7:F3}/{part.ActuatorLimit,5:F2}")
            + (part.Saturated ? "  at stop" : string.Empty);

    private static string Row(FlightWrench wrench) =>
        FormattableString.Invariant(
            $"({wrench.Force.X:F3}, {wrench.Force.Z:F3}) {wrench.YawTorque:F3}");
}
