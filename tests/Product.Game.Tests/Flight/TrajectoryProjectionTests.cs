using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// What the line the navigation view draws ahead of the hull is allowed to claim.
/// A player picks a course by reading it, so it is held to the same accounting as
/// the hull itself: it goes where the hull's own push takes it, it reads the
/// environment at the points ahead rather than the one the ship is at, it answers
/// the throttle, and it promises no stopping that no force would cause.
/// </summary>
public class TrajectoryProjectionTests
{
    private static readonly TimeSpan FixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private const double StockMass = 1.62 + 0.38;
    private const double SecondsAhead = 1.5;
    private const double TightTolerance = 1e-3;
    private const double ReadingTolerance = 0.05;

    [Fact]
    public void ALineFromACoastingHullKeepsBothItsSpeedAndItsStraightness()
    {
        // Nothing is pushing on this hull: the drive is dark, the coupling is
        // wound off, and it is far enough out that the planet's well has almost
        // nothing to say about it. A
        // projection that braked, drift-corrected, or otherwise helped along on
        // its own would show up here as a line that curved or a line whose far
        // steps were shorter than its near ones.
        RecordingKinematic kinematic = new();
        TrajectoryProjection projection = Projection(kinematic);
        PlanarVector start = PlanarVector.Zero;
        PlanarVector velocity = new(4.0, 0.0);

        projection = Projection(kinematic);
        FlightPath path = projection.Project(
            new FlightBodyState(start, 0.0, velocity, 0.0),
            StockShip(),
            Effort(coupling: 0.0),
            StockMass,
            FixedStep);

        PlanarVector[] points = path.Points.ToArray();
        Assert.Equal(SpaceTuning.Defaults.Trajectory.SampleCount, points.Length);
        PlanarVector expectedEnd = start + velocity.Scale(SecondsAhead);
        PlanarVector drift = points[^1] - expectedEnd;
        Assert.True(
            drift.Magnitude < TightTolerance,
            $"expected the line to end where the hull's own motion puts it, got ({points[^1].X:F4}, {points[^1].Z:F4}) against ({expectedEnd.X:F4}, {expectedEnd.Z:F4})");

        double near = (points[1] - points[0]).Magnitude;
        double far = (points[^1] - points[^2]).Magnitude;
        Assert.True(
            far >= near - TightTolerance,
            $"expected no braking along a line nothing is slowing: near interval {near:F4}, far interval {far:F4}");
        Assert.True(
            Math.Abs(path.SampleInterval.TotalSeconds
                - (SecondsAhead / SpaceTuning.Defaults.Trajectory.SampleCount)) < 1e-5,
            $"expected each sample to be one slice of the horizon apart, got {path.SampleInterval.TotalSeconds:F7}");
    }

    [Fact]
    public void ALineHeadingIntoABandIsCaughtByItBeforeTheHullArrives()
    {
        // The hull is coasting up toward the swift current, still short of it at
        // a distance where the band has barely a grip on it yet. The line the
        // player reads has to meet that current along its length: the push the
        // environment puts on a hull is read at each point the line reaches, not
        // once at the ship, which is what lets a course be picked for the band it
        // is about to enter.
        RecordingKinematic kinematic = new();
        TrajectoryProjection projection = Projection(kinematic);
        PlanarVector start = new(0.0, 16.0);

        FlightPath path = projection.Project(
            new FlightBodyState(start, 0.0, new PlanarVector(0.0, 4.0), 0.0),
            StockShip(),
            Effort(coupling: 1.0),
            StockMass,
            FixedStep);

        PlanarVector[] points = path.Points.ToArray();
        Assert.True(
            points[^1].X > 0.5,
            $"expected the band ahead to pull the line with it, ended at X {points[^1].X:F4}");
        for (int point = 1; point < points.Length; point++)
        {
            Assert.True(
                points[point].X >= points[point - 1].X - TightTolerance,
                $"expected the line to keep being carried the way the band runs, step {point} gave back ground");
        }
    }

    [Fact]
    public void ALineFromAHullThatHasDeclinedItsCouplingIsNotCaughtByAnything()
    {
        // The same coast, aimed at the same band, with the coupling wound off.
        // Nothing out there has a hold on the hull, so the line stays on the
        // straight course the hull's momentum describes and the player can see
        // that this trim will not catch the current ahead.
        RecordingKinematic kinematic = new();
        TrajectoryProjection projection = Projection(kinematic);
        PlanarVector start = new(0.0, 16.0);

        FlightPath path = projection.Project(
            new FlightBodyState(start, 0.0, new PlanarVector(0.0, 4.0), 0.0),
            StockShip(),
            Effort(coupling: 0.0),
            StockMass,
            FixedStep);

        PlanarVector[] points = path.Points.ToArray();
        Assert.True(
            Math.Abs(points[^1].X - start.X) < ReadingTolerance,
            $"expected an uncoupled hull to be left alone by the band, ended at X {points[^1].X:F4}");
        Assert.True(
            Math.Abs(points[^1].Z - (start.Z + (4.0 * SecondsAhead))) < 0.25,
            $"expected the uncoupled line to hold its own motion, ended at Z {points[^1].Z:F4}");
    }

