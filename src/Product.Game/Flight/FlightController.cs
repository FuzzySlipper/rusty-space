using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>Resolves pilot demands; installed parts own response and authority.</summary>
internal sealed class FlightController(FlightTuning tuning)
{
    internal FlightControlOutput Resolve(
        FlightBodyState body,
        FlightCommand command,
        double momentOfInertia)
    {
        double throttle = Math.Clamp(command.Throttle, 0.0, 1.0);
        double turn = Math.Clamp(command.Turn, -1.0, 1.0);
        PlanarVector commandedForce = body.Forward.Scale(throttle * tuning.MaximumThrust);
        PlanarVector driveForce = RemoveForwardAccelerationAtMaximumSpeed(commandedForce, body.LinearVelocity);
        double requestedTorque = 0.0;
        if (double.IsFinite(momentOfInertia) && momentOfInertia > 0.0
            && (command.StabilizerEnabled || turn != 0.0))
        {
            // SteeringResponse is the controller's error gain, not another
            // actuator lag or torque stop. The fitted vanes limit delivery.
            double desiredRate = turn * tuning.MaximumTurnRate;
            requestedTorque = momentOfInertia * (desiredRate - body.AngularVelocity)
                / tuning.SteeringResponse.TotalSeconds;
        }

        return new FlightControlOutput(
            new FlightWrench(driveForce, 0.0),
            new FlightWrench(PlanarVector.Zero, requestedTorque),
            throttle,
            DriveSaturated: driveForce != commandedForce);
    }

    private PlanarVector RemoveForwardAccelerationAtMaximumSpeed(PlanarVector commandedForce, PlanarVector velocity)
    {
        double speed = velocity.Magnitude;
        if (speed < tuning.MaximumSpeed)
        {
            return commandedForce;
        }

        PlanarVector velocityDirection = velocity.Scale(1.0 / speed);
        double alongVelocity = commandedForce.Dot(velocityDirection);
        return alongVelocity > 0.0
            ? commandedForce - velocityDirection.Scale(alongVelocity)
            : commandedForce;
    }
}
