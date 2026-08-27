namespace Rusty.Space.Product.Flight;

internal sealed record FlightTuning(
    double MaximumSpeed,
    double MaximumThrust,
    double MaximumTurnRate,
    TimeSpan ThrottleResponse,
    TimeSpan SteeringResponse)
{
    private const double MinimumPositiveMagnitude = 0.0;

    internal FlightTuning Validate()
    {
        ValidatePositiveFinite(MaximumSpeed, nameof(MaximumSpeed));
        ValidatePositiveFinite(MaximumThrust, nameof(MaximumThrust));
        ValidatePositiveFinite(MaximumTurnRate, nameof(MaximumTurnRate));
        ValidatePositive(SteeringResponse, nameof(SteeringResponse));
        ValidatePositive(ThrottleResponse, nameof(ThrottleResponse));
        return this;
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= MinimumPositiveMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
