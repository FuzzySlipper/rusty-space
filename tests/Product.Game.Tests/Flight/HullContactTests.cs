using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Approach;
using Rusty.Space.Product.Engine.Tests;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Tuning;
using Xunit;

namespace Rusty.Space.Product.Flight.Tests;

/// <summary>
/// Reading what the Engine's contacts did to the hull. Whether the hull and a
/// rock met, and what that push was, are the Engine's answers; what this reads
/// them into — a named impact, stated in the hull's own frame — is the product's.
/// </summary>
public class HullContactTests
{
    [Fact]
    public void AContactIsReportedAgainstTheThingTheHullMet()
    {
        (RecordingDynamics dynamics, DynamicsWorld world, DynamicsBody hull, ApproachField chart) =
            HullAgainstTheChart();
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f);
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(hull.Handle.Value),
            Second: ObstacleHandle(0),
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f));

        HullImpact impact = HullContacts(dynamics, chart).Read(
            world, hull, HullReadout(1U, dynamics.HullContact), headingRadians: 0.0);

        Assert.True(impact.Present);
        Assert.Equal(3.0, impact.Magnitude, 6);
        AssertPush(new PlanarVector(0.0, -3.0), impact.Impulse);

        // The obstacle authored first is the one the chart put at handle two.
        Assert.Equal(SpaceTuning.Defaults.Approach.Obstacles[0].Id, impact.Struck);
    }

    [Fact]
    public void APushIsStatedAsThePushOnTheHullWhicheverEndOfThePairItIsOn()
    {
        (RecordingDynamics dynamics, DynamicsWorld world, DynamicsBody hull, ApproachField chart) =
            HullAgainstTheChart();
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, 3.0f),
            ImpulseMagnitude: 3.0f);

        // The same pair, reported from the rock's end: the Engine hands the
        // impulse to the body named first and its negation to the body named
        // second, and what the hull felt is the second of those here.
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: ObstacleHandle(0),
            Second: new DynamicsBodyReference(hull.Handle.Value),
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f));

        HullImpact impact = HullContacts(dynamics, chart).Read(
            world, hull, HullReadout(1U, dynamics.HullContact), headingRadians: 0.0);

        Assert.True(impact.Present);
        AssertPush(new PlanarVector(0.0, 3.0), impact.Impulse);
    }

    [Fact]
    public void AContactIsAlsoStatedInTheHullsOwnFrame()
    {
        (RecordingDynamics dynamics, DynamicsWorld world, DynamicsBody hull, ApproachField chart) =
            HullAgainstTheChart();
        dynamics.ContactCount = 1U;
        dynamics.HullContact = new DynamicsContactFact(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -3.0f),
            ImpulseMagnitude: 3.0f);

        // A hull facing along +Z shoved toward -Z was shoved from ahead and takes
        // it on the bow: in the hull's frame the push points astern, whatever way
        // the hull happens to be pointing when it arrives.
        HullImpact impact = HullContacts(dynamics, chart).Read(
            world, hull, HullReadout(1U, dynamics.HullContact), headingRadians: Math.PI / 2.0);

        AssertPush(new PlanarVector(0.0, -3.0), impact.Impulse);
        AssertPush(new PlanarVector(-3.0, 0.0), impact.LocalImpulse);
    }

    [Fact]
    public void TheHardestOfSeveralContactsIsTheOneThatGetsRead()
    {
        (RecordingDynamics dynamics, DynamicsWorld world, DynamicsBody hull, ApproachField chart) =
            HullAgainstTheChart();
        DynamicsContactFact brushAgainstFender = new(
            Present: true,
            Environment: false,
            Impulse: new Vector3(0.0f, 0.0f, -1.0f),
            ImpulseMagnitude: 1.0f);
        dynamics.ContactCount = 1U;
        dynamics.HullContact = brushAgainstFender;
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: ObstacleHandle(0),
            Second: ObstacleHandle(1),
            Impulse: new Vector3(0.0f, 0.0f, -9.0f),
            ImpulseMagnitude: 9.0f));
        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: new DynamicsBodyReference(hull.Handle.Value),
            Second: ObstacleHandle(2),
            Impulse: new Vector3(0.0f, 0.0f, -5.0f),
            ImpulseMagnitude: 5.0f));

        // A rock falling on another rock ten metres away is not something the hull
        // struck, and of the two contacts that involve the hull the harder one is
        // the one worth reporting.
        HullImpact impact = HullContacts(dynamics, chart).Read(
            world, hull, HullReadout(1U, brushAgainstFender), headingRadians: 0.0);

        Assert.Equal(5.0, impact.Magnitude, 6);
        Assert.Equal(SpaceTuning.Defaults.Approach.Obstacles[2].Id, impact.Struck);
    }

    [Fact]
    public void AHullWithNothingAgainstItHasNothingToReport()
    {
        (RecordingDynamics dynamics, DynamicsWorld world, DynamicsBody hull, ApproachField chart) =
            HullAgainstTheChart();

        dynamics.WorldContacts.Add(new DynamicsContactAtReceipt(
            Present: true,
            Environment: false,
            First: ObstacleHandle(0),
            Second: ObstacleHandle(1),
            Impulse: new Vector3(0.0f, 0.0f, -9.0f),
            ImpulseMagnitude: 9.0f));

        HullImpact impact = HullContacts(dynamics, chart).Read(
            world, hull, HullReadout(contactCount: 0U), headingRadians: 0.0);

        Assert.False(impact.Present);
        Assert.Null(impact.Struck);

        // And the world's contact list is not walked at all for it.
        Assert.Equal(0, dynamics.WorldReads);
    }

    private static HullContacts HullContacts(RecordingDynamics dynamics, ApproachField chart) =>
        new(dynamics, chart);

    private static void AssertPush(PlanarVector expected, PlanarVector actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }

    private static (RecordingDynamics, DynamicsWorld, DynamicsBody, ApproachField) HullAgainstTheChart()
    {
        RecordingDynamics dynamics = new();
        DynamicsWorld world = dynamics.CreateWorld(default);
        DynamicsBody hull = dynamics.CreateBody(new DynamicsCreateBodyRequest(
            world,
            new DynamicsBodyConfig(
                new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                new Vector3(0.5f, 0.75f, 0.5f),
                2.25f,
                new DynamicsMassPolicy(DynamicsMassPolicyKind.DeriveFromShapeAndMass, default),
                new AxisLocks(
                    TranslationX: false,
                    TranslationY: true,
                    TranslationZ: false,
                    RotationX: true,
                    RotationY: false,
                    RotationZ: true),
                GravityScale: 0.0f)));
        ApproachField chart = new(dynamics, world, SpaceTuning.Defaults.Approach);
        return (dynamics, world, hull, chart);
    }

    // The chart's bodies are opened after the hull's, so the obstacle authored at
    // a given index answers to the handle that many past the hull's.
    private static DynamicsBodyReference ObstacleHandle(int authoredIndex) =>
        new((ulong)(authoredIndex + 2));

    private static DynamicsReadout HullReadout(
        uint contactCount,
        DynamicsContactFact first = default) => new(
            new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
            Vector3.Zero,
            Vector3.Zero,
            Sleeping: false,
            new MassProperties(
                Available: true,
                Mass: 2.25f,
                PrincipalInertia: new Vector3(1.0f, 1.0f, 1.0f),
                Policy: DynamicsMassPolicyKind.DeriveFromShapeAndMass,
                CenterOfMass: Vector3.Zero,
                PrincipalInertiaLocalFrame: Quaternion.Identity),
            ContactCount: contactCount,
            FirstContact: first);
}
