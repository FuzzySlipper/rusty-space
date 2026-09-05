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
    private const ulong FirstStarObjectId = 1_000UL;

    private readonly IGraphicsService appearance;
    private readonly IUiService ui;
    private readonly FieldTuning fieldTuning;
    private readonly SpacePresentationTuning tuning;
    private readonly Appearance shipAppearance;
    private readonly Appearance planetAppearance;
    private readonly Appearance wakeAppearance;
    private readonly Appearance starAppearance;
    private readonly UiStream hudStream;
    private ulong hudSequence;
    private bool retainedSnapshotRetired;

    internal SpacePresentation(
        IGraphicsService appearance,
        IUiService ui,
        FieldTuning fieldTuning,
        SpacePresentationTuning tuning)
    {
        this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.fieldTuning = fieldTuning.Validate();
        this.tuning = tuning.Validate();

        // A failed create callback is discarded by the staged Engine call, so
        // this constructor deliberately does not issue individual release
        // calls that could desynchronize generated lease wrappers from a
        // later transaction rollback.
        shipAppearance = CreateShipMesh(this.tuning.ShipColor);
        planetAppearance = CreateSphere(this.tuning.PlanetColor);
        wakeAppearance = CreateCube(this.tuning.WakeColor);
        starAppearance = CreateSphere(this.tuning.StarColor);
        hudStream = this.ui.OpenStream(new UiStreamRequest(HudStreamName, HudContract));
    }

    internal void Publish(FlightReadout readout)
    {
        PublishAppearance(readout);
        PublishHud(readout);
    }

    private void PublishAppearance(FlightReadout readout)
    {
        int starWidth = checked((tuning.StarGridRadius * 2) + 1);
        int starCount = checked(starWidth * starWidth);
        AppearanceFact[] facts = new AppearanceFact[checked(starCount + 3)];
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
        PublishStars(facts.AsSpan(3));
        appearance.PublishSnapshot(facts);
    }

    private void PublishStars(Span<AppearanceFact> destination)
    {
        int index = 0;
        for (int gridZ = -tuning.StarGridRadius; gridZ <= tuning.StarGridRadius; gridZ++)
        {
            for (int gridX = -tuning.StarGridRadius; gridX <= tuning.StarGridRadius; gridX++)
            {
                float diameter = tuning.StarDiameter * StarScale(gridX, gridZ);
                destination[index] = new AppearanceFact(
                    checked(FirstStarObjectId + (ulong)index),
                    false,
                    0,
                    new Transform(
                        new Vector3(
                            (gridX * tuning.StarSpacing) + StarJitter(gridX, gridZ, 17),
                            tuning.StarHeight,
                            (gridZ * tuning.StarSpacing) + StarJitter(gridX, gridZ, 43)),
                        Quaternion.Identity,
                        new Vector3(diameter, diameter, diameter)),
                    starAppearance,
                    Visible: true,
                    RenderLayer.Scene);
                index++;
            }
        }
    }

    private static float StarJitter(int gridX, int gridZ, int salt)
    {
        int value = unchecked((gridX * 73_856_093) ^ (gridZ * 19_349_663) ^ salt);
        return ((uint)value % 1_001U) / 1_000.0f * 3.0f - 1.5f;
    }

    private static float StarScale(int gridX, int gridZ)
    {
        int value = unchecked((gridX * 83_492_791) ^ (gridZ * 2_971_215) ^ 101);
        return 0.70f + (((uint)value % 601U) / 1_000.0f);
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
