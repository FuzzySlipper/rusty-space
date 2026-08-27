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
    private const uint Visible = 1;
    private const uint Hidden = 0;
    private const uint NoReservedValue = 0;
    private const float DirectionEpsilon = 0.0001f;
    private const float NeutralHeadingRadians = 0.0f;
    private const float UnitScale = 1.0f;
    private const float HalfLength = 0.5f;

    private readonly IAppearanceService appearance;
    private readonly FieldTuning fieldTuning;
    private readonly SpacePresentationTuning tuning;
    private readonly AppearanceHandle shipAppearance;
    private readonly AppearanceHandle headingAppearance;
    private readonly AppearanceHandle velocityAppearance;
    private readonly AppearanceHandle planetAppearance;
    private readonly AppearanceHandle wakeAppearance;

    internal SpacePresentation(
        IAppearanceService appearance,
        FieldTuning fieldTuning,
        SpacePresentationTuning tuning)
    {
        this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        this.fieldTuning = fieldTuning.Validate();
        this.tuning = tuning.Validate();
        shipAppearance = CreateCube(this.tuning.ShipColor);
        headingAppearance = CreateCube(this.tuning.HeadingColor);
        velocityAppearance = CreateCube(this.tuning.VelocityColor);
        planetAppearance = CreateSphere(this.tuning.PlanetColor);
        wakeAppearance = CreateCube(this.tuning.WakeColor);
    }

    internal void Publish(FlightReadout readout)
    {
        AppearanceFact[] facts =
        [
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Ship,
                ShipTransform(readout),
                shipAppearance,
                Visible,
                NoReservedValue),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Heading,
                RodTransform(
                    readout.Position,
                    DirectionFromHeading(readout.HeadingRadians),
                    tuning.HeadingLength,
                    tuning.HeadingThickness,
                    tuning.HeadingHeight),
                headingAppearance,
                Visible,
                NoReservedValue),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Velocity,
                VelocityTransform(readout),
                velocityAppearance,
                VelocityVisibility(readout),
                NoReservedValue),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Planet,
                PlanetTransform(),
                planetAppearance,
                Visible,
                NoReservedValue),
            new AppearanceFact(
                (ulong)SpaceAppearanceObject.Wake,
                WakeTransform(),
                wakeAppearance,
                Visible,
                NoReservedValue),
        ];
        appearance.PublishSnapshot(facts);
    }

    private AppearanceHandle CreateCube(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(
            (uint)SpacePrimitiveGeometry.Cube,
            NoReservedValue,
            color));

    private AppearanceHandle CreateSphere(Color color) => appearance.CreatePrimitive(
        new PrimitiveAppearanceRequest(
            (uint)SpacePrimitiveGeometry.Sphere,
            NoReservedValue,
            color));

    private Transform ShipTransform(FlightReadout readout) => new(
        PositionAtHeight(readout.Position, tuning.ShipHeight),
        RotationFromHeading(readout.HeadingRadians),
        tuning.ShipScale);

    private Transform VelocityTransform(FlightReadout readout)
    {
        float speed = ToSingle(readout.LinearVelocity.Magnitude);
        PlanarVector direction = speed > DirectionEpsilon
            ? readout.LinearVelocity.Scale(UnitScale / speed)
            : DirectionFromHeading(readout.HeadingRadians);
        float length = Math.Clamp(
            speed * tuning.VelocitySeconds,
            tuning.MinimumVelocityLength,
            tuning.MaximumVelocityLength);
        return RodTransform(
            readout.Position,
            direction,
            length,
            tuning.VelocityThickness,
            tuning.VelocityHeight);
    }

    private static uint VelocityVisibility(FlightReadout readout) =>
        ToSingle(readout.LinearVelocity.Magnitude) > DirectionEpsilon ? Visible : Hidden;

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

    private static PlanarVector DirectionFromHeading(double headingRadians) => new(
        Math.Cos(headingRadians),
        Math.Sin(headingRadians));

    private static Quaternion RotationFromHeading(double headingRadians) => Quaternion.CreateFromAxisAngle(
        Vector3.UnitY,
        ToSingle(headingRadians));

    private static float ToSingle(double value) => checked((float)value);
}

internal enum SpaceAppearanceObject : ulong
{
    Ship = 1,
    Heading = 2,
    Velocity = 3,
    Planet = 4,
    Wake = 5,
}

internal enum SpacePrimitiveGeometry : uint
{
    Cube = 1,
    Sphere = 2,
}
