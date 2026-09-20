using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

internal sealed class FlightController
{
    private const double MinimumThrottleIntent = 0.0;
    private const double MaximumThrottleIntent = 1.0;
    private const double MinimumTurnIntent = -1.0;
    private const double MaximumTurnIntent = 1.0;
    private const double FullResponseFactor = 1.0;
    private const double UnitVectorMagnitude = 1.0;
    private const double NoForwardAcceleration = 0.0;
    private const double NoTurnIntent = 0.0;
    private const double MinimumValidMomentOfInertia = 0.0;
    private const double NoYawTorque = 0.0;
    private const double NoPlanarForce = 0.0;

    private readonly FlightTuning tuning;
    private double throttleLevel;

    internal FlightController(FlightTuning tuning)
    {
        this.tuning = tuning.Validate();
    }

    internal double ThrottleLevel => throttleLevel;

    internal FlightControlOutput Prepare(
        FlightBodyState body,
        FlightCommand command,
        double momentOfInertia,
        TimeSpan step,
        double currentThrottleLevel)
    {
        double throttleIntent = Math.Clamp(
            command.Throttle,
            MinimumThrottleIntent,
            MaximumThrottleIntent);
        double turnIntent = Math.Clamp(command.Turn, MinimumTurnIntent, MaximumTurnIntent);
        double nextThrottleLevel = AdvanceThrottle(throttleIntent, step, currentThrottleLevel);

        PlanarVector commandedForce = body.Forward.Scale(nextThrottleLevel);
        PlanarVector driveForce = RemoveForwardAccelerationAtMaximumSpeed(
            commandedForce,
            body.LinearVelocity);
        Steering steering = ResolveSteering(
            body.AngularVelocity,
            turnIntent,
            momentOfInertia,
            command.StabilizerEnabled);

        return new FlightControlOutput(
            new FlightWrench(driveForce, NoYawTorque),
            new FlightWrench(new PlanarVector(NoPlanarForce, NoPlanarForce), steering.Torque),
            nextThrottleLevel,
            nextThrottleLevel / tuning.MaximumThrust,
            steering.Effort,
            DriveSaturated: driveForce != commandedForce,
            steering.Saturated);
    }

    internal void Commit(FlightControlOutput output) => throttleLevel = output.ThrottleLevel;

    internal void Reset() => throttleLevel = MinimumThrottleIntent;

    private double AdvanceThrottle(double throttleIntent, TimeSpan step, double currentThrottleLevel)
    {
        // Classic inertial flight stops adding force as soon as thrust is
        // released. Acceleration may spool up for feel, but coast begins with
        // no lingering force and therefore preserves its velocity exactly.
        if (throttleIntent == MinimumThrottleIntent)
        {
            return MinimumThrottleIntent;
        }

        double desiredThrust = throttleIntent * tuning.MaximumThrust;
        double responseFactor = Math.Min(
            step.TotalSeconds / tuning.ThrottleResponse.TotalSeconds,
            FullResponseFactor);
        return currentThrottleLevel + ((desiredThrust - currentThrottleLevel) * responseFactor);
    }

    private PlanarVector RemoveForwardAccelerationAtMaximumSpeed(
        PlanarVector commandedForce,
        PlanarVector velocity)
    {
        double speed = velocity.Magnitude;
        if (speed < tuning.MaximumSpeed)
        {
            return commandedForce;
        }

        PlanarVector velocityDirection = velocity.Scale(UnitVectorMagnitude / speed);
        double alongVelocity = commandedForce.Dot(velocityDirection);
        return alongVelocity > NoForwardAcceleration
            ? commandedForce - velocityDirection.Scale(alongVelocity)
            : commandedForce;
    }

    private Steering ResolveSteering(
        double angularVelocity,
        double turnIntent,
        double momentOfInertia,
        bool stabilizerEnabled)
    {
        if (!double.IsFinite(momentOfInertia)
            || momentOfInertia <= MinimumValidMomentOfInertia)
        {
            return new Steering(NoYawTorque, NoPlanarForce, Saturated: false);
        }

        double desiredAngularVelocity = turnIntent * tuning.MaximumTurnRate;
        if (!stabilizerEnabled && desiredAngularVelocity == NoTurnIntent)
        {
            // With the attitude hold disengaged the steering effectors answer a
            // demand and nothing else. A released control asks for nothing, so
            // whatever rotation the ship already has carries on untouched.
            return new Steering(NoYawTorque, NoPlanarForce, Saturated: false);
        }

        double angularVelocityError = desiredAngularVelocity - angularVelocity;
        double torqueAuthority = momentOfInertia
            * tuning.MaximumTurnRate
            / tuning.SteeringResponse.TotalSeconds;
        double requestedTorque = momentOfInertia
            * angularVelocityError
            / tuning.SteeringResponse.TotalSeconds;
        double appliedTorque = Math.Clamp(requestedTorque, -torqueAuthority, torqueAuthority);
        return new Steering(
            appliedTorque,
            Math.Abs(appliedTorque) / torqueAuthority,
            appliedTorque != requestedTorque);
    }

    /// <summary>
    /// Steering demand for one turn: applied yaw torque, how close that is to
    /// the actuator's authority, and whether the clamp took hold.
    /// </summary>
    private readonly record struct Steering(double Torque, double Effort, bool Saturated);
}
