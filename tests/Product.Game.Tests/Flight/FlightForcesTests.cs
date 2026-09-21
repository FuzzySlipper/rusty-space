using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// The contribution table is the product's answer to "why did the ship move".
/// These pin that the named sources are the whole story and that the total is
/// their join, so a source cannot be silently dropped or double-counted.
/// </summary>
public class FlightForcesTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void EveryNamedSourceReachesTheTotal()
    {
        FlightWrench drive = new(new PlanarVector(1.0, 0.5), 0.25);
        FlightWrench steering = new(new PlanarVector(0.0, 0.0), -1.5);
        FlightWrench field = new(new PlanarVector(-2.0, 3.0), 0.0);
        FlightWrench gentle = new(new PlanarVector(0.75, -0.25), 0.1);
        FlightWrench swift = new(new PlanarVector(-0.5, 1.25), -0.2);
        FlightWrench orbital = new(new PlanarVector(2.5, -1.0), 0.05);

        FlightWrench total = new FlightForces(
            drive,
            steering,
            field,
            gentle,
            swift,
            orbital).Total;

        Assert.Equal(1.75, total.Force.X, Tolerance);
        Assert.Equal(3.5, total.Force.Z, Tolerance);
        Assert.Equal(-1.3, total.YawTorque, 12);
    }

    [Fact]
    public void AWornSidesStandingPullReachesTheHullExactlyOnce()
    {
        // The standing pull a tired effector puts on the bow while it is loaded is
        // hardware work: it reaches the hull as part of what that side's actuator
        // delivered, through its own response, damping, and stop. The force model
        // reports the pull inside the steering it was handed and adds nothing of its
        // own, so a hull asked for no turn at all under full load is turned by
        // exactly the pull the hardware reached — not by that pull a second time,
        // which would arrive having passed through no response whatsoever.
        InstalledShip worn = new(
            ShipLoadouts.DamagedStabilizer,
            DriveAuthority,
            SpaceTuning.Defaults.Damage);
        ShipEffort effort = worn.Advance(
            PlanarVector.Zero,
            NoDemand,
            FullCoupling,
            HardFlow,
            FixedStep);

        HullForceModel model = new(
            new FieldResponse(SpaceTuning.Defaults.Field),
            new DriftCurrent(SpaceTuning.Defaults.GentleCurrent),
            new DriftCurrent(SpaceTuning.Defaults.SwiftCurrent),
            new OrbitalGravity(SpaceTuning.Defaults.Orbital));
        FlightForces forces = model.Resolve(
            new FlightBodyState(PlanarVector.Zero, 0.0, PlanarVector.Zero, 0.0),
            worn,
            HardFlow,
            effort,
            StockMass);

        Assert.True(effort.WearPull > 0.0);
        Assert.Equal(effort.HeadingTorque, forces.Steering.YawTorque, Tolerance);

        double environment = forces.Field.YawTorque
            + forces.GentleCurrent.YawTorque
            + forces.SwiftCurrent.YawTorque
            + forces.OrbitalPull.YawTorque;
        Assert.Equal(effort.HeadingTorque + environment, forces.Total.YawTorque, Tolerance);
    }

    private static readonly FieldSample HardFlow = new(
        FlowVelocity: new PlanarVector(0.0, 1.75),
        Intensity: 1.0,
        Gradient: FieldFlowGradient.Zero,
        Turbulence: PlanarVector.Zero);

    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    private const double DriveAuthority = 6.0;
    private const double NoDemand = 0.0;
    private const double FullCoupling = 1.0;
    private const double StockMass = 1.62 + 0.38;

    [Fact]
    public void ASourceLeftAtZeroLeavesTheTotalAtTheOtherSources()
    {
        FlightWrench drive = new(PlanarVector.UnitX, 0.0);
        FlightForces forces = FlightForces.Zero with { MainDrive = drive };

        Assert.Equal(drive.Force.X, forces.Total.Force.X, Tolerance);
        Assert.Equal(drive.Force.Z, forces.Total.Force.Z, Tolerance);
        Assert.Equal(drive.YawTorque, forces.Total.YawTorque, Tolerance);
    }

    [Fact]
    public void WrenchesJoinBothPushAndTurn()
    {
        FlightWrench joined = new FlightWrench(new PlanarVector(1.0, 2.0), 3.0)
            + new FlightWrench(new PlanarVector(-4.0, 6.0), -1.0);

        Assert.Equal(-3.0, joined.Force.X, Tolerance);
        Assert.Equal(8.0, joined.Force.Z, Tolerance);
        Assert.Equal(2.0, joined.YawTorque, Tolerance);
    }
}
