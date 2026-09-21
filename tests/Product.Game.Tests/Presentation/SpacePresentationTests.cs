using System.Numerics;
using System.Text;
using Rusty.Engine;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Presentation.Tests;

/// <summary>
/// What the navigation view puts in front of a player who has to pick a course by
/// it. These read the facts the projection actually handed the Engine: that the
/// bow and the motion are two readings rather than one, that the projected line is
/// the line the flight projected, that the flow reading is anchored to the world
/// instead of swimming around the hull, that a band a hull has declined is drawn
/// declined, and that the tuning-only readings ride a layer a player never has to
/// look at.
/// </summary>
public class SpacePresentationTests
{
    private const ulong FirstPathPointId = 2_000UL;
    private const ulong FirstFlowPointId = 3_000UL;
    private const ulong FirstDebugVectorId = 4_000UL;
    private const ulong FirstCenterMarkerId = 4_100UL;
    private const int CenterMarkerCount = 4;
    private const double StockMass = 1.62 + 0.38;
    private const float DirectionTolerance = 0.95f;
    private const float PositionTolerance = 3;

    [Fact]
    public void TheWayTheHullPointsAndTheWayItIsGoingAreTwoSeparateReadings()
    {
        // Run out under power, then take the bow off the line and keep going. In
        // an inertial hull these are different facts, and a player flying one has
        // to be able to see both at once: the rod beside the hull says which way it
        // is actually going, and it says it independently of which way the hull is
        // pointed.
        RecordingEngine engine = new();
        // A hull pointed one way and going another: the case the view exists to
        // make visible, and the one a single arrow on the hull cannot tell.
        FlightReadout turnedOver = new(
            PlanarVector.Zero,
            1.9,
            new PlanarVector(3.0, 0.2),
            0.0,
            StockMass,
            0.4);

        AppearanceFact[] facts = Publish(engine, turnedOver);
        AppearanceFact bow = Fact(facts, (ulong)SpaceAppearanceObject.Ship);
        AppearanceFact motion = Fact(facts, (ulong)SpaceAppearanceObject.Velocity);

        Assert.True(bow.Visible, "expected the hull to be drawn");
        Assert.True(
            motion.Visible,
            "expected a hull under way to have its motion drawn beside it");
        Vector3 bowPoints = Vector3.Transform(Vector3.UnitX, bow.Transform.Rotation);
        Vector3 hullIsGoing = Vector3.Transform(Vector3.UnitX, motion.Transform.Rotation);
        Vector3 actualMotion = new(3.0f, 0.0f, 0.2f);

        Assert.True(
            Vector3.Dot(Vector3.Normalize(hullIsGoing), Vector3.Normalize(actualMotion))
                > DirectionTolerance,
            $"expected the motion reading to point where the hull is going: ({hullIsGoing.X:F3}, {hullIsGoing.Z:F3}) against ({actualMotion.X:F3}, {actualMotion.Z:F3})");
        Assert.True(
            Vector3.Dot(bowPoints, hullIsGoing) < DirectionTolerance,
            $"expected a turned hull to show its bow and its motion apart: {Vector3.Dot(bowPoints, hullIsGoing):F3}");
    }

    [Fact]
    public void TheProjectedLineIsDrawnWhereTheFlightProjectedIt()
    {
        // The line on the screen is the flight's answer, point for point, and not
        // a second guess the view made on the way past.
        RecordingEngine engine = new();
        PlanarVector[] projected = [
            new(2.0, 0.0),
            new(3.5, 0.4),
            new(5.0, 1.1),
        ];
        FlightPath path = new(projected, TimeSpan.FromSeconds(0.15));

        AppearanceFact[] facts = Publish(engine, AtRest(), path);
        Assert.Equal(
            path.Points.Length,
            facts.Count(fact =>
                fact.ObjectId >= FirstPathPointId && fact.ObjectId < FirstFlowPointId));

        for (int point = 0; point < projected.Length; point++)
        {
            PlanarVector expected = projected[point];
            AppearanceFact marker = Fact(facts, checked(FirstPathPointId + (ulong)point));
            Assert.Equal((float)expected.X, marker.Transform.Translation.X, PositionTolerance);
            Assert.Equal((float)expected.Z, marker.Transform.Translation.Z, PositionTolerance);
        }
    }

