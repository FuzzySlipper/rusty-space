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
    private const double MinimumValidMomentOfInertia = 0.0;
    private const double NoYawTorque = 0.0;

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
        ValidateStep(step);

        double throttleIntent = Math.Clamp(
            command.Throttle,
            MinimumThrottleIntent,
            MaximumThrottleIntent);
        double turnIntent = Math.Clamp(command.Turn, MinimumTurnIntent, MaximumTurnIntent);
        double nextThrottleLevel = AdvanceThrottle(throttleIntent, step, currentThrottleLevel);

        PlanarVector force = Forward(body.HeadingRadians).Scale(nextThrottleLevel);
        force = RemoveForwardAccelerationAtMaximumSpeed(force, body.LinearVelocity);

        return new FlightControlOutput(
            new FlightWrench(force, ResolveYawTorque(body.AngularVelocity, turnIntent, momentOfInertia)),
            nextThrottleLevel);
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
        PlanarVector requestedForce,
        PlanarVector velocity)
    {
        double speed = velocity.Magnitude;
        if (speed < tuning.MaximumSpeed)
        {
            return requestedForce;
        }

        PlanarVector velocityDirection = velocity.Scale(UnitVectorMagnitude / speed);
        double alongVelocity = requestedForce.Dot(velocityDirection);
        return alongVelocity > NoForwardAcceleration
            ? requestedForce - velocityDirection.Scale(alongVelocity)
            : requestedForce;
    }

    private double ResolveYawTorque(double angularVelocity, double turnIntent, double momentOfInertia)
    {
        if (!double.IsFinite(momentOfInertia)
            || momentOfInertia <= MinimumValidMomentOfInertia)
        {
            return NoYawTorque;
        }

        double desiredAngularVelocity = turnIntent * tuning.MaximumTurnRate;
        double angularVelocityError = desiredAngularVelocity - angularVelocity;
        double torqueAuthority = momentOfInertia * tuning.MaximumTurnRate / tuning.SteeringResponse.TotalSeconds;
        double requestedTorque = momentOfInertia * angularVelocityError / tuning.SteeringResponse.TotalSeconds;
        return Math.Clamp(requestedTorque, -torqueAuthority, torqueAuthority);
    }

    private static PlanarVector Forward(double headingRadians) =>
        new(Math.Cos(headingRadians), Math.Sin(headingRadians));

    private static void ValidateStep(TimeSpan step)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
    }
}
