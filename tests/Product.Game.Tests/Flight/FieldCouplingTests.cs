using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Coupling is the ship's own actuator. Trim winds it at a bounded rate, the
/// emergency release dumps it at once, and it decides only how much of the
/// environment the hull is allowed to feel — it never touches velocity.
/// </summary>
public class FieldCouplingTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private const double Tolerance = 1e-9;
    private const double CradleLevel = 0.6;

    private readonly CouplingTuning tuning = new(
        DefaultLevel: CradleLevel,
        TrimResponse: TimeSpan.FromSeconds(1.5));

    [Fact]
    public void AFreshShipLeavesTheCradleCoupled()
    {
        FieldCoupling coupling = new(tuning);

        Assert.Equal(CradleLevel, coupling.Level, Tolerance);
    }

    [Fact]
    public void TrimWindsTheActuatorAtTheAuthoredRateRatherThanInstantly()
    {
        FieldCoupling coupling = new(tuning);

        double next = coupling.Prepare(Trim(1.0), FixedStep, coupling.Level);

        double expected = CradleLevel + (FixedStep.TotalSeconds / tuning.TrimResponse.TotalSeconds);
        Assert.True(next > CradleLevel);
        Assert.True(next < 1.0);
        Assert.Equal(expected, next, Tolerance);
    }

    [Fact]
    public void TheActuatorStopsAtBothEndsOfItsTravel()
    {
        FieldCoupling coupling = new(tuning);
        double level = coupling.Level;
        for (int step = 0; step < 400; step++)
        {
            level = coupling.Prepare(Trim(1.0), FixedStep, level);
        }

        Assert.Equal(1.0, level, Tolerance);

        for (int step = 0; step < 600; step++)
        {
            level = coupling.Prepare(Trim(-1.0), FixedStep, level);
        }

        Assert.Equal(0.0, level, Tolerance);
    }

    [Fact]
    public void TrimmingDownFromCradleReachesInertialFlight()
    {
        // The sailing has to be windable all the way off, or the environment can
        // never actually be declined.
        FieldCoupling coupling = new(tuning);
        double level = coupling.Level;
        for (int step = 0; step < 120; step++)
        {
            level = coupling.Prepare(Trim(-1.0), FixedStep, level);
        }

        Assert.Equal(0.0, level, Tolerance);
    }

    [Fact]
    public void AnEmergencyUncoupleDumpsTheActuatorOnTheTurnItIsHeld()
    {
        FieldCoupling coupling = new(tuning);

        double dumped = coupling.Prepare(
            new FlightCommand(
                Throttle: 0.0,
                Turn: 0.0,
                CouplingTrim: 0.0,
                StabilizerEnabled: true,
                EmergencyUncouple: true),
            FixedStep,
            coupling.Level);

        Assert.Equal(0.0, dumped, Tolerance);
    }

    [Fact]
    public void NothingMovesTheActuatorUntilTheTurnCommitsIt()
    {
        FieldCoupling coupling = new(tuning);

        double prepared = coupling.Prepare(Trim(1.0), FixedStep, coupling.Level);
        Assert.Equal(CradleLevel, coupling.Level, Tolerance);

        // Commit publishes the staged value, so a turn that advances four
        // substeps still sets the actuator once.
        coupling.Commit(prepared);
        coupling.Commit(prepared);
        Assert.Equal(prepared, coupling.Level, Tolerance);
    }

    [Fact]
    public void AResetReturnsTheActuatorToItsCradleSetting()
    {
        FieldCoupling coupling = new(tuning);
        coupling.Commit(coupling.Prepare(Trim(-1.0), FixedStep, coupling.Level));

        coupling.Reset();

        Assert.Equal(CradleLevel, coupling.Level, Tolerance);
    }

    [Fact]
    public void NoTrimDemandHoldsTheSettingWhereThePlayerLeftIt()
    {
        FieldCoupling coupling = new(tuning);
        double wound = coupling.Prepare(Trim(1.0), FixedStep, coupling.Level);

        Assert.Equal(wound, coupling.Prepare(Trim(0.0), FixedStep, wound), Tolerance);
    }

    private static FlightCommand Trim(double trimIntent) => new(
        Throttle: 0.0,
        Turn: 0.0,
        CouplingTrim: trimIntent,
        StabilizerEnabled: true,
        EmergencyUncouple: false);
}
