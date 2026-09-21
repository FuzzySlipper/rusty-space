using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.ShipSystems.Tests;

/// <summary>
/// The hull as it is fitted: what the mounted hardware weighs, where each piece
/// pushes, and what a fit does when one side of it is worse than the other.
/// Nothing here is about whether the ship should turn — that is the
/// controller's — but about whether the machinery can do what it was told, and
/// what it costs when it cannot.
/// </summary>
public class InstalledShipTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    // No flow anywhere near the hull, so a load of nothing and no coupling push.
    private static readonly FieldSample Calm = new(
        FlowVelocity: PlanarVector.Zero,
        Intensity: 0.0,
        Gradient: FieldFlowGradient.Zero,
        Turbulence: PlanarVector.Zero);

    // Flow at full strength, which is what worn hardware responds to.
    private static readonly FieldSample HardFlow = new(
        FlowVelocity: new PlanarVector(0.0, 1.75),
        Intensity: 1.0,
        Gradient: FieldFlowGradient.Zero,
        Turbulence: PlanarVector.Zero);

    private const double DriveAuthority = 6.0;
    private const double StockAddedMass = 0.38;
    private const double StockAddedYawInertia = 0.06375;
    private const double WornHealth = 0.9;
    private const double NominalTrim = 0.6;
    private const double FullTrim = 1.0;
    private const double NoDemand = 0.0;
    private const double StockOversizedEmitterMount = 0.85;
    private const double WornPullAtFullLoad = 0.60;
    private const double RatedSideAuthority = 5.0;
    private const double DemandingTurn = 6.0;
    private const double WreckingImpulse = 24.0;

    [Fact]
    public void AFitWeighsWhatIsMountedOnIt()
    {
        InstalledShip stock = new(ShipLoadouts.Stock, DriveAuthority, SpaceTuning.Defaults.Damage);

        Assert.Equal(StockAddedMass, stock.AddedMass, 12);
        Assert.Equal(StockAddedYawInertia, stock.AddedYawInertia, 8);
    }

    [Fact]
    public void AMountedPartSwingsOutOfTheWayWhenTheHullTurns()
    {
        // The lever arm a part has is authored once, in the ship's own frame, and
        // has to arrive wherever the hull is pointing. A mount computed against
        // the world instead would keep pushing at the same compass bearing while
        // the ship spun beneath it.
        InstalledShip scavenged = new(ShipLoadouts.OversizedScavengedEmitter, DriveAuthority, SpaceTuning.Defaults.Damage);

        PlanarVector facingDownrange = scavenged.FieldCouplingCenter(0.0);
        PlanarVector facingStarboard = scavenged.FieldCouplingCenter(Math.PI / 2.0);

        Assert.Equal(StockOversizedEmitterMount, facingDownrange.X, 10);
        Assert.Equal(0.0, facingDownrange.Z, 10);
        Assert.Equal(0.0, facingStarboard.X, 10);
        Assert.Equal(StockOversizedEmitterMount, facingStarboard.Z, 10);
    }

    [Fact]
    public void AMatchedPairSharesATurnDemandEvenly()
    {
        InstalledShip stock = new(ShipLoadouts.Stock, DriveAuthority, SpaceTuning.Defaults.Damage);

        ShipEffort delivered = Drive(stock, demandedHeadingTorque: 3.0, NominalTrim, Calm, steps: 240);

        Assert.Equal(3.0, delivered.HeadingTorque, 3);
        Assert.Equal(0.0, delivered.HeadingAsymmetry, 9);
        Assert.False(delivered.HeadingSaturated);
    }

    [Fact]
    public void ASideThatCannotHoldItsShareShortfallsTheTurn()
    {
        // The demand does not get met, and nothing hides that by asking the other
        // side for more than it has either: what the pair can put on the keel is
        // the sum of what the two of them can actually put there, and the panel
        // says so. A side that has had its rating knocked off by a contact is what
        // a shortfall looks like on this hull; the stop each side is built against
        // stays its rated authority.
        InstalledShip limping = new(
            ShipLoadouts.Stock,
            DriveAuthority,
            SpaceTuning.Defaults.Damage);
        limping.TakeImpact(new PlanarVector(0.0, -1.0), WreckingImpulse);

        ShipEffort delivered = Drive(limping, demandedHeadingTorque: 6.0, NominalTrim, Calm, steps: 240);

        Assert.True(
            delivered.HeadingTorque < 4.0,
            $"expected the turn to come up short, got {delivered.HeadingTorque}");
        Assert.True(limping.PortStabilizer.Saturated && limping.StarboardStabilizer.Saturated);
        Assert.True(
            delivered.HeadingAsymmetry > 0.3,
            $"expected the two sides to disagree, asymmetry {delivered.HeadingAsymmetry}");
    }

    [Fact]
    public void WornHardwarePullsTheBowUnderLoadAndNotAtRest()
    {
        // A stabilizer that leaks does not do nothing at rest and something under
        // load by accident: the pull is authored against the load, so the ship
        // behaves normally on calm water and starts to fight the player in a
        // wake. That is what makes the defect findable rather than random.
        InstalledShip worn = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);

        ShipEffort atRest = Drive(worn, NoDemand, coupling: 0.0, Calm, steps: 240);

        Assert.Equal(0.0, atRest.WearPull, 12);
        Assert.Equal(0.0, atRest.HeadingTorque, 9);

        InstalledShip alsoWorn = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);
        ShipEffort underLoad = Drive(alsoWorn, NoDemand, FullTrim, HardFlow, steps: 480);

        Assert.Equal(WornPullAtFullLoad, underLoad.WearPull, 9);
        Assert.True(
            underLoad.HeadingTorque > WornPullAtFullLoad - 0.01,
            $"expected the worn side's pull to arrive through its actuator, got {underLoad.HeadingTorque}");
    }

    [Fact]
    public void AWornSideStillReachesTheSameStopAHealthyOneDoes()
    {
        // What makes a tired side a different maneuver is how it gets where it is
        // told and what it pulls on the way, not a ceiling clipped below what it is
        // rated for. Asked for more than either has, the worn side arrives at the
        // same stop a healthy one arrives at — later, and ringing on the way.
        InstalledShip healthy = new(ShipLoadouts.Stock, DriveAuthority, SpaceTuning.Defaults.Damage);
        InstalledShip worn = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);

        // Part way through the move the tired side is still behind, which is the
        // wear a player feels and the reason the fit is called worn.
        Drive(healthy, DemandingTurn, FullTrim, HardFlow, steps: 12);
        Drive(worn, DemandingTurn, FullTrim, HardFlow, steps: 12);
        Assert.True(
            Math.Abs(healthy.StarboardStabilizer.ActuatorValue)
                > Math.Abs(worn.StarboardStabilizer.ActuatorValue) + 0.2,
            $"expected the tired side to lag on the way to the stop, got "
                + $"{worn.StarboardStabilizer.ActuatorValue} against "
                + $"{healthy.StarboardStabilizer.ActuatorValue}");

        // Sustained at maximum demand, both are at the stop and it is the same stop.
        Drive(healthy, DemandingTurn, FullTrim, HardFlow, steps: 300);
        Drive(worn, DemandingTurn, FullTrim, HardFlow, steps: 300);

        Assert.Equal(RatedSideAuthority, healthy.StarboardStabilizer.ActuatorLimit, 9);
        Assert.Equal(RatedSideAuthority, worn.StarboardStabilizer.ActuatorLimit, 9);
        Assert.True(healthy.StarboardStabilizer.Saturated && worn.StarboardStabilizer.Saturated);
        Assert.Equal(
            Math.Abs(healthy.StarboardStabilizer.ActuatorValue),
            Math.Abs(worn.StarboardStabilizer.ActuatorValue),
            9);
    }

    [Fact]
    public void AWorkedSideRingsUnderLoadWhereItWouldSettleOnCalmWater()
    {
        // Same demand, same hardware, two loads: past the wear threshold the
        // response rings instead of settling. The onset is a load a player can
        // find, and the ring is the same every time it is reached.
        InstalledShip calm = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);
        InstalledShip loaded = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);

        double calmPeak = DriveToPeak(calm, demandedHeadingTorque: 2.0, NominalTrim, Calm, steps: 90);
        double loadedPeak = DriveToPeak(loaded, demandedHeadingTorque: 2.0, FullTrim, HardFlow, steps: 90);

        Assert.True(
            Math.Abs(calmPeak - 2.0) < 0.15,
            $"expected a healthy response at calm load, peaked at {calmPeak}");
        Assert.True(
            loadedPeak > calmPeak + 0.3,
            $"expected the loaded side to overshoot past the settled one, peaks {loadedPeak} against {calmPeak}");
    }

    [Fact]
    public void TheEmitterDecidesHowMuchOfTheFlowTheHullFeels()
    {
        // The coupling actuator is the player's demand; what the hull feels is
        // that demand through the hardware fitted to answer it. A coil rated
        // above the hull's own trim catches more of the same flow, which is the
        // whole reason anyone would wire in something that does not belong.
        InstalledShip stock = new(ShipLoadouts.Stock, DriveAuthority, SpaceTuning.Defaults.Damage);
        InstalledShip scavenged = new(ShipLoadouts.OversizedScavengedEmitter, DriveAuthority, SpaceTuning.Defaults.Damage);

        ShipEffort onSpec = Drive(stock, NoDemand, NominalTrim, Calm, steps: 480);
        ShipEffort oversized = Drive(scavenged, NoDemand, NominalTrim, Calm, steps: 480);

        Assert.Equal(NominalTrim, onSpec.Coupling, 6);
        Assert.True(
            oversized.Coupling > NominalTrim * 1.5,
            $"expected the oversized coil to catch more of the flow, got {oversized.Coupling}");
    }

    [Fact]
    public void AHeadingEffectorOnTheCenterlineIsRefused()
    {
        // An effector mounted on the keel has no lever and can never yaw the
        // ship, so a fit like that is a mistake in the tuning rather than a ship.
        ShipLoadout broken = ShipLoadouts.Stock with
        {
            StarboardStabilizer = ShipLoadouts.Stock.StarboardStabilizer with
            {
                Mount = new PlanarVector(-0.20, 0.0),
            },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => broken.Validate());
    }

    [Fact]
    public void AWorkedPartWarmsAndAPartLeftAloneCoolsDown()
    {
        InstalledShip ship = new(ShipLoadouts.Stock, DriveAuthority, SpaceTuning.Defaults.Damage);

        Drive(ship, demandedHeadingTorque: 4.0, NominalTrim, Calm, steps: 120);
        double worked = ship.StarboardStabilizer.Temperature;

        Drive(ship, NoDemand, coupling: 0.0, Calm, steps: 600);
        double rested = ship.StarboardStabilizer.Temperature;

        Assert.True(worked > 0.05, $"expected the worked side to warm, reached {worked}");
        Assert.True(rested < worked, $"expected the rested side to cool, {rested} against {worked}");
    }

    [Fact]
    public void EveryFittedPartIsNamedAndAccountedFor()
    {
        InstalledShip worn = new(ShipLoadouts.DamagedStabilizer, DriveAuthority, SpaceTuning.Defaults.Damage);

        Assert.Equal("emitter-stock", worn.Emitter.Id.Value);
        Assert.Equal("drive-stock", worn.MainDrive.Id.Value);
        Assert.Equal("stabilizer-port", worn.PortStabilizer.Id.Value);
        Assert.Equal("stabilizer-starboard-worn", worn.StarboardStabilizer.Id.Value);
        Assert.Equal(1.0, worn.PortStabilizer.Health, 12);
        Assert.Equal(WornHealth, worn.StarboardStabilizer.Health, 12);
        Assert.Equal(PartRole.FieldEmitter, worn.Emitter.Definition.Role);
    }

    private static ShipEffort Drive(
        InstalledShip ship,
        double demandedHeadingTorque,
        double coupling,
        FieldSample field,
        int steps)
    {
        ShipEffort last = default;
        for (int step = 0; step < steps; step++)
        {
            last = ship.Advance(PlanarVector.Zero, demandedHeadingTorque, coupling, field, FixedStep);
        }

        return last;
    }

    private static double DriveToPeak(
        InstalledShip ship,
        double demandedHeadingTorque,
        double coupling,
        FieldSample field,
        int steps)
    {
        double peak = 0.0;
        for (int step = 0; step < steps; step++)
        {
            ShipEffort delivered = ship.Advance(
                PlanarVector.Zero,
                demandedHeadingTorque,
                coupling,
                field,
                FixedStep);
            peak = Math.Max(peak, Math.Abs(delivered.HeadingTorque));
        }

        return peak;
    }
}
