using System;
using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Presentation;

/// <summary>
/// Product-owned meaning for a small set of Engine-rendered Space facts.
/// </summary>
internal sealed class SpacePresentation
{
    // Content identity of the authored ship dart; admitted product content is
    // keyed by its content-root-relative path.
    private const string ShipMeshPath = "meshes/ship.json";
    private const string HudStreamName = "rusty-space";
    private const string HudContract = "rusty.space.hud";
    private const float NeutralHeadingRadians = 0.0f;
    private const float HalfLength = 0.5f;
    private const float UniformScale = 1.0f;

    private readonly IAppearanceService appearance;
    private readonly IUiService ui;
    private readonly FieldTuning fieldTuning;
    private readonly SpacePresentationTuning tuning;
    private readonly Appearance shipAppearance;
    private readonly Appearance planetAppearance;
    private readonly Appearance wakeAppearance;
    private readonly UiStream hudStream;
    private ulong hudSequence;

    internal SpacePresentation(
        IAppearanceService appearance,
        IUiService ui,
        FieldTuning fieldTuning,
        SpacePresentationTuning tuning)
    {
        this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.fieldTuning = fieldTuning.Validate();
        this.tuning = tuning.Validate();
        shipAppearance = CreateShipMesh(this.tuning.ShipColor);
        planetAppearance = CreateSphere(this.tuning.PlanetColor);
        wakeAppearance = CreateCube(this.tuning.WakeColor);
        hudStream = this.ui.OpenStream(new UiStreamRequest(HudStreamName, HudContract));
    }

    internal void Publish(FlightReadout readout)
    {
        PublishAppearance(readout);
        PublishHud(readout);
    }

    private void PublishAppearance(FlightReadout readout)
    {
        AppearanceFact[] facts =
        [
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Ship,
                ShipTransform(readout),
                shipAppearance,
                Visible: true,
                RenderLayer.Scene),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Planet,
                PlanetTransform(),
                planetAppearance,
                Visible: true,
                RenderLayer.Scene),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Wake,
                WakeTransform(),
                wakeAppearance,
                Visible: true,
                RenderLayer.Scene),
        ];
        appearance.PublishSnapshot(facts);
    }

    // DOM-layer HUD facts: planar heading (radians) and planar speed. The
    // Engine injects the runtime identity envelope around this value.
    private void PublishHud(FlightReadout readout)
    {
        double speed = PlanarSpeed(readout.LinearVelocity);
        StructuredValueNode[] nodes =
        [
            new(StructuredValueKind.Object, 0, 0, 0, 0, 0, 0, 0, 2),
            new(StructuredValueKind.Number, 0, readout.HeadingRadians, 0, 7, 0, 0, 0, 0),
            new(StructuredValueKind.Number, 0, speed, 7, 5, 0, 0, 0, 0),
        ];
        ui.PublishProjection(new UiProjection(
            hudStream,
            checked(++hudSequence),
            new UiValue(nodes, (uint[])[1, 2], 0, "headingspeed"u8.ToArray())));
    }

    private static double PlanarSpeed(PlanarVector velocity) => Math.Sqrt(
        velocity.X * velocity.X + velocity.Z * velocity.Z);

    internal void Dispose()
    {
        hudStream.Dispose();
    }

    // The authored dart's nose runs along local +X, which matches the
    // presentation heading convention below.
    private Appearance CreateShipMesh(Color color) => appearance.CreateStaticMeshFromContent(
        new StaticMeshContentAppearanceRequest(ShipMeshPath, color));

    private Appearance CreateCube(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(PrimitiveGeometry.Cube, Wireframe: false, color));

    private Appearance CreateSphere(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(PrimitiveGeometry.Sphere, Wireframe: false, color));

    private Transform ShipTransform(FlightReadout readout) => new(
        PositionAtHeight(readout.Position, tuning.ShipHeight),
        RotationFromHeading(readout.HeadingRadians),
        new Vector3(UniformScale, UniformScale, UniformScale));

    private Transform PlanetTransform() => new(
        PositionAtHeight(fieldTuning.PlanetPosition, tuning.PlanetHeight),
        RotationFromHeading(NeutralHeadingRadians),
        new Vector3(tuning.PlanetDiameter, tuning.PlanetDiameter, tuning.PlanetDiameter));

    private Transform WakeTransform()
    {
        PlanarVector wakeOrigin = new(
            fieldTuning.PlanetPosition.X - fieldTuning.WakeCenterBehindPlanet,
            fieldTuning.PlanetPosition.Z);
        return RodTransform(
            wakeOrigin,
            PlanarVector.UnitX,
            tuning.WakeLength,
            tuning.WakeThickness,
            tuning.WakeHeight);
    }

    private static Transform RodTransform(
        PlanarVector origin,
        PlanarVector direction,
        float length,
        float thickness,
        float height)
    {
        PlanarVector center = origin + direction.Scale(length * HalfLength);
        return new Transform(
            PositionAtHeight(center, height),
            RotationFromHeading(Math.Atan2(direction.Z, direction.X)),
            new Vector3(length, thickness, thickness));
    }

    private static Vector3 PositionAtHeight(PlanarVector position, float height) => new(
        ToSingle(position.X),
        height,
        ToSingle(position.Z));

    // Planar headings run (cos h, sin h) in (X, Z), but the Engine's
    // right-handed Y-up quaternions rotate +h about +Y so a body's local +X
    // points along (cos h, 0, -sin h). Negating h makes +X-nosed shapes face
    // the direction the planar flight model actually moves.
    private static Quaternion RotationFromHeading(double headingRadians) => Quaternion.CreateFromAxisAngle(
        Vector3.UnitY,
        -ToSingle(headingRadians));

    private static float ToSingle(double value) => checked((float)value);
}

internal enum SpaceAppearanceObject : ulong
{
    Ship = 1,
    Planet = 2,
    Wake = 3,
}
