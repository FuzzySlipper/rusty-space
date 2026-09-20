namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// How an actuator answers a demand, in the two quantities that decide it: how
/// fast it gets there and how much it overshoots on the way.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Frequency"/> is the response frequency in radians per second: the
/// rate at which the actuator closes on a demand. <see cref="DampingRatio"/> is
/// the dimensionless damping ratio of the same response — <c>1.0</c> closes as
/// fast as it can without overshooting, and anything below it arrives past the
/// demand and comes back.
/// </para>
/// <para>
/// Every actuator in the ship carries its own, so two effectors doing the same
/// job on opposite sides of the keel can be given different values and the ship
/// will tell you which one is tired.
/// </para>
/// </remarks>
internal sealed record ActuatorTuning(double Frequency, double DampingRatio)
{
    private const double MinimumPositiveFrequency = 0.0;
    private const double MinimumDampingRatio = 0.0;

    internal ActuatorTuning Validate()
    {
        if (!double.IsFinite(Frequency) || Frequency <= MinimumPositiveFrequency)
        {
            throw new ArgumentOutOfRangeException(nameof(Frequency));
        }

        if (!double.IsFinite(DampingRatio) || DampingRatio < MinimumDampingRatio)
        {
            throw new ArgumentOutOfRangeException(nameof(DampingRatio));
        }

        return this;
    }
}
