using Rusty.Space.Product.Navigation;
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
        FlightWrench damage = new(new PlanarVector(-0.125, -0.375), 0.02);

        FlightWrench total = new FlightForces(
            drive,
            steering,
            field,
            gentle,
            swift,
            orbital,
            damage).Total;

        Assert.Equal(1.625, total.Force.X, Tolerance);
        Assert.Equal(3.125, total.Force.Z, Tolerance);
        Assert.Equal(-1.28, total.TorqueY, 12);
    }

    [Fact]
    public void ASourceLeftAtZeroLeavesTheTotalAtTheOtherSources()
    {
        FlightWrench drive = new(PlanarVector.UnitX, 0.0);
        FlightForces forces = FlightForces.Zero with { MainDrive = drive };

        Assert.Equal(drive.Force.X, forces.Total.Force.X, Tolerance);
        Assert.Equal(drive.Force.Z, forces.Total.Force.Z, Tolerance);
        Assert.Equal(drive.TorqueY, forces.Total.TorqueY, Tolerance);
    }

    [Fact]
    public void WrenchesJoinBothPushAndTurn()
    {
        FlightWrench joined = new FlightWrench(new PlanarVector(1.0, 2.0), 3.0)
            + new FlightWrench(new PlanarVector(-4.0, 6.0), -1.0);

        Assert.Equal(-3.0, joined.Force.X, Tolerance);
        Assert.Equal(8.0, joined.Force.Z, Tolerance);
        Assert.Equal(2.0, joined.TorqueY, Tolerance);
    }
}