    [Fact]
    public void ADriveStillSpoolingCarriesTheLineFartherThanOneJustReleased()
    {
        // The line holds the controls where they are, and the drive's actuator is
        // part of that. Throttle down and the line shortens; that shortening is
        // the player's cue that the ship will not keep answering.
        RecordingKinematic held = new();
        RecordingKinematic released = new();
        FlightBodyState state = new(
            PlanarVector.Zero,
            0.0,
            new PlanarVector(2.0, 0.0),
            0.0);

        double withDrive = Projection(held).Project(
            state, StockShip(), Effort(coupling: 0.0, drive: new PlanarVector(6.0, 0.0)), StockMass, FixedStep)
            .Points.ToArray()[^1].X;
        double dark = Projection(released).Project(
            state, StockShip(), Effort(coupling: 0.0), StockMass, FixedStep)
            .Points.ToArray()[^1].X;

        Assert.True(
            withDrive > dark + 2.0,
            $"expected a drive pushing at the front of the line to carry it farther: {withDrive:F3} against {dark:F3}");
    }

    [Fact]
    public void TheLineIsWalkedOnTheEnginesOwnKinematicLaneAtItsOwnClock()
    {
        // The product does not own an integrator, and a projected line is an
        // integration. Every sample is handed to the Engine's call-local kinematic
        // lane over whole fixed steps, with the environment's pull expressed as
        // the acceleration the hull would feel there — including the fact that
        // the orbital well is a product force, so the Engine's gravity term stays
        // out of it.
        RecordingKinematic kinematic = new();
        TrajectoryProjection projection = Projection(kinematic);
        TrajectoryTuning tuning = SpaceTuning.Defaults.Trajectory;

        projection.Project(
            new FlightBodyState(new PlanarVector(0.0, 16.0), 0.0, new PlanarVector(0.0, 4.0), 0.0),
            StockShip(),
            Effort(coupling: 1.0),
            StockMass,
            FixedStep);

        Assert.Equal(tuning.SampleCount, kinematic.Integrations.Count);
        foreach (KinematicIntegrationRequest request in kinematic.Integrations)
        {
            Assert.Equal((ulong)tuning.TicksPerSample, request.Step.Ticks);
            Assert.Equal(1.0 / 60.0, request.Step.SecondsPerTick, 5);
            Assert.Equal(Vector3.Zero, request.Settings.Gravity);
            Assert.Equal(0.0f, request.Body.GravityScale);
            Assert.Equal(KinematicCollisionMode.None, request.Body.CollisionMode);
        }

        for (int step = 1; step < kinematic.Integrations.Count; step++)
        {
            Assert.Equal(
                kinematic.Results[step - 1].NextPosition,
                kinematic.Integrations[step].Body.Position);
        }
    }

    [Fact]
    public void TheLineReadsTheEnvironmentWhereItHasReachedRatherThanWhereTheHullIs()
    {
        // A projection that sampled the environment once at the ship and then
        // coasted on that answer would be a straight line with a fancy name. Read
        // where the hull is, the swift current has barely a grip; read at the far
        // end of the line, well into it, it has a real one.
        RecordingKinematic kinematic = new();
        Projection(kinematic).Project(
            new FlightBodyState(new PlanarVector(0.0, 16.0), 0.0, new PlanarVector(0.0, 4.0), 0.0),
            StockShip(),
            Effort(coupling: 1.0),
            StockMass,
            FixedStep);

        Vector3 atTheHull = kinematic.Integrations[0].Body.Acceleration;
        Vector3 ahead = kinematic.Integrations[^1].Body.Acceleration;
        Assert.True(
            ahead.Length() > atTheHull.Length(),
            $"expected the far end of the line to be reading a stronger current than the hull is: {ahead.Length():F4} against {atTheHull.Length():F4}");
    }

    /// <summary>
    /// A projection over the shipped field and bands with the planet's well turned
    /// off, so that what a line does in these tests is the thing under test and
    /// nothing else. The well has a test of its own.
    /// </summary>
    private static TrajectoryProjection Projection(RecordingKinematic kinematic)
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        return new TrajectoryProjection(
            kinematic,
            new HullForceModel(
                new FieldResponse(tuning.Field),
                new DriftCurrent(tuning.GentleCurrent),
                new DriftCurrent(tuning.SwiftCurrent),
                new OrbitalGravity(tuning.Orbital with { Strength = 0.0, Swirl = 0.0 })),
            new StellarField(tuning.Field),
            tuning.Trajectory);
    }

    private static InstalledShip StockShip() => new(
        SpaceTuning.Defaults.Ship,
        SpaceTuning.Defaults.Flight.MaximumThrust);

    private static ShipEffort Effort(double coupling, PlanarVector drive = default) => new(
        DriveForce: drive,
        HeadingTorque: 0.0,
        WearPull: 0.0,
        Coupling: coupling,
        HeadingEffort: 0.0,
        HeadingSaturated: false,
        HeadingAsymmetry: 0.0);
}
