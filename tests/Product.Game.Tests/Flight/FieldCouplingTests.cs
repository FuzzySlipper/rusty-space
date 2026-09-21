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

        coupling.Advance(Trim(1.0), FixedStep);

        double expected = CradleLevel + (FixedStep.TotalSeconds / tuning.TrimResponse.TotalSeconds);
        Assert.True(coupling.Level > CradleLevel);
        Assert.True(coupling.Level < 1.0);
        Assert.Equal(expected, coupling.Level, Tolerance);
    }

    [Fact]
    public void TheActuatorStopsAtBothEndsOfItsTravel()
    {
        FieldCoupling coupling = new(tuning);
        for (int step = 0; step < 400; step++)
        {
            coupling.Advance(Trim(1.0), FixedStep);
        }

        Assert.Equal(1.0, coupling.Level, Tolerance);

        for (int step = 0; step < 600; step++)
        {
            coupling.Advance(Trim(-1.0), FixedStep);
        }

        Assert.Equal(0.0, coupling.Level, Tolerance);
    }

    [Fact]
    public void TrimmingDownFromCradleReachesInertialFlight()
    {
        // The sailing has to be windable all the way off, or the environment can
        // never actually be declined.
        FieldCoupling coupling = new(tuning);
        for (int step = 0; step < 120; step++)
        {
            coupling.Advance(Trim(-1.0), FixedStep);
        }

        Assert.Equal(0.0, coupling.Level, Tolerance);
    }

    [Fact]
    public void AnEmergencyUncoupleDumpsTheActuatorOnTheTurnItIsHeld()
    {
        FieldCoupling coupling = new(tuning);

        coupling.Advance(
            new FlightCommand(
                Throttle: 0.0,
                Turn: 0.0,
                CouplingTrim: 0.0,
                StabilizerEnabled: true,
                EmergencyUncouple: true,
                    RepairHeld: false),
            FixedStep);

        Assert.Equal(0.0, coupling.Level, Tolerance);
    }

    [Fact]
    public void OneAdmittedStepTravelsTheActuatorExactlyOnce()
    {
        // The actuator carries its own level, so a turn that catches up four
        // fixed steps lands on four steps of travel: each advance moves over the
        // interval it is handed, and reading the level moves nothing.
        FieldCoupling coupling = new(tuning);
        double perStep = FixedStep.TotalSeconds / tuning.TrimResponse.TotalSeconds;
        double expected = CradleLevel;
        for (int step = 0; step < 4; step++)
        {
            coupling.Advance(Trim(1.0), FixedStep);
            expected += perStep;
            Assert.Equal(expected, coupling.Level, Tolerance);
        }
    }

    [Fact]
    public void AResetReturnsTheActuatorToItsCradleSetting()
    {
        FieldCoupling coupling = new(tuning);
        coupling.Advance(Trim(-1.0), FixedStep);

        coupling.Reset();

        Assert.Equal(CradleLevel, coupling.Level, Tolerance);
    }

    [Fact]
    public void NoTrimDemandHoldsTheSettingWhereThePlayerLeftIt()
    {
        FieldCoupling coupling = new(tuning);
        coupling.Advance(Trim(1.0), FixedStep);
        double wound = coupling.Level;

        coupling.Advance(Trim(0.0), FixedStep);

        Assert.Equal(wound, coupling.Level, Tolerance);
    }

    private static FlightCommand Trim(double trimIntent) => new(
        Throttle: 0.0,
        Turn: 0.0,
        CouplingTrim: trimIntent,
        StabilizerEnabled: true,
        EmergencyUncouple: false,
            RepairHeld: false);
}
