using Rusty.Space.Product.Navigation;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Telemetry is derived from the readouts either side of an Engine step, on the
/// fixed-step clock. These pin the frame the numbers are reported in and the
/// window they are divided by, which is what makes them comparable across a
/// catch-up turn.
/// </summary>
public class FlightTelemetryTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    // A fixed step is carried as a TimeSpan, so it lands on a whole number of
    // 100ns ticks; derived rates can only be compared to that resolution.
    private const double RateTolerance = 0.01;

    [Fact]
    public void AccelerationIsReportedInTheShipsOwnFrame()
    {
        FlightTelemetry telemetry = new();

        telemetry.Capture(
            Frame(headingRadians: Math.PI / 2.0, PlanarVector.Zero, 0.0),
            Readout(headingRadians: Math.PI / 2.0, new PlanarVector(3.0, 0.0), 0.0),
            FlightForces.Zero,
            Control(),
            7UL,
            1U,
            FixedStep);

        // The ship points along +Z and the velocity change is purely along
        // world +X, so all of it reads as lateral and none as forward.
        Assert.Equal(0.0, telemetry.Current.ForwardAcceleration, 9);
        Assert.Equal(-180.0, telemetry.Current.LateralAcceleration, RateTolerance);
    }

    [Fact]
    public void ACoastingShipReportsNoAcceleration()
    {
        FlightTelemetry telemetry = new();
        PlanarVector drift = new(4.0, -1.5);

        telemetry.Capture(
            Frame(0.0, drift, 0.0),
            Readout(0.0, drift, 0.0),
            FlightForces.Zero,
            Control(),
            1UL,
            1U,
            FixedStep);

        Assert.Equal(0.0, telemetry.Current.ForwardAcceleration, 12);
        Assert.Equal(0.0, telemetry.Current.LateralAcceleration, 12);
        Assert.Equal(0.0, telemetry.Current.YawAcceleration, 12);
    }

    [Fact]
    public void ACatchUpTurnDividesByTheWholeAdmittedWindow()
    {
        FlightTelemetry telemetry = new();
        PlanarVector velocityChange = new(2.0, 0.0);

        telemetry.Capture(
            Frame(0.0, PlanarVector.Zero, 0.0),
            Readout(0.0, velocityChange, 0.0),
            FlightForces.Zero,
            Control(),
            2UL,
            2U,
            FixedStep);

        // The same change spread over one admitted step would read 120; the
        // window is the admitted steps, not a single fixed step.
        Assert.Equal(60.0, telemetry.Current.ForwardAcceleration, RateTolerance);
        Assert.Equal(2U, telemetry.Current.AdmittedSteps);
    }

    [Fact]
    public void ActuatorEffortAndSaturationAreCarriedThroughUntouched()
    {
        FlightTelemetry telemetry = new();

        telemetry.Capture(
            Frame(0.0, PlanarVector.Zero, 0.0),
            Readout(0.0, PlanarVector.Zero, 0.0),
            FlightForces.Zero,
            Control(driveEffort: 0.75, steeringEffort: 1.0, driveSaturated: false, steeringSaturated: true),
            3UL,
            1U,
            FixedStep);

        Assert.Equal(0.75, telemetry.Current.DriveEffort, 12);
        Assert.Equal(1.0, telemetry.Current.SteeringEffort, 12);
        Assert.False(telemetry.Current.DriveSaturated);
        Assert.True(telemetry.Current.SteeringSaturated);
    }

    [Fact]
    public void FieldLoadReportsThePushTheFieldIsActuallyApplying()
    {
        FlightTelemetry telemetry = new();

        telemetry.Capture(
            Frame(0.0, PlanarVector.Zero, 0.0),
            Readout(0.0, PlanarVector.Zero, 0.0),
            FlightForces.Zero with { Field = new FlightWrench(new PlanarVector(0.0, -4.0), 0.0) },
            Control(),
            1UL,
            1U,
            FixedStep);

        Assert.Equal(4.0, telemetry.Current.FieldLoad, 12);
    }

    [Fact]
    public void ResetLeavesNothingStaleBehind()
    {
        FlightTelemetry telemetry = new();
        telemetry.Capture(
            Frame(0.0, PlanarVector.Zero, 0.0),
            Readout(0.0, new PlanarVector(0.0, 9.0), 0.0),
            FlightForces.Zero,
            Control(driveEffort: 1.0, steeringEffort: 1.0, driveSaturated: true, steeringSaturated: true),
            9UL,
            1U,
            FixedStep);

        telemetry.Reset();

        Assert.Equal(0.0, telemetry.Current.ForwardAcceleration, 12);
        Assert.Equal(0.0, telemetry.Current.DriveEffort, 12);
        Assert.False(telemetry.Current.DriveSaturated);
        Assert.Equal(0UL, telemetry.Current.FixedStepCount);
    }

    [Fact]
    public void ANonPositiveFixedStepIsRejected()
    {
        FlightTelemetry telemetry = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => telemetry.Capture(
            Frame(0.0, PlanarVector.Zero, 0.0),
            Readout(0.0, PlanarVector.Zero, 0.0),
            FlightForces.Zero,
            Control(),
            1UL,
            1U,
            TimeSpan.Zero));
    }

    private static FlightBodyState Frame(double headingRadians, PlanarVector velocity, double angularVelocity) =>
        new(PlanarVector.Zero, headingRadians, velocity, angularVelocity);

    private static FlightReadout Readout(double headingRadians, PlanarVector velocity, double angularVelocity) =>
        new(PlanarVector.Zero, headingRadians, velocity, angularVelocity, 2.0, 1.0);

    private static FlightControlOutput Control(
        double driveEffort = 0.0,
        double steeringEffort = 0.0,
        bool driveSaturated = false,
        bool steeringSaturated = false) => new(
            FlightWrench.Zero,
            FlightWrench.Zero,
            0.0,
            driveEffort,
            steeringEffort,
            driveSaturated,
            steeringSaturated);
}
