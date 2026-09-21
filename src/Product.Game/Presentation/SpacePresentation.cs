using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Presentation;

/// <summary>
/// Product-owned meaning for the facts Engine renders in Space: the hull and its
/// world, and the navigational reading laid over that world.
/// </summary>
/// <remarks>
/// <para>
/// The view answers what the hull is doing and what the environment is about to
/// do to it. Which way the ship points and which way it is going are drawn as two
/// separate things, because in an inertial model they are two separate things and
/// a player who cannot tell them apart cannot fly one. Local flow is shown on a
/// lattice anchored to the world rather than to the ship, so the reading stays put
/// as the hull moves across it. Each band is drawn twice: a wide faint slab for
/// the authority it has, and a core slab for where it pushes at close to full
/// strength — the core drawn declined when the hull has wound its coupling down,
/// because on that trim the band will not catch it.
/// </para>
/// <para>
/// Everything here is downstream. The environment is read through its owners,
/// never re-derived; the tuning-only readings — each source's push and each
/// center of force — go on the Engine's debug layer where a player never has to
/// see them. Nothing published through this projection reaches back into flight
/// state.
/// </para>
/// </remarks>
internal sealed class SpacePresentation : IDisposable
{
    // Content identity of the authored ship dart; admitted product content is
    // keyed by its content-root-relative path.
    private const string ShipMeshPath = "meshes/ship.json";
    private const string HudStreamName = "rusty-space";
    private const string HudContract = "rusty.space.hud";
    private const float NeutralHeadingRadians = 0.0f;
    private const float HalfLength = 0.5f;
    private const float UniformScale = 1.0f;
    private const ulong FirstStarObjectId = 1_000UL;
    private const ulong FirstPathPointId = 2_000UL;
    private const ulong FirstFlowPointId = 3_000UL;
    private const ulong FirstDebugVectorId = 4_000UL;
    private const ulong FirstCenterMarkerId = 4_100UL;

    // Scene facts that are always there, whatever the ship is doing: the hull,
    // the planet, the wake, both bands and both authority regions, and the
    // velocity reading beside the hull.
    private const int FixedSceneFactCount = 8;

    // One marker per center of force the hull has, beside the push each source
    // puts on the world.
    private const int CenterMarkerCount = 4;
    private const int DebugVectorCount = 5;
    private const int MainDriveSource = 0;
    private const int FieldSource = 1;
    private const int GentleSource = 2;
    private const int SwiftSource = 3;
    private const int ThrustCenterMarker = 0;
    private const int CouplingCenterMarker = 1;
    private const int SteeringCenterMarker = 2;
    private const float NoFlow = 0.0f;
    private const double NegligibleFlow = 1e-3;
    private const double NegligibleForce = 1e-3;
    private const double Uncoupled = 0.0;

    private readonly IGraphicsService appearance;
    private readonly IUiService ui;
    private readonly StellarField field;
    private readonly DriftCurrent gentleCurrent;
    private readonly DriftCurrent swiftCurrent;
    private readonly SpacePresentationTuning tuning;
    private readonly NavigationOverlayTuning overlay;
    private readonly Appearance shipAppearance;
    private readonly Appearance planetAppearance;
    private readonly Appearance wakeAppearance;
    private readonly Appearance gentleAppearance;
    private readonly Appearance swiftAppearance;
    private readonly Appearance gentleAuthorityAppearance;
    private readonly Appearance swiftAuthorityAppearance;
    private readonly Appearance declinedAppearance;
    private readonly Appearance velocityAppearance;
    private readonly Appearance pathAppearance;
    private readonly Appearance flowAppearance;
    private readonly Appearance debugVectorAppearance;
    private readonly Appearance centerMarkerAppearance;
    private readonly Appearance starAppearance;
    private readonly UiStream hudStream;
    private ulong hudSequence;
    private bool retainedSnapshotRetired;
    private bool released;

