using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.ShipSystems.Tests;

public sealed class RecoveryEnvelopeTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1.0 / 60.0);
    private static readonly FieldSample Calm = new(PlanarVector.Zero, 0.0, FieldFlowGradient.Zero, PlanarVector.Zero);

    [Fact]
    public void BurstReserveWarnsBeforeItFadesAndNeverTakesTheDriveAway()
    {
        DriveReserve reserve = new(SpaceTuning.Defaults.Reserve);
        bool warnedAtFullOutput = false;
        for (int i = 0; i < 1200; i++)
        {
            reserve.Advance(1.0, Step);
            warnedAtFullOutput |= reserve.Low && reserve.OutputFraction == 1.0;
            Assert.InRange(reserve.Fraction, 0.0, 1.0);
            Assert.InRange(reserve.OutputFraction, SpaceTuning.Defaults.Reserve.SustainableOutputFraction, 1.0);
        }
        Assert.True(warnedAtFullOutput);
        Assert.Equal(0.0, reserve.Fraction);
        Assert.Equal(SpaceTuning.Defaults.Reserve.SustainableOutputFraction, reserve.OutputFraction);

        for (int i = 0; i < 1200; i++) reserve.Advance(0.0, Step);
        Assert.Equal(1.0, reserve.Fraction);
        Assert.False(reserve.Low);
        Assert.Equal(1.0, reserve.OutputFraction);
    }

    [Fact]
    public void SustainableManoeuvresRefillReserveAndUnadmittedTimeSpendsNothing()
    {
        DriveReserve reserve = new(SpaceTuning.Defaults.Reserve);
        reserve.Advance(1.0, TimeSpan.FromSeconds(6.0));
        double low = reserve.Fraction;
        reserve.Advance(1.0, TimeSpan.Zero);
        Assert.Equal(low, reserve.Fraction);
        Assert.Throws<ArgumentOutOfRangeException>(() => reserve.Advance(1.0, TimeSpan.FromSeconds(-1.0)));
        Assert.Equal(low, reserve.Fraction);
        reserve.Advance(SpaceTuning.Defaults.Reserve.SustainableOutputFraction, TimeSpan.FromSeconds(2.0));
        Assert.True(reserve.Fraction > low);
    }

    [Fact]
    public void HeatWarningLeadsOutputLossAndCoastingRestoresTheSameActuator()
    {
        InstalledPart drive = Drive();
        bool warnedAtFullOutput = false;
        for (int i = 0; i < 1200; i++)
        {
            drive.Advance(6.0, 1.0, Step);
            warnedAtFullOutput |= drive.HeatWarning && drive.ThermalAuthority == 1.0;
        }
        Assert.True(warnedAtFullOutput);
        Assert.True(drive.Temperature > SpaceTuning.Defaults.Thermal.DeratingTemperature);
        Assert.InRange(drive.ActuatorValue, 0.1, 5.5);
        Assert.True(drive.ThermalAuthority >= SpaceTuning.Defaults.Thermal.MinimumOutputFraction);
        double hot = drive.Temperature;
        for (int i = 0; i < 1200; i++) drive.Advance(0.0, 0.0, Step);
        Assert.True(drive.Temperature < hot);
        Assert.False(drive.HeatWarning);
        Assert.Equal(1.0, drive.ThermalAuthority);
        for (int i = 0; i < 60; i++) drive.Advance(6.0, 1.0, Step);
        Assert.True(drive.ActuatorValue > 5.9);
    }

    [Fact]
    public void ImpactSlowsTheExistingResponseAndDoesNotDeleteItsAuthority()
    {
        InstalledPart healthy = Drive();
        InstalledPart dented = Drive();
        dented.TakeImpact(40.0);
        for (int i = 0; i < 6; i++)
        {
            healthy.Advance(6.0, 1.0, Step);
            dented.Advance(6.0, 1.0, Step);
        }
        Assert.True(dented.ActuatorValue < healthy.ActuatorValue);
        Assert.True(dented.DeliveryFraction > 0.0);
        Assert.True(dented.Health < healthy.Health);
    }

    [Fact]
    public void ADepletedHotDamagedShipStillAnswersSteeringAndRecoversItsBurst()
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        InstalledShip ship = new(tuning.Ship, tuning.Flight.MaximumThrust, tuning.Damage, tuning.Thermal, tuning.Reserve);
        ship.TakeImpact(new PlanarVector(1.0, 0.0), 40.0); // aft-mounted main drive
        ShipEffort effort = default;
        for (int i = 0; i < 1200; i++)
            effort = ship.Advance(new PlanarVector(6.0, 0.0), 1.0, 0.0, Calm, Step);
        Assert.True(effort.DriveForce.Magnitude > 0.0);
        Assert.True(effort.DriveForce.Magnitude < 6.0);
        Assert.True(effort.HeadingTorque > 0.5);
        Assert.InRange(effort.Systems.ReserveFraction, 0.0, 0.01);
        double dent = ship.MainDrive.Health;

        for (int i = 0; i < 1200; i++)
            ship.Advance(PlanarVector.Zero, 0.0, 0.0, Calm, Step);
        Assert.Equal(1.0, ship.ReadSystems().ReserveFraction);
        Assert.Equal(dent, ship.MainDrive.Health);
        Assert.False(ship.ReadSystems().HeatWarning);
    }

    [Fact]
    public void ThermalEnvelopeRequiresAnEarlyWarningAndAViableOutputFloor()
    {
        PartThermalTuning thermal = SpaceTuning.Defaults.Thermal;
        Assert.Throws<ArgumentOutOfRangeException>(() => (thermal with { WarningTemperature = thermal.DeratingTemperature }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (thermal with { MinimumOutputFraction = 0.0 }).Validate());
        DriveReserveTuning reserve = SpaceTuning.Defaults.Reserve;
        Assert.Throws<ArgumentOutOfRangeException>(() => (reserve with { WarningFraction = reserve.DeratingFraction }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (reserve with { SustainableOutputFraction = 0.0 }).Validate());
    }

    [Fact]
    public void CouplingDoesNotSpendDriveReserveOrReportAHotIdleDrive()
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        InstalledShip ship = new(tuning.Ship, tuning.Flight.MaximumThrust, tuning.Damage, tuning.Thermal, tuning.Reserve);
        for (int i = 0; i < 1200; i++)
            ship.Advance(PlanarVector.Zero, 0.0, 1.0, Calm, Step);
        Assert.True(ship.Emitter.HeatWarning);
        Assert.Equal(1.0, ship.ReadSystems().ReserveFraction);
        Assert.Equal(0.0, ship.ReadSystems().DriveTemperature);
        Assert.False(ship.ReadSystems().HeatWarning);
    }

    private static InstalledPart Drive()
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        return new InstalledPart(tuning.Ship.Drive,
            new ActuatorResponse(tuning.Ship.Drive.Response, tuning.Flight.MaximumThrust), tuning.Damage, tuning.Thermal);
    }
}
