using Rusty.Engine;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Space runs on the time the Engine admits. These pin that a turn carries the
/// admitted fixed delta and count, that the turn's duration is derived from
/// those facts rather than a second clock, and that the one boundary check
/// refuses facts that genuinely cannot be integrated step by step.
/// </summary>
public class AdmittedTurnTests
{
    // A TimeSpan is a whole number of 100 ns ticks, so a duration rebuilt from
    // a rate can only be compared at that resolution.
    private const double Tolerance = 1e-6;

    [Fact]
    public void ATurnCarriesTheAdmittedFixedDeltaAndCount()
    {
        AdmittedTurn turn = AdmittedTurn.FromFacts(Facts(steps: 3, fixedDeltaSeconds: 1.0 / 60.0));

        Assert.Equal(3U, turn.StepCount);
        Assert.Equal(TimeSpan.FromSeconds(1.0 / 60.0), turn.FixedStep);
    }

    [Fact]
    public void TheTurnDurationIsTheAdmittedCountAtTheAdmittedRate()
    {
        AdmittedTurn one = AdmittedTurn.FromFacts(Facts(1, 1.0 / 60.0));
        AdmittedTurn four = AdmittedTurn.FromFacts(Facts(4, 1.0 / 60.0));

        Assert.Equal(4.0 * one.Duration.TotalSeconds, four.Duration.TotalSeconds, Tolerance);
    }

    [Fact]
    public void ATurnAdmittedWithNoStepsCarriesNoTime()
    {
        AdmittedTurn turn = AdmittedTurn.FromFacts(Facts(0, 1.0 / 60.0));

        Assert.Equal(0U, turn.StepCount);
        Assert.Equal(TimeSpan.Zero, turn.Duration);
    }

    [Fact]
    public void TheIntegratedStepCarriesTheAdmittedRateRatherThanARestatedOne()
    {
        // A host configured at another fixed rate changes the step the product
        // asks Dynamics to integrate, without any product constant moving.
        AdmittedTurn halfRate = AdmittedTurn.FromFacts(Facts(1, 1.0 / 30.0));

        Assert.Equal(1.0f / 30.0f, halfRate.DynamicsStepSeconds);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.016666666666666666)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void StepsAdmittedWithoutAUsableDeltaAreRefusedOnce(double fixedDeltaSeconds)
    {
        Assert.Throws<InvalidOperationException>(
            () => AdmittedTurn.FromFacts(Facts(1, fixedDeltaSeconds)));
    }

    private static ProductUpdateFacts Facts(uint steps, double fixedDeltaSeconds) => new(
        ProductUpdateMode.Realtime,
        ProductLifecycleState.Running,
        Generation: 1UL,
        ControlRevision: 0UL,
        ObservedHostTimeNanoseconds: 0UL,
        SimulationStep: 0UL,
        FixedStepHz: 60U,
        AdmittedStepCount: steps,
        DroppedStepCount: 0UL,
        FixedDeltaSeconds: fixedDeltaSeconds);
}