    internal SpacePresentation(
        IGraphicsService appearance,
        IUiService ui,
        FlightEnvironment environment,
        SpacePresentationTuning tuning,
        NavigationOverlayTuning overlay)
    {
        this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        field = environment.Field;
        gentleCurrent = environment.GentleCurrent;
        swiftCurrent = environment.SwiftCurrent;
        this.tuning = tuning;
        this.overlay = overlay.Validate();

        // A failed create callback is discarded by the staged Engine call, so
        // this constructor deliberately does not issue individual release
        // calls that could desynchronize generated lease wrappers from a
        // later transaction rollback.
        shipAppearance = CreateCube(this.tuning.ShipColor);
        planetAppearance = CreateSphere(this.tuning.PlanetColor);
        wakeAppearance = CreateCube(this.tuning.WakeColor);
        gentleAppearance = CreateCube(this.tuning.GentleCurrentColor);
        swiftAppearance = CreateCube(this.tuning.SwiftCurrentColor);
        gentleAuthorityAppearance = CreateCube(overlay.GentleAuthorityColor);
        swiftAuthorityAppearance = CreateCube(overlay.SwiftAuthorityColor);
        declinedAppearance = CreateCube(overlay.DeclinedColor);
        velocityAppearance = CreateCube(overlay.VelocityIndicatorColor);
        pathAppearance = CreateCube(overlay.PathColor);
        flowAppearance = CreateCube(overlay.FlowColor);
        debugVectorAppearance = CreateCube(overlay.DebugColor);
        centerMarkerAppearance = CreateCube(overlay.DebugCenterColor);
        starAppearance = CreateSphere(this.tuning.StarColor);
        hudStream = this.ui.OpenStream(new UiStreamRequest(HudStreamName, HudContract));
    }

    internal void Publish(
        FlightReadout readout,
        FlightTelemetrySnapshot telemetry,
        FlightForces contributions,
        FlightPath path,
        InstalledShip ship)
    {
        ArgumentNullException.ThrowIfNull(ship);

        PublishAppearance(readout, telemetry, contributions, path, ship);
        PublishHud(readout, telemetry);
    }

