namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// One part as it stands on this hull: what it is, how warm it is, where its
/// actuator actually is, and whether it managed what it was asked for.
/// </summary>
/// <remarks>
/// <para>
/// A definition is a design; this is a specific piece of hardware that has been
/// fitted. The part carries the state that only a fitted part has: the travel of
/// its actuator between admitted steps, and the heat it has taken on. Nothing
/// here decides how the ship should move — the part reports what its actuator
/// reached, and the owner that asked for it turns that into force.
/// </para>
/// <para>
/// Heat accumulates while a part is worked and gives itself up when it is left
/// alone. It is reported so a hot part is visible to the player's instruments;
/// what heat costs a part — derating it, damaging it, forcing a cooldown — is the
/// thermal and health phase's to decide, not this one's.
/// </para>
/// </remarks>
internal sealed class InstalledPart
{
    // Heat gained in one second at full demand, and the fraction of the heat it
    // carries that a part gives up in one second. Together they settle a part
    // worked continuously at its rating just under the nominal envelope, so a
    // part asked to overdeliver is the one that climbs past it.
    private const double HeatPerSecondAtFullDemand = 0.25;
    private const double CoolingPerSecond = 0.30;
    private const double AmbientTemperature = 0.0;
    private const double NoDemand = 0.0;

    private readonly ActuatorResponse response;
    private double temperature = AmbientTemperature;

    internal InstalledPart(PartDefinition definition, ActuatorResponse response)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.response = response ?? throw new ArgumentNullException(nameof(response));
    }

    internal PartDefinition Definition { get; }

    internal PartId Id => Definition.Id;

    /// <summary>
    /// The fraction of its rating this part can still put up, as it was fitted.
    /// </summary>
    internal double Health => Definition.Health;

    /// <summary>
    /// How warm the part is against its nominal envelope: <c>0</c> at ambient,
    /// <c>1</c> at the envelope, and past it for hardware being overworked.
    /// </summary>
    internal double Temperature => temperature;

    /// <summary>Where the part's actuator stands after the last advance.</summary>
    internal double ActuatorValue => response.Value;

    /// <summary>How fast the part's actuator is travelling.</summary>
    internal double ActuatorTravel => response.Velocity;

    /// <summary>The stop the part's actuator is built against.</summary>
    internal double ActuatorLimit => response.Limit;

    /// <summary>Whether the last demand asked for more than this part can reach.</summary>
    internal bool Saturated => response.Saturated;

    /// <summary>
    /// Travels this part's actuator toward a demand over one admitted fixed step
    /// and takes on the heat that working it costs. Returns where the actuator
    /// ended up, which is what the requesting owner delivers.
    /// </summary>
    internal double Advance(double demand, double demandAsFractionOfRating, TimeSpan step)
    {
        response.Advance(demand, step);
        Warm(demandAsFractionOfRating, step);
        return response.Value;
    }

    /// <summary>
    /// The same advance with the damping the actuator answers with this step
    /// supplied by the owner, for a part whose wear depends on how hard the
    /// environment is pushing it.
    /// </summary>
    internal double Advance(
        double demand,
        double dampingRatio,
        double demandAsFractionOfRating,
        TimeSpan step)
    {
        response.Advance(demand, dampingRatio, step);
        Warm(demandAsFractionOfRating, step);
        return response.Value;
    }

    internal void Reset()
    {
        response.Reset();
        temperature = AmbientTemperature;
    }

    private void Warm(double demandAsFractionOfRating, TimeSpan step)
    {
        double seconds = step.TotalSeconds;
        double gathered = Math.Abs(demandAsFractionOfRating) * HeatPerSecondAtFullDemand * seconds;
        double givenUp = (temperature - AmbientTemperature) * CoolingPerSecond * seconds;
        temperature = Math.Max(AmbientTemperature, temperature + gathered - givenUp);
    }
}
