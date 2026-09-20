using Xunit;

namespace Rusty.Space.Product.ShipSystems.Tests;

/// <summary>
/// The actuator primitive every part on the ship is built out of. What is pinned
/// here is what makes a ship's handling learnable: it takes time to get where it
/// is told, how much it overshoots is set by its damping ratio and nothing else,
/// a hard actuator stops at its stop rather than inventing force, and the whole
/// travel is a function of simulated time — not of how many turns the host
/// happened to admit, and not of chance.
/// </summary>
public class ActuatorResponseTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private static readonly TimeSpan HalfStep = TimeSpan.FromSeconds(1.0 / 120.0);

    private const double Demand = 1.0;
    private const int OneSecond = 60;
    private const int ThreeSeconds = 180;
    private const double SettledTolerance = 5e-3;
    private const double NoDemand = 0.0;
    private const double FullScale = 1.0;
    private const double CriticallyDamped = 1.0;
    private const double LightlyDamped = 0.20;
    private const double ResponseFrequency = 10.0;
    private const double SlowResponse = 4.0;
    private const double FastResponse = 20.0;
    private const double CoarseComparisonTolerance = 2e-2;

    [Fact]
    public void ACriticallyDampedActuatorSettlesOnItsDemandWithoutOvershooting()
    {
        ActuatorResponse actuator = Actuator(CriticallyDamped);

        double[] travel = Travel(actuator, Demand, OneSecond);

        Assert.True(
            Math.Abs(travel[^1] - Demand) < SettledTolerance,
            $"expected to be settled on the demand, ended at {travel[^1]}");
        Assert.True(
            Peak(travel) <= Demand + SettledTolerance,
            $"expected no overshoot, peaked at {Peak(travel)}");
    }

    [Fact]
    public void ALightlyDampedActuatorArrivesPastItsDemandAndComesBack()
    {
        ActuatorResponse actuator = Actuator(LightlyDamped);

        double[] travel = Travel(actuator, Demand, ThreeSeconds);

        Assert.True(
            Peak(travel) > Demand + 0.2,
            $"expected a visible overshoot, peaked at {Peak(travel)}");
        Assert.True(
            Math.Abs(travel[^1] - Demand) < CoarseComparisonTolerance,
            $"expected the ringing to settle back onto the demand, ended at {travel[^1]}");
    }

    [Theory]
    [InlineData(0.20, 0.52)]
    [InlineData(0.35, 0.30)]
    [InlineData(0.80, 0.02)]
    public void TheDampingRatioDecidesHowMuchItOvershoots(double dampingRatio, double expectedOvershoot)
    {
        // Overshoot is the damping ratio's doing and nothing else's: this is the
        // dial a worn actuator is described with, so it has to be the only thing
        // that moves it.
        ActuatorResponse actuator = Actuator(dampingRatio);

        double overshoot = Peak(Travel(actuator, Demand, OneSecond)) - Demand;

        Assert.True(
            Math.Abs(overshoot - expectedOvershoot) < 0.04,
            $"damping {dampingRatio} overshot by {overshoot}, expected about {expectedOvershoot}");
    }

    [Fact]
    public void AHardActuatorStopsAtItsStopInsteadOfInventingTravel()
    {
        ActuatorResponse actuator = new(
            new ActuatorTuning(ResponseFrequency, CriticallyDamped),
            limit: FullScale);

        double[] travel = Travel(actuator, demand: 10.0, OneSecond);

        Assert.True(
            Peak(travel) <= FullScale + SettledTolerance,
            $"expected the stop to hold at {FullScale}, peaked at {Peak(travel)}");
        Assert.True(actuator.Saturated, "expected the demand beyond the stop to be reported");
        Assert.Equal(FullScale, actuator.Value, SettledTolerance);
    }

    [Fact]
    public void ADemandWithinTheStopIsNeverReportedAsSaturated()
    {
        ActuatorResponse actuator = new(
            new ActuatorTuning(ResponseFrequency, CriticallyDamped),
            limit: FullScale);

        Travel(actuator, Demand, OneSecond);

        Assert.False(actuator.Saturated);
    }

    [Fact]
    public void AFasterActuatorIsFurtherAlongAtTheSameInstant()
    {
        ActuatorResponse willing = new(
            new ActuatorTuning(FastResponse, CriticallyDamped),
            limit: FullScale);
        ActuatorResponse tired = new(
            new ActuatorTuning(SlowResponse, CriticallyDamped),
            limit: FullScale);

        double[] willingTravel = Travel(willing, Demand, steps: 6);
        double[] tiredTravel = Travel(tired, Demand, steps: 6);

        Assert.True(
            willingTravel[^1] > tiredTravel[^1] + 0.1,
            $"expected the faster actuator ahead of the slower one, got {willingTravel[^1]} against {tiredTravel[^1]}");
    }

    [Fact]
    public void TheSameSecondOfTravelIsTheSameTravelHoweverItWasAdmitted()
    {
        // A catch-up turn hands the actuator four short admitted steps where a
        // smooth one hands it one long one. The response is a property of the
        // hardware over simulated time, so the two have to agree; if they did
        // not, the ship's handling would depend on the host's frame rate.
        ActuatorResponse oneWay = Actuator(LightlyDamped, limit: 4.0);
        ActuatorResponse otherWay = Actuator(LightlyDamped, limit: 4.0);

        double settled = Travel(oneWay, Demand, FixedStep, ThreeSeconds)[^1];
        double caughtUp = Travel(otherWay, Demand, HalfStep, ThreeSeconds * 2)[^1];

        Assert.True(
            Math.Abs(settled - caughtUp) < CoarseComparisonTolerance,
            $"the same second arrived at {settled} and {caughtUp} depending on step size");
        Assert.True(
            Math.Abs(Peak(Travel(Actuator(LightlyDamped, 4.0), Demand, HalfStep, ThreeSeconds * 2))
                - Peak(Travel(Actuator(LightlyDamped, 4.0), Demand, FixedStep, ThreeSeconds))) < 0.05,
            "the overshoot moved with the step size");
    }

    [Fact]
    public void ResetLeavesNoTravelInFlight()
    {
        ActuatorResponse actuator = Actuator(LightlyDamped);
        Travel(actuator, Demand, steps: 10);
        Assert.True(actuator.Value > 0.0);

        actuator.Reset();

        Assert.Equal(NoDemand, actuator.Value, SettledTolerance);
        Assert.Equal(NoDemand, actuator.Velocity, SettledTolerance);
        Assert.False(actuator.Saturated);
    }

    private static ActuatorResponse Actuator(double dampingRatio, double limit = 4.0) =>
        new(new ActuatorTuning(ResponseFrequency, dampingRatio), limit);

    private static double[] Travel(
        ActuatorResponse actuator,
        double demand,
        int steps)
    {
        double[] travel = new double[steps];
        for (int step = 0; step < steps; step++)
        {
            actuator.Advance(demand, FixedStep);
            travel[step] = actuator.Value;
        }

        return travel;
    }

    private static double[] Travel(
        ActuatorResponse actuator,
        double demand,
        TimeSpan step,
        int steps)
    {
        double[] travel = new double[steps];
        for (int index = 0; index < steps; index++)
        {
            actuator.Advance(demand, step);
            travel[index] = actuator.Value;
        }

        return travel;
    }

    private static double Peak(double[] travel)
    {
        double peak = double.MinValue;
        foreach (double value in travel)
        {
            peak = Math.Max(peak, value);
        }

        return peak;
    }
}