    private void PublishAppearance(
        FlightReadout readout,
        FlightTelemetrySnapshot telemetry,
        FlightForces contributions,
        FlightPath path,
        InstalledShip ship)
    {
        int starWidth = checked((tuning.StarGridRadius * 2) + 1);
        int starCount = checked(starWidth * starWidth);
        int flowWidth = checked((overlay.FlowLatticeRadius * 2) + 1);
        int flowCount = checked(flowWidth * flowWidth);
        AppearanceFact[] facts = new AppearanceFact[checked(
            FixedSceneFactCount + path.Points.Length + flowCount
            + DebugVectorCount + CenterMarkerCount)];
        facts[0] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.Ship,
                false,
                0,
                ShipTransform(readout),
                shipAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[1] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.Planet,
                false,
                0,
                PlanetTransform(),
                planetAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[2] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.Wake,
                false,
                0,
                WakeTransform(),
                wakeAppearance,
                Visible: true,
                RenderLayer.Scene);
        // Coupled bands are drawn in their own color; a hull that has wound its
        // coupling down sees them declined, because on that trim they have
        // nothing to do with it.
        bool caught = telemetry.Coupling > Uncoupled;
        facts[3] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.GentleCurrent,
                false,
                0,
                CurrentTransform(gentleCurrent.Shape, tuning.GentleCurrentDepth, tuning.GentleCurrentHeight),
                caught ? gentleAppearance : declinedAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[4] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.SwiftCurrent,
                false,
                0,
                CurrentTransform(swiftCurrent.Shape, tuning.SwiftCurrentDepth, tuning.SwiftCurrentHeight),
                caught ? swiftAppearance : declinedAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[5] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.GentleAuthority,
                false,
                0,
                AuthorityTransform(gentleCurrent.Shape, tuning.GentleCurrentDepth, tuning.GentleCurrentHeight),
                gentleAuthorityAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[6] = new AppearanceFact(
                (ulong)SpaceAppearanceObject.SwiftAuthority,
                false,
                0,
                AuthorityTransform(swiftCurrent.Shape, tuning.SwiftCurrentDepth, tuning.SwiftCurrentHeight),
                swiftAuthorityAppearance,
                Visible: true,
                RenderLayer.Scene);
        facts[7] = VelocityTransform(readout);
        int index = FixedSceneFactCount;
        index = PublishPath(facts, index, path);
        index = PublishFlowLattice(facts, index, readout);
        index = PublishDebugVectors(facts, index, readout, contributions, ship);
        PublishCenterMarkers(facts, index, readout.HeadingRadians, ship);
        appearance.PublishSnapshot(facts);
    }

    /// <summary>
    /// The line the hull is on, one marker per sample ahead.
    /// </summary>
    private int PublishPath(AppearanceFact[] facts, int index, FlightPath path)
    {
        int marker = 0;
        foreach (PlanarVector point in path.Points.Span)
        {
            facts[index++] = new AppearanceFact(
                checked(FirstPathPointId + (ulong)marker),
                false,
                0,
                MarkerTransform(point, overlay.PathMarkerHeight, overlay.PathMarkerSize),
                pathAppearance,
                Visible: true,
                RenderLayer.Scene);
            marker++;
        }

        return index;
    }

    /// <summary>
    /// Local flow on a lattice pinned to the world, so the reading a player picks
    /// a line by stays where they left it instead of swimming around the hull.
    /// </summary>
    private int PublishFlowLattice(AppearanceFact[] facts, int index, FlightReadout readout)
    {
        int radius = overlay.FlowLatticeRadius;
        float spacing = overlay.FlowLatticeSpacing;
        PlanarVector anchor = new(
            Math.Round(readout.Position.X / spacing) * spacing,
            Math.Round(readout.Position.Z / spacing) * spacing);
        int cell = 0;
        for (int gridZ = -radius; gridZ <= radius; gridZ++)
        {
            for (int gridX = -radius; gridX <= radius; gridX++)
            {
                PlanarVector point = anchor + new PlanarVector(gridX * spacing, gridZ * spacing);
                PlanarVector flow = LocalFlowAt(point);
                double strength = flow.Magnitude;
                facts[index++] = new AppearanceFact(
                    checked(FirstFlowPointId + (ulong)cell),
                    false,
                    0,
                    FlowRodTransform(point, flow, strength * overlay.FlowRodLengthPerUnitSpeed),
                    flowAppearance,
                    Visible: strength > NegligibleFlow,
                    RenderLayer.Scene);
                cell++;
            }
        }

        return index;
    }

    /// <summary>
    /// Tuning-only: each source's push, drawn where it lands on the hull. Torque
    /// is not a place, so the two steering channels are not drawn here; the
    /// centers they act through are, below.
    /// </summary>
    private int PublishDebugVectors(
        AppearanceFact[] facts,
        int index,
        FlightReadout readout,
        FlightForces contributions,
        InstalledShip ship)
    {
        double heading = readout.HeadingRadians;
        PlanarVector couplingCenter = ship.FieldCouplingCenter(heading);
        PlanarVector thrustCenter = ship.MainThrustCenter(heading);
        for (int source = 0; source < DebugVectorCount; source++)
        {
            (FlightWrench push, PlanarVector at) = source switch
            {
                MainDriveSource => (contributions.MainDrive, thrustCenter),
                FieldSource => (contributions.Field, couplingCenter),
                GentleSource => (contributions.GentleCurrent, couplingCenter),
                SwiftSource => (contributions.SwiftCurrent, couplingCenter),
                _ => (contributions.OrbitalPull, readout.Position),
            };
            double strength = push.Force.Magnitude;
            facts[index++] = new AppearanceFact(
                checked(FirstDebugVectorId + (ulong)source),
                false,
                0,
                RodTransform(
                    at,
                    push.Force,
                    strength * overlay.DebugVectorLengthPerUnitForce,
                    overlay.DebugVectorThickness,
                    overlay.DebugHeight),
                debugVectorAppearance,
                Visible: strength > NegligibleForce,
                RenderLayer.Debug);
        }

        return index;
    }

    /// <summary>
    /// Tuning-only: where each center of force landed at the hull's current
    /// heading. Which center a push arrives at is most of a ship's character, so
    /// it is worth being able to see it land.
    /// </summary>
    private void PublishCenterMarkers(
        AppearanceFact[] facts,
        int index,
        double heading,
        InstalledShip ship)
    {
        for (int marker = 0; marker < CenterMarkerCount; marker++)
        {
            PlanarVector center = marker switch
            {
                ThrustCenterMarker => ship.MainThrustCenter(heading),
                CouplingCenterMarker => ship.FieldCouplingCenter(heading),
                SteeringCenterMarker => ship.SteeringAuthorityCenter(heading),
                _ => ship.StabilizationCenter(heading),
            };
            facts[index++] = new AppearanceFact(
                checked(FirstCenterMarkerId + (ulong)marker),
                false,
                0,
                MarkerTransform(center, overlay.DebugHeight, overlay.DebugMarkerSize),
                centerMarkerAppearance,
                Visible: true,
                RenderLayer.Debug);
        }
    }

    private PlanarVector LocalFlowAt(PlanarVector point) =>
        field.Sample(point).FlowVelocity + gentleCurrent.FlowAt(point) + swiftCurrent.FlowAt(point);

    private AppearanceFact VelocityTransform(FlightReadout readout)
    {
        double speed = readout.LinearVelocity.Magnitude;
        PlanarVector velocity = readout.LinearVelocity;
        return new AppearanceFact(
            (ulong)SpaceAppearanceObject.Velocity,
            false,
            0,
            RodTransform(
                readout.Position,
                velocity,
                speed * overlay.VelocityIndicatorLengthPerUnitSpeed,
                overlay.VelocityIndicatorThickness,
                overlay.VelocityIndicatorHeight),
            velocityAppearance,
            Visible: speed > NegligibleFlow,
            RenderLayer.Scene);
    }

    private Transform ShipTransform(FlightReadout readout) => new(
        PositionAtHeight(readout.Position, tuning.ShipHeight),
        PlanarFrame.ToEngineAttitude(readout.HeadingRadians),
        new Vector3(UniformScale, UniformScale, UniformScale));

    private Transform PlanetTransform() => new(
        PositionAtHeight(field.Shape.PlanetPosition, tuning.PlanetHeight),
        PlanarFrame.ToEngineAttitude(NeutralHeadingRadians),
        new Vector3(tuning.PlanetDiameter, tuning.PlanetDiameter, tuning.PlanetDiameter));

    private Transform WakeTransform()
    {
        PlanarVector wakeOrigin = new(
            field.Shape.PlanetPosition.X - field.Shape.WakeCenterBehindPlanet,
            field.Shape.PlanetPosition.Z);
        return RodTransform(
            wakeOrigin,
            PlanarVector.UnitX,
            tuning.WakeLength,
            tuning.WakeThickness,
            tuning.WakeHeight);
    }

    // Each drift band draws one procedural slab along its flow so the push
    // the player feels has a visible river to match. The slab's lateral
    // extent is the band's own Width so the visual tracks the push zone if
    // tuning moves it; depth stays a thin vertical extent, and color carries
    // the gentle-vs-swift reading.
    private static Transform CurrentTransform(
        DriftCurrentTuning band,
        float depth,
        float height)
    {
        PlanarVector direction = band.Direction.Scale(1.0 / band.Direction.Magnitude);
        return new Transform(
            PositionAtHeight(band.Center, height),
            PlanarFrame.ToEngineAttitude(PlanarFrame.HeadingOf(direction)),
            new Vector3(ToSingle(band.Length), depth, ToSingle(band.Width)));
    }

    /// <summary>
    /// The same band's authority: how far past its marked core the band still has
    /// something to say about the hull's line.
    /// </summary>
    private Transform AuthorityTransform(DriftCurrentTuning band, float depth, float height)
    {
        PlanarVector direction = band.Direction.Scale(1.0 / band.Direction.Magnitude);
        return new Transform(
            PositionAtHeight(band.Center, height),
            PlanarFrame.ToEngineAttitude(PlanarFrame.HeadingOf(direction)),
            new Vector3(
                ToSingle(band.Length),
                depth,
                ToSingle(band.Width * overlay.AuthorityWidthFactor)));
    }

    private static Transform RodTransform(
        PlanarVector origin,
        PlanarVector direction,
        double length,
        float thickness,
        float height)
    {
        double magnitude = direction.Magnitude;
        PlanarVector unit = magnitude <= 0.0 ? PlanarVector.Zero : direction.Scale(1.0 / magnitude);
        PlanarVector center = origin + unit.Scale(length * HalfLength);
        float span = checked((float)length);
        return new Transform(
            PositionAtHeight(center, height),
            PlanarFrame.ToEngineAttitude(magnitude <= 0.0 ? NeutralHeadingRadians : PlanarFrame.HeadingOf(direction)),
            new Vector3(span, thickness, thickness));
    }

    /// <summary>
    /// A flow rod stands on its own point of the world rather than reaching away
    /// from it, because what it reports is the flow at that point: a player reads
    /// the lattice as a field of samples, not as arrows flying out of them.
    /// </summary>
    private Transform FlowRodTransform(PlanarVector point, PlanarVector flow, double length)
    {
        double magnitude = flow.Magnitude;
        return new Transform(
            PositionAtHeight(point, overlay.FlowHeight),
            PlanarFrame.ToEngineAttitude(
                magnitude <= 0.0 ? NeutralHeadingRadians : PlanarFrame.HeadingOf(flow)),
            new Vector3(checked((float)length), overlay.FlowRodThickness, overlay.FlowRodThickness));
    }

    private static Transform MarkerTransform(PlanarVector position, float height, float? size)
    {
        float diameter = size ?? 0.0f;
        return new Transform(
            PositionAtHeight(position, height),
            PlanarFrame.ToEngineAttitude(NeutralHeadingRadians),
            new Vector3(diameter, diameter, diameter));
    }

    private static Vector3 PositionAtHeight(PlanarVector position, float height) => new(
        ToSingle(position.X),
        height,
        ToSingle(position.Z));

    private static float ToSingle(double value) => checked((float)value);

    /// <summary>
    /// Releases the render and UI handles this projection opened. Shutdown
    /// retires the retained snapshot before this runs, so no snapshot still
    /// points at a handle being put down. A generated lease wrapper releases
    /// through the staged call it is issued in, and once the runtime is
    /// terminal it drops its release instead of issuing one, so this is safe on
    /// a live turn and at teardown alike.
    /// </summary>
    public void Dispose()
    {
        if (released)
        {
            return;
        }

        released = true;
        hudStream.Dispose();
        starAppearance.Dispose();
        centerMarkerAppearance.Dispose();
        debugVectorAppearance.Dispose();
        flowAppearance.Dispose();
        pathAppearance.Dispose();
        velocityAppearance.Dispose();
        declinedAppearance.Dispose();
        swiftAuthorityAppearance.Dispose();
        gentleAuthorityAppearance.Dispose();
        swiftAppearance.Dispose();
        gentleAppearance.Dispose();
        wakeAppearance.Dispose();
        planetAppearance.Dispose();
        shipAppearance.Dispose();
    }

    internal void RetireRetainedSnapshot()
    {
        if (retainedSnapshotRetired)
        {
            return;
        }

        // This call has no generated lease-wrapper state to advance. Mark it
        // complete only after the Engine accepts the staged empty snapshot so
        // a failed Shutdown remains safely retryable.
        appearance.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty);
        retainedSnapshotRetired = true;
    }

    private Appearance CreateCube(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(PrimitiveGeometry.Cube, Wireframe: false, color));

    private Appearance CreateSphere(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(PrimitiveGeometry.Sphere, Wireframe: false, color));

    // DOM-layer HUD facts: where the ship points and how fast it goes, plus the
    // handling values a tuning pass needs — commanded thrust as a share of what
    // the drive can give, the acceleration actually felt, the current turn rate,
    // the coupling the hull is answering the environment at, the flow the hull
    // is sitting in, and how far apart the two sides of the effector pair have
    // gotten. The Engine injects the runtime identity envelope around this value.
    private void PublishHud(FlightReadout readout, FlightTelemetrySnapshot telemetry)
    {
        double acceleration = Math.Sqrt(
            (telemetry.ForwardAcceleration * telemetry.ForwardAcceleration)
            + (telemetry.LateralAcceleration * telemetry.LateralAcceleration));
        double flow = LocalFlowAt(readout.Position).Magnitude;
        StructuredValueNode[] nodes =
        [
            new(StructuredValueKind.Object, 0, 0, 0, 0, 0, 0, 0, 8),
            new(StructuredValueKind.Number, 0, readout.HeadingRadians, 0, 7, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, PlanarSpeed(readout.LinearVelocity), 7, 5, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, telemetry.DriveEffort, 12, 6, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, acceleration, 18, 5, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, readout.AngularVelocity, 23, 4, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, telemetry.Coupling, 27, 8, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, flow, 35, 4, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, telemetry.HeadingAsymmetry, 39, 4, 0, 0, 0, 0),
        ];
        ui.PublishProjection(new UiProjection(
            hudStream,
            checked(++hudSequence),
            new UiValue(
                nodes,
                (uint[])[1, 2, 3, 4, 5, 6, 7, 8],
                0,
                "headingspeedthrustaccelturncouplingflowasym"u8.ToArray())));
    }

    private static double PlanarSpeed(PlanarVector velocity) => Math.Sqrt(
        velocity.X * velocity.X + velocity.Z * velocity.Z);
}

internal enum SpaceAppearanceObject : ulong
{
    Ship = 1,
    Planet = 2,
    Wake = 3,
    GentleCurrent = 4,
    SwiftCurrent = 5,
    GentleAuthority = 6,
    SwiftAuthority = 7,
    Velocity = 8,
}