    [Fact]
    public void TheFlowReadingStaysWhereTheWorldPutItWhileTheHullCrossesIt()
    {
        // A reading a player picks a line by has to stay put. The lattice is
        // anchored on the world, so moving the hull a little leaves every rod
        // exactly where it was, pointing at the same flow, while the hull itself
        // moves off through them.
        RecordingEngine engine = new();
        AppearanceFact[] before = Publish(engine, At(new PlanarVector(0.0, 0.0)));
        Vector3[] latticeBefore = LatticeTranslations(before);
        Vector3 hullBefore = Fact(before, (ulong)SpaceAppearanceObject.Ship).Transform.Translation;

        AppearanceFact[] after = Publish(engine, At(new PlanarVector(1.3, 0.9)));

        Assert.NotEqual(
            hullBefore,
            Fact(after, (ulong)SpaceAppearanceObject.Ship).Transform.Translation);
        Assert.Equal(latticeBefore, LatticeTranslations(after));
    }

    [Fact]
    public void ABandIsDrawnDeclinedForAHullThatHasDeclinedItsCoupling()
    {
        // Coupling is the hull's choice, and the view has to say what that choice
        // means: wind it off and the bands out there stop being drawn as something
        // that will catch this hull. The extent they would have had is still there,
        // because a player who trims back on needs to know what they are sailing
        // past.
        RecordingEngine engine = new();
        AppearanceFact[] engaged = Publish(engine, AtRest(), Coupled(0.7));
        AppearanceFact[] declined = Publish(engine, AtRest(), Coupled(0.0));

        Assert.NotEqual(
            Fact(engaged, (ulong)SpaceAppearanceObject.GentleCurrent).Appearance,
            Fact(declined, (ulong)SpaceAppearanceObject.GentleCurrent).Appearance);
        Assert.Contains(
            engaged,
            fact => fact.ObjectId == (ulong)SpaceAppearanceObject.GentleAuthority);
        Assert.Contains(
            declined,
            fact => fact.ObjectId == (ulong)SpaceAppearanceObject.GentleAuthority);
    }

    [Fact]
    public void TuningReadingsRideTheDebugLayerApartFromTheWorld()
    {
        // Where each source pushes and where each center of force lands is the
        // tuning pass's business, not the player's: those go on the Engine's debug
        // layer, and nothing that belongs to the world does.
        RecordingEngine engine = new();
        AppearanceFact[] facts = Publish(engine, AtRest(), FlightTelemetrySnapshot.Neutral);
        Assert.Equal(CenterMarkerCount, facts.Count(fact =>
            fact.ObjectId >= FirstCenterMarkerId
            && fact.ObjectId < (FirstCenterMarkerId + (ulong)CenterMarkerCount)));

        foreach (AppearanceFact fact in facts)
        {
            bool tuningOnly = fact.ObjectId is >= FirstDebugVectorId
                and < (FirstCenterMarkerId + (ulong)CenterMarkerCount);
            Assert.Equal(tuningOnly ? RenderLayer.Debug : RenderLayer.Scene, fact.Layer);
        }
    }

    [Fact]
    public void AFavorableCurrentAheadIsReadableAsAStrongerRodPointingTheWayItRuns()
    {
        // The reading a course is picked by: at a lattice point inside the swift
        // current the rod is long and runs the way the band runs, while a point in
        // the ambient field beside it reports what is actually there. A player who
        // can see that difference can aim the ship so the line runs into the band
        // rather than past it.
        RecordingEngine engine = new();
        AppearanceFact[] facts = Publish(engine, At(new PlanarVector(0.0, 14.0)));

        AppearanceFact inTheCurrent = LatticeAt(facts, new PlanarVector(0.0, 21.0));
        AppearanceFact inAmbientFlow = LatticeAt(facts, new PlanarVector(0.0, 7.0));
        Assert.True(
            inTheCurrent.Visible && inAmbientFlow.Visible,
            "expected both a banded point and an ambient point to report flow");
        Assert.True(
            inTheCurrent.Transform.Scale.X > (inAmbientFlow.Transform.Scale.X * 2.0f),
            $"expected the band's rod to stand out against the ambient flow: {inTheCurrent.Transform.Scale.X:F3} against {inAmbientFlow.Transform.Scale.X:F3}");

        Vector3 runs = Vector3.Transform(Vector3.UnitX, inTheCurrent.Transform.Rotation);
        Assert.True(
            Vector3.Dot(Vector3.Normalize(runs), Vector3.UnitX) > 0.8f,
            $"expected the band's rod to point the way the band runs: ({runs.X:F3}, {runs.Z:F3})");
    }

