using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.ShipSystems.Tests;

/// <summary>
/// What the hardware remembers after the Engine says the hull arrived at
/// something. A brush is a push and nothing more; a hard arrival costs the part
/// that caught it, and hard enough leaves something jammed off where it was told
/// to be — a pull the player has to fly against until the crew has had time on
/// it. Nothing here ever takes a hull out of the fight entirely.
/// </summary>
public class HullImpactTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);

    // No flow, so nothing but the demands put on the hardware reach the hull.
    private static readonly FieldSample Calm = new(
        FlowVelocity: PlanarVector.Zero,
        Intensity: 0.0,
        Gradient: FieldFlowGradient.Zero,
        Turbulence: PlanarVector.Zero);

    private static readonly DamageTuning Damage = SpaceTuning.Defaults.Damage;

    // Impulses in newton-seconds against a hull a shade over two kilograms: a
    // brush at half a metre a second, a clunk at four, and an arrival at twenty.
    private const double Brush = 1.0;
    private const double Clunk = 8.0;
    private const double Wrecking = 40.0;
    private const double NoDemand = 0.0;
    private const int SettlingSteps = 180;

    [Fact]
    public void ABrushAgainstSomethingIsAPushAndNothingElse()
    {
        InstalledShip ship = Stock();

        HullDamage struck = ship.TakeImpact(new PlanarVector(0.0, -1.0), Brush);

        Assert.Equal(0.0, struck.HealthLost, 10);
        Assert.False(ship.StarboardStabilizer.OutOfTrim);
        Assert.Equal(1.0, ship.StarboardStabilizer.Health, 10);
    }

    [Fact]
    public void AHardArrivalOnOneQuarterKnocksThatSidesEffectorOutOfTrim()
    {
        // The Engine's impulse points out of the hull, away from what produced it,
        // so a push toward port was a contact with the starboard quarter, and the
        // starboard effector is the one with something jammed in it.
        InstalledShip ship = Stock();

        HullDamage struck = ship.TakeImpact(new PlanarVector(0.0, -1.0), Clunk);

        Assert.Equal(SpaceTuning.Defaults.Ship.StarboardStabilizer.Id, struck.Part);
        Assert.True(struck.HealthLost > 0.0);
        Assert.True(struck.KnockedOutOfTrim);
        Assert.True(ship.StarboardStabilizer.OutOfTrim);
        Assert.False(ship.PortStabilizer.OutOfTrim);
    }

    [Fact]
    public void AJammedVanePullsTheHullWithNothingAskedOfIt()
    {
        InstalledShip hit = Stock();
        InstalledShip untouched = Stock();
        hit.TakeImpact(new PlanarVector(0.0, -1.0), Clunk);

        double pulled = AdvanceWithNoDemand(hit).HeadingTorque;
        double held = AdvanceWithNoDemand(untouched).HeadingTorque;

        Assert.Equal(NoDemand, held, 10);
        Assert.True(pulled > 0.05);

        // The mirrored side fails the other way: the same arrival on the port
        // quarter turns the hull about the keel in the opposite sense.
        InstalledShip port = Stock();
        port.TakeImpact(new PlanarVector(0.0, 1.0), Clunk);

        Assert.True(AdvanceWithNoDemand(port).HeadingTorque < -0.05);
    }

    [Fact]
    public void AHitTakesRatingOffTheHardwareThatCaughtIt()
    {
        // A bow-first arrival goes to whatever is hung forward. The emitter still
        // closes its gate as far as it is told; what it catches with is less.
        InstalledShip hit = Stock();
        InstalledShip untouched = Stock();
        HullDamage struck = hit.TakeImpact(new PlanarVector(-1.0, 0.0), Clunk);

        Assert.Equal(SpaceTuning.Defaults.Ship.Emitter.Id, struck.Part);

        double caught = ThrottleUp(hit).Coupling;
        double full = ThrottleUp(untouched).Coupling;

        Assert.True(caught < full);
        Assert.True(caught > 0.0);
    }

    [Fact]
    public void TheHealthFloorKeepsAWorkedOverHullFlying()
    {
        // Physics mistakes are meant to open a situation, not end one: whatever
        // else a hull has left, its controls still answer, because health stops
        // falling somewhere above nothing.
        InstalledShip ship = Stock();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            ship.TakeImpact(new PlanarVector(0.0, -1.0), Wrecking);
        }

        Assert.True(ship.StarboardStabilizer.Health >= Damage.MinimumHealth);
        Assert.True(ship.StarboardStabilizer.Health > 0.0);
        Assert.True(ThrottleUp(ship).Coupling > 0.0);
        Assert.True(AdvanceWithNoDemand(ship).HeadingTorque > 0.0);
    }

    [Fact]
    public void APatchHeldLongEnoughLetsAJamGoAndLeavesTheDent()
    {
        InstalledShip ship = Stock();
        ship.TakeImpact(new PlanarVector(0.0, -1.0), Clunk);
        double dented = ship.StarboardStabilizer.Health;
        Assert.True(ship.StarboardStabilizer.OutOfTrim);

        HoldPatch(ship, Damage.RepairTime);

        Assert.False(ship.StarboardStabilizer.OutOfTrim);
        Assert.Equal(0.0, ship.StarboardStabilizer.TrimOffset, 10);
        Assert.Equal(NoDemand, AdvanceWithNoDemand(ship).HeadingTorque, 10);

        // What the hit cost stays cost. A patch holds a mechanism where it
        // belongs; it does not un-bend it.
        Assert.True(dented < 1.0);
        Assert.Equal(dented, ship.StarboardStabilizer.Health, 10);
    }

    [Fact]
    public void APatchLetGoOfBeforeTheLatchReleasesIsAbandoned()
    {
        InstalledShip ship = Stock();
        ship.TakeImpact(new PlanarVector(0.0, -1.0), Clunk);

        HoldPatch(ship, TimeSpan.FromSeconds(0.4));
        Assert.True(ship.StarboardStabilizer.OutOfTrim);
        Assert.True(ship.StarboardStabilizer.RepairProgress > 0.0);

        ship.AdvanceRepairs(patchHeld: false, FixedStep);
        Assert.Equal(0.0, ship.StarboardStabilizer.RepairProgress, 10);

        // The work done before the patch was let go of counts for nothing, and
        // the latch is still there.
        Assert.True(ship.StarboardStabilizer.OutOfTrim);
    }

    [Fact]
    public void PuttingTheHullBackOnTheLineDoesNotSendTheCrewOutWithAPatchKit()
    {
        // A reset rebuilds what the Engine simulates. The hardware remembers what
        // it has been through, which is the difference between a reset and a fix.
        InstalledShip ship = Stock();
        ship.TakeImpact(new PlanarVector(0.0, -1.0), Clunk);
        double afterTheHit = ship.StarboardStabilizer.Health;

        ship.Reset();

        Assert.True(ship.StarboardStabilizer.OutOfTrim);
        Assert.Equal(afterTheHit, ship.StarboardStabilizer.Health, 10);
    }

    [Fact]
    public void ADentedSideMovesWhereThePairHoldsTheHullFrom()
    {
        // The attitude hold acts between whatever the two sides can actually
        // deliver. Weaken one and the hull is held from wherever the strong side
        // is, which is a different place to be pushed from.
        InstalledShip ship = Stock();
        double before = ship.StabilizationCenter(0.0).Z;

        // A dent that stops short of jamming anything, so this is about the
        // rating alone.
        ship.TakeImpact(new PlanarVector(0.0, 1.0), Damage.GlancingImpulse + 0.8);

        Assert.True(before < ship.StabilizationCenter(0.0).Z);
        Assert.False(ship.PortStabilizer.OutOfTrim);
    }

    private static InstalledShip Stock() => new(
        SpaceTuning.Defaults.Ship,
        SpaceTuning.Defaults.Flight.MaximumThrust,
        SpaceTuning.Defaults.Damage);

    private static ShipEffort AdvanceWithNoDemand(InstalledShip ship)
    {
        ShipEffort effort = default;
        for (int step = 0; step < SettlingSteps; step++)
        {
            effort = ship.Advance(
                PlanarVector.Zero,
                NoDemand,
                NoDemand,
                Calm,
                FixedStep);
        }

        return effort;
    }

    private static ShipEffort ThrottleUp(InstalledShip ship)
    {
        ShipEffort effort = default;
        for (int step = 0; step < SettlingSteps; step++)
        {
            effort = ship.Advance(
                new PlanarVector(SpaceTuning.Defaults.Flight.MaximumThrust, 0.0),
                NoDemand,
                1.0,
                Calm,
                FixedStep);
        }

        return effort;
    }

    private static void HoldPatch(InstalledShip ship, TimeSpan held)
    {
        int steps = (int)Math.Ceiling(held.TotalSeconds / FixedStep.TotalSeconds);
        for (int step = 0; step < steps; step++)
        {
            ship.AdvanceRepairs(patchHeld: true, FixedStep);
        }
    }
}
