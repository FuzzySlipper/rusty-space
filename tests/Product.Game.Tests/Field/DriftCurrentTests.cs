using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Field.Tests;

/// <summary>
/// A drift current is a finite, readable river. These pin the difference
/// between "the river pushes" and "space drags": outside the band nothing
/// happens, and a ship already moving with the river feels nothing at all.
/// </summary>
public class DriftCurrentTests
{
    private const double ShipMass = 2.0;
    private const double FullyCoupled = 1.0;

    private readonly DriftCurrentTuning tuning = SpaceTuning.Defaults.GentleCurrent;

    [Fact]
    public void WellOutsideTheBandThereIsNoPush()
    {
        DriftCurrent current = new(tuning);

        Flight.FlightWrench wrench = current.Resolve(
            tuning.Center + new PlanarVector(0.0, 60.0),
            PlanarVector.Zero,
            mass: ShipMass,
            coupling: FullyCoupled);

        Assert.Equal(0.0, wrench.Force.X, 12);
        Assert.Equal(0.0, wrench.Force.Z, 12);
    }

    [Fact]
    public void AParkedShipAtTheCentreOfTheBandIsCarriedWithTheRiver()
    {
        DriftCurrent current = new(tuning);

        Flight.FlightWrench wrench = current.Resolve(
            tuning.Center,
            PlanarVector.Zero,
            mass: ShipMass,
            coupling: FullyCoupled);

        Assert.True(wrench.Force.X > 0.0);
        Assert.Equal(0.0, wrench.YawTorque, 12);
    }

    [Fact]
    public void AShipAlreadyAtTheLocalFlowSpeedFeelsNothing()
    {
        // This is the invariant that keeps a current from becoming universal
        // drag: matching the river ends the push, while moving through open
        // space at the same speed would keep decelerating under drag.
        DriftCurrent current = new(tuning);

        Flight.FlightWrench wrench = current.Resolve(
            tuning.Center,
            current.FlowDirection.Scale(tuning.FlowSpeed),
            mass: ShipMass,
            coupling: FullyCoupled);

        Assert.Equal(0.0, wrench.Force.X, 9);
        Assert.Equal(0.0, wrench.Force.Z, 9);
    }

    [Fact]
    public void AShipFightingUpstreamIsPushedBackWithinTheBandsLimit()
    {
        DriftCurrent current = new(tuning);

        Flight.FlightWrench wrench = current.Resolve(
            tuning.Center,
            current.FlowDirection.Scale(-50.0),
            mass: ShipMass,
            coupling: FullyCoupled);

        Assert.True(wrench.Force.X > 0.0);
        Assert.True(wrench.Force.Magnitude <= tuning.MaximumForce + 1e-9);
    }

    [Fact]
    public void AnUncoupledShipKeepsItsLineStraightThroughTheBand()
    {
        // Coupling is the ship's choice. A band that would bend a coupled hull
        // leaves nothing at all on one that has wound itself off, so coasting
        // through a river at zero coupling is the same coast as open space.
        DriftCurrent current = new(tuning);

        Flight.FlightWrench wrench = current.Resolve(
            tuning.Center,
            PlanarVector.Zero,
            mass: ShipMass,
            coupling: 0.0);

        Assert.Equal(0.0, wrench.Force.X, 12);
        Assert.Equal(0.0, wrench.Force.Z, 12);
    }

    [Fact]
    public void ThePushRisesMonotonicallyWithCoupling()
    {
        DriftCurrent current = new(tuning);

        double quarter = current.Resolve(
            tuning.Center, PlanarVector.Zero, ShipMass, 0.25).Force.Magnitude;
        double half = current.Resolve(
            tuning.Center, PlanarVector.Zero, ShipMass, 0.5).Force.Magnitude;
        double full = current.Resolve(
            tuning.Center, PlanarVector.Zero, ShipMass, FullyCoupled).Force.Magnitude;

        Assert.True(quarter < half);
        Assert.True(half < full);
        Assert.Equal(2.0 * quarter, half, 9);
    }

    [Fact]
    public void ThePushFadesSmoothlyAcrossTheBandsEdge()
    {
        DriftCurrent current = new(tuning);
        double inside = current.Resolve(
            tuning.Center, PlanarVector.Zero, ShipMass, FullyCoupled).Force.Magnitude;
        double halfWidthOut = current.Resolve(
            tuning.Center + new PlanarVector(0.0, tuning.Width / 2.0),
            PlanarVector.Zero,
            ShipMass,
            FullyCoupled).Force.Magnitude;
        double twoWidthsOut = current.Resolve(
            tuning.Center + new PlanarVector(0.0, tuning.Width * 2.0),
            PlanarVector.Zero,
            ShipMass,
            FullyCoupled).Force.Magnitude;

        Assert.True(inside > halfWidthOut);
        Assert.True(halfWidthOut > twoWidthsOut);
    }
}