    private static AppearanceFact LatticeAt(AppearanceFact[] facts, PlanarVector point) =>
        facts.Single(fact =>
            fact.ObjectId >= FirstFlowPointId
            && fact.ObjectId < FirstDebugVectorId
            && Math.Abs(fact.Transform.Translation.X - point.X) < 0.01f
            && Math.Abs(fact.Transform.Translation.Z - point.Z) < 0.01f);

    [Fact]
    public void TheInstrumentsNameEveryValueTheyHandThePanel()
    {
        // A reading published under the wrong name is a number a player reads as
        // something else, and a name used twice silently overwrites one reading
        // with another. The panel is only as good as the labels on it.
        RecordingEngine engine = new();
        Publish(engine, AtRest());

        UiValue hud = engine.Ui.LastProjection!.Value.Value;
        StructuredValueNode[] nodes = hud.Nodes.ToArray();
        byte[] names = hud.Utf8.ToArray();
        string[] keyed = [.. nodes
            .Where(node => node.Kind == StructuredValueKind.Number)
            .Select(node => Encoding.UTF8.GetString(names, (int)node.KeyOffset, (int)node.KeyLen))];

        Assert.Equal(
            ["heading", "speed", "thrust", "accel", "turn", "coupling", "flow", "asym"],
            keyed);
        Assert.Equal(nodes.Length - 1, keyed.Length);
    }

    private static Vector3[] LatticeTranslations(AppearanceFact[] facts) =>
        [.. facts
            .Where(fact => fact.ObjectId >= FirstFlowPointId && fact.ObjectId < FirstDebugVectorId)
            .OrderBy(fact => fact.ObjectId)
            .Select(fact => fact.Transform.Translation)];

    private static AppearanceFact Fact(AppearanceFact[] facts, ulong objectId) =>
        facts.Single(fact => fact.ObjectId == objectId);

    private static AppearanceFact[] Publish(
        RecordingEngine engine, FlightReadout readout) => Publish(
            engine, readout, FlightTelemetrySnapshot.Neutral, FlightPath.None);

    private static AppearanceFact[] Publish(
        RecordingEngine engine, FlightReadout readout, FlightTelemetrySnapshot telemetry) => Publish(
            engine, readout, telemetry, FlightPath.None);

    private static AppearanceFact[] Publish(
        RecordingEngine engine, FlightReadout readout, FlightPath path) => Publish(
            engine, readout, FlightTelemetrySnapshot.Neutral, path);

    private static AppearanceFact[] Publish(
        RecordingEngine engine,
        FlightReadout readout,
        FlightTelemetrySnapshot telemetry,
        FlightPath path)
    {
        SpaceTuning tuning = SpaceTuning.Defaults;
        SpacePresentation presentation = new(
            engine.Graphics,
            engine.Ui,
            new FlightEnvironment(
                new StellarField(tuning.Field),
                new DriftCurrent(tuning.GentleCurrent),
                new DriftCurrent(tuning.SwiftCurrent)),
            tuning.Presentation,
            tuning.Overlay);
        presentation.Publish(
            readout,
            telemetry,
            FlightForces.Zero,
            path,
            new InstalledShip(tuning.Ship, tuning.Flight.MaximumThrust));
        return engine.Graphics.LastSnapshot;
    }

    private static FlightReadout AtRest() => At(PlanarVector.Zero);

    private static FlightReadout At(PlanarVector position) => new(
        position,
        0.0,
        PlanarVector.Zero,
        0.0,
        StockMass,
        0.4);

    private static FlightTelemetrySnapshot Coupled(double level) =>
        FlightTelemetrySnapshot.Neutral with { Coupling = level };
}
