using System;
using System.Numerics;
using Rusty.Engine;

namespace Rusty.Space.Product.Bridge;

/// <summary>
/// Named dimensions, placements, and palette for the one parametric bridge
/// set. Everything the room measures is derived from these values plus the
/// anchor, so a deliberate dimension change never means hand-editing a mesh.
/// </summary>
/// <remarks>
/// <para>
/// Local frame: origin at the room center on the finished-floor top, +X east
/// toward the helm wall, +Z north toward the side-instrument wall, +Y up. The
/// doorway leaves through the south wall. World placement is the anchor added
/// to the local point; the set never moves after construction.
/// </para>
/// <para>
/// This record carries no Engine handles and performs no extraction. It is
/// pure product meaning, which is why layout math is covered by unit tests
/// while <see cref="BridgeRecipe"/> and <see cref="BridgeSet"/> need the host.
/// </para>
/// </remarks>
internal sealed record BridgeLayout(
    Vector3 Anchor,
    float RoomLengthX,
    float RoomWidthZ,
    float WallHeight,
    float WallThickness,
    float FloorThickness,
    float CeilingThickness,
    float DoorwayWidth,
    float DoorwayHeight,
    // Helm console against the east wall, worked from a seat to its west.
    float ConsoleDepthX,
    float ConsoleWidthZ,
    float ConsoleHeight,
    float ConsoleEastInset,
    float DisplayWidth,
    float DisplayHeight,
    float DisplayCenterHeight,
    float RecessDepth,
    float BezelMargin,
    float ControlStripHeight,
    float ControlStripProtrusion,
    float ControlStripCenterHeight,
    // Side instrument housing north of the helm, facing the same seat.
    float SideHousingDepthX,
    float SideHousingWidthZ,
    float SideHousingHeight,
    float SideHousingGap,
    float SideDisplayWidth,
    float SideDisplayHeight,
    float SideDisplayCenterHeight,
    // Seated position west of the console.
    Vector3 SeatCenter,
    float EyeHeightAboveFloor,
    // Engineering corner in the southwest: tall cabinet on the west wall plus
    // a low cabinet on the south wall, and the loose slate hovering nearby.
    float CabinetDepth,
    float CabinetHeight,
    float CabinetLengthZ,
    float LowCabinetHeight,
    float LowCabinetLengthX,
    Vector3 PropAnchor,
    Vector3 PropSize,
    // Practical light and audio-source placements the set stages.
    Vector3 OverheadLightLocal,
    float OverheadLightRange,
    Vector3 HelmLightLocal,
    float HelmLightRange,
    Vector3 AudioAnchorLocal,
    BridgePalette Palette)
{
    /// <summary>
    /// How far proud of the recess back the instrument face sits. The display
    /// module plugs the opening without touching the cut walls.
    /// </summary>
    internal const float InstrumentBackClearance = 0.02f;

    /// <summary>
    /// How far the control-strip island stops short of the console edges. The
    /// strip must fit the face it protrudes from.
    /// </summary>
    internal const float ControlStripZInset = 0.2f;

    internal static BridgeLayout Defaults { get; } = new(
        Anchor: new Vector3(-9.5f, 0.0f, -9.5f),
        RoomLengthX: 6.4f,
        RoomWidthZ: 4.6f,
        WallHeight: 2.6f,
        WallThickness: 0.25f,
        FloorThickness: 0.30f,
        CeilingThickness: 0.25f,
        DoorwayWidth: 1.1f,
        DoorwayHeight: 2.0f,
        ConsoleDepthX: 1.2f,
        ConsoleWidthZ: 2.2f,
        ConsoleHeight: 1.3f,
        ConsoleEastInset: 0.3f,
        DisplayWidth: 1.5f,
        DisplayHeight: 0.55f,
        DisplayCenterHeight: 0.84f,
        RecessDepth: 0.28f,
        BezelMargin: 0.12f,
        ControlStripHeight: 0.14f,
        ControlStripProtrusion: 0.06f,
        ControlStripCenterHeight: 0.36f,
        SideHousingDepthX: 1.2f,
        SideHousingWidthZ: 0.95f,
        SideHousingHeight: 1.5f,
        SideHousingGap: 0.15f,
        SideDisplayWidth: 0.6f,
        SideDisplayHeight: 0.4f,
        SideDisplayCenterHeight: 1.0f,
        SeatCenter: new Vector3(-0.15f, 0.0f, 0.0f),
        EyeHeightAboveFloor: 1.32f,
        CabinetDepth: 0.8f,
        CabinetHeight: 1.7f,
        CabinetLengthZ: 1.5f,
        LowCabinetHeight: 0.9f,
        LowCabinetLengthX: 1.2f,
        PropAnchor: new Vector3(-1.6f, 1.05f, -1.5f),
        PropSize: new Vector3(0.34f, 0.06f, 0.24f),
        OverheadLightLocal: new Vector3(0.0f, 2.2f, 0.0f),
        OverheadLightRange: 9.0f,
        HelmLightLocal: new Vector3(1.0f, 1.9f, 0.0f),
        HelmLightRange: 4.5f,
        AudioAnchorLocal: new Vector3(0.0f, 1.6f, 0.0f),
        Palette: BridgePalette.Defaults);

    internal BridgeLayout Validate()
    {
        ValidatePositive(RoomLengthX, nameof(RoomLengthX));
        ValidatePositive(RoomWidthZ, nameof(RoomWidthZ));
        ValidatePositive(WallHeight, nameof(WallHeight));
        ValidatePositive(WallThickness, nameof(WallThickness));
        ValidatePositive(FloorThickness, nameof(FloorThickness));
        ValidatePositive(CeilingThickness, nameof(CeilingThickness));
        ValidatePositive(DoorwayWidth, nameof(DoorwayWidth));
        // A full-height doorway leaves no lintel above it: the south-wall
        // extraction would go degenerate.
        if (!float.IsFinite(DoorwayHeight) || DoorwayHeight <= 0.0f || DoorwayHeight >= WallHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(DoorwayHeight));
        }

        if (!float.IsFinite(DoorwayWidth) || DoorwayWidth >= RoomLengthX)
        {
            throw new ArgumentOutOfRangeException(nameof(DoorwayWidth));
        }

        ValidatePositive(ConsoleDepthX, nameof(ConsoleDepthX));
        ValidatePositive(ConsoleWidthZ, nameof(ConsoleWidthZ));
        if (ConsoleWidthZ <= 2.0f * ControlStripZInset)
        {
            throw new ArgumentOutOfRangeException(nameof(ConsoleWidthZ));
        }

        ValidatePositive(ConsoleHeight, nameof(ConsoleHeight));
        ValidateFinite(ConsoleEastInset, nameof(ConsoleEastInset));
        if (ConsoleEastInset < 0.0f || ConsoleEastInset + ConsoleDepthX > RoomLengthX / 2.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(ConsoleEastInset));
        }

        // The display opening plus its bezel rim must fit the console face the
        // seat reads, and the recess must stop inside the console body.
        ValidatePositive(DisplayWidth, nameof(DisplayWidth));
        ValidatePositive(DisplayHeight, nameof(DisplayHeight));
        if (DisplayWidth + (2.0f * BezelMargin) > ConsoleWidthZ)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayWidth));
        }

        if (DisplayCenterHeight - (DisplayHeight / 2.0f) - BezelMargin < ControlStripCenterHeight + (ControlStripHeight / 2.0f))
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayCenterHeight));
        }

        if (DisplayCenterHeight + (DisplayHeight / 2.0f) + BezelMargin > ConsoleHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayCenterHeight));
        }

        ValidatePositive(RecessDepth, nameof(RecessDepth));
        if (RecessDepth >= ConsoleDepthX || RecessDepth <= InstrumentBackClearance)
        {
            throw new ArgumentOutOfRangeException(nameof(RecessDepth));
        }

        ValidatePositive(BezelMargin, nameof(BezelMargin));
        ValidatePositive(ControlStripHeight, nameof(ControlStripHeight));
        ValidatePositive(ControlStripProtrusion, nameof(ControlStripProtrusion));
        ValidatePositive(SideHousingDepthX, nameof(SideHousingDepthX));
        ValidatePositive(SideHousingWidthZ, nameof(SideHousingWidthZ));
        ValidatePositive(SideHousingHeight, nameof(SideHousingHeight));
        ValidateFinite(SideHousingGap, nameof(SideHousingGap));
        if (SideHousingGap < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(SideHousingGap));
        }

        // The side housing shares the helm's east inset: it must stop inside
        // the room, clear of the north wall and the console beside it, and its
        // own recess must stop inside its own body.
        if (SideHousingFaceX + SideHousingDepthX > RoomLengthX / 2.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(SideHousingDepthX));
        }

        if (SideHousingCenterZ + (SideHousingWidthZ / 2.0f) > RoomWidthZ / 2.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(SideHousingWidthZ));
        }

        if (SideHousingCenterZ - (SideHousingWidthZ / 2.0f) <= ConsoleWidthZ / 2.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(SideHousingGap));
        }

        if (RecessDepth >= SideHousingDepthX)
        {
            throw new ArgumentOutOfRangeException(nameof(SideHousingDepthX));
        }

        ValidatePositive(SideDisplayWidth, nameof(SideDisplayWidth));
        ValidatePositive(SideDisplayHeight, nameof(SideDisplayHeight));
        if (SideDisplayWidth + (2.0f * BezelMargin) > SideHousingWidthZ)
        {
            throw new ArgumentOutOfRangeException(nameof(SideDisplayWidth));
        }

        if (SideDisplayCenterHeight - (SideDisplayHeight / 2.0f) < BezelMargin
            || SideDisplayCenterHeight + (SideDisplayHeight / 2.0f) + BezelMargin > SideHousingHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(SideDisplayCenterHeight));
        }

        ValidatePositive(EyeHeightAboveFloor, nameof(EyeHeightAboveFloor));
        if (EyeHeightAboveFloor > WallHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(EyeHeightAboveFloor));
        }

        ValidatePositive(CabinetDepth, nameof(CabinetDepth));
        ValidatePositive(CabinetHeight, nameof(CabinetHeight));
        ValidatePositive(CabinetLengthZ, nameof(CabinetLengthZ));
        ValidatePositive(LowCabinetHeight, nameof(LowCabinetHeight));
        ValidatePositive(LowCabinetLengthX, nameof(LowCabinetLengthX));
        ValidatePositive(PropSize.X, nameof(PropSize));
        ValidatePositive(PropSize.Y, nameof(PropSize));
        ValidatePositive(PropSize.Z, nameof(PropSize));
        ValidatePositive(OverheadLightRange, nameof(OverheadLightRange));
        ValidatePositive(HelmLightRange, nameof(HelmLightRange));
        ValidateFiniteVector(Anchor, nameof(Anchor));
        ValidateFiniteVector(SeatCenter, nameof(SeatCenter));
        ValidateFiniteVector(PropAnchor, nameof(PropAnchor));
        ValidateFiniteVector(OverheadLightLocal, nameof(OverheadLightLocal));
        ValidateFiniteVector(HelmLightLocal, nameof(HelmLightLocal));
        ValidateFiniteVector(AudioAnchorLocal, nameof(AudioAnchorLocal));
        Palette.Validate();
        return this;
    }

    /// <summary>
    /// The world-space attachment facts the parent theater consumes: where the
    /// seated and approach cameras belong, where the instrument faces point,
    /// and where lights and the audio source stage. Pure derivation, so the
    /// parent owns every reaction and this task owns only the facts.
    /// </summary>
    internal BridgePlacements Placements
    {
        get
        {
            Vector3 eye = ToWorld(new Vector3(SeatCenter.X, EyeHeightAboveFloor, SeatCenter.Z));
            Vector3 instrument = InstrumentCenter;
            (double seatedYaw, double seatedPitch) = YawPitchFor(eye, instrument);
            // The approach view stands south of the room on the doorway axis
            // and reads the helm face through the doorway gap: the segment
            // from camera to focus must cross the south-wall plane inside the
            // doorway width and below the lintel, which the layout tests pin.
            Vector3 approach = ToWorld(ApproachSourceLocal);
            Vector3 approachTarget = ToWorld(ApproachFocusLocal);
            (double approachYaw, double approachPitch) = YawPitchFor(approach, approachTarget);
            return new BridgePlacements(
                new CameraPose(eye, seatedPitch, seatedYaw),
                new CameraPose(approach, approachPitch, approachYaw),
                approachTarget,
                instrument,
                InstrumentNormal,
                SideInstrumentCenter,
                SideInstrumentNormal,
                ToWorld(OverheadLightLocal),
                ToWorld(HelmLightLocal),
                ToWorld(AudioAnchorLocal),
                ToWorld(PropAnchor));
        }
    }

    /// <summary>
    /// Where the approach camera stands, room-local: south of the doorway,
    /// offset west so the sightline to the helm face threads the gap.
    /// </summary>
    internal Vector3 ApproachSourceLocal => new(
        -((DoorwayWidth / 2.0f) + 0.65f),
        1.6f,
        -((RoomWidthZ / 2.0f) + 2.9f));

    /// <summary>
    /// What the approach camera reads, room-local: the center of the main
    /// display opening on the helm face.
    /// </summary>
    internal Vector3 ApproachFocusLocal => new(ConsoleFaceX, DisplayCenterHeight, 0.0f);

    internal Vector3 ToWorld(Vector3 local) => Anchor + local;

    /// <summary>West face of the helm console: the plane the seat reads.</summary>
    internal float ConsoleFaceX => (RoomLengthX / 2.0f) - ConsoleEastInset - ConsoleDepthX;

    /// <summary>Center of the recessed main display, in world space.</summary>
    internal Vector3 InstrumentCenter =>
        ToWorld(new Vector3(ConsoleFaceX + RecessDepth - InstrumentBackClearance, DisplayCenterHeight, 0.0f));

    /// <summary>Direction the main display faces: toward the seat.</summary>
    internal static Vector3 InstrumentNormal => -Vector3.UnitX;

    internal float SideHousingFaceX => (RoomLengthX / 2.0f) - ConsoleEastInset - SideHousingDepthX;

    internal float SideHousingCenterZ => (ConsoleWidthZ / 2.0f) + SideHousingGap + (SideHousingWidthZ / 2.0f);

    internal Vector3 SideInstrumentCenter => ToWorld(new Vector3(
        SideHousingFaceX + RecessDepth - InstrumentBackClearance,
        SideDisplayCenterHeight,
        SideHousingCenterZ));

    internal static Vector3 SideInstrumentNormal => -Vector3.UnitX;

    /// <summary>
    /// Engine camera yaw/pitch that looks from a source at a target. Yaw zero
    /// faces -Z with positive yaw toward +X; negative pitch looks down.
    /// </summary>
    internal static (double YawDegrees, double PitchDegrees) YawPitchFor(Vector3 source, Vector3 target)
    {
        Vector3 offset = target - source;
        double length = offset.Length();
        if (length <= 0.0)
        {
            return (0.0, 0.0);
        }

        double yaw = Math.Atan2(offset.X, -offset.Z) * 180.0 / Math.PI;
        double pitch = Math.Asin(Math.Clamp(offset.Y / length, -1.0, 1.0)) * 180.0 / Math.PI;
        return (yaw, pitch);
    }

    private static void ValidatePositive(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFiniteVector(Vector3 value, string parameterName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>
/// The restrained initial palette: broad structural tones plus the phosphor
/// display colors. Wear, labels, and per-part variation follow the layout.
/// </summary>
internal sealed record BridgePalette(
    Color Floor,
    Color Walls,
    Color Ceiling,
    Color Console,
    Color SeatFabric,
    Color Cabinet,
    Color Prop,
    Color MainDisplay,
    Color SideDisplay,
    Color EngineeringDisplay,
    Color OverheadGlow,
    Color HelmGlow)
{
    internal static BridgePalette Defaults { get; } = new(
        Floor: new Color(0.23f, 0.24f, 0.27f, 1.0f),
        Walls: new Color(0.38f, 0.40f, 0.38f, 1.0f),
        Ceiling: new Color(0.20f, 0.21f, 0.23f, 1.0f),
        Console: new Color(0.18f, 0.22f, 0.26f, 1.0f),
        SeatFabric: new Color(0.45f, 0.28f, 0.18f, 1.0f),
        Cabinet: new Color(0.42f, 0.36f, 0.22f, 1.0f),
        Prop: new Color(0.55f, 0.58f, 0.60f, 1.0f),
        MainDisplay: new Color(0.30f, 0.90f, 0.80f, 1.0f),
        SideDisplay: new Color(1.00f, 0.65f, 0.20f, 1.0f),
        EngineeringDisplay: new Color(0.65f, 0.50f, 1.00f, 1.0f),
        OverheadGlow: new Color(1.00f, 0.85f, 0.65f, 1.0f),
        HelmGlow: new Color(0.55f, 0.80f, 1.00f, 1.0f));

    internal BridgePalette Validate()
    {
        ValidateColor(Floor, nameof(Floor));
        ValidateColor(Walls, nameof(Walls));
        ValidateColor(Ceiling, nameof(Ceiling));
        ValidateColor(Console, nameof(Console));
        ValidateColor(SeatFabric, nameof(SeatFabric));
        ValidateColor(Cabinet, nameof(Cabinet));
        ValidateColor(Prop, nameof(Prop));
        ValidateColor(MainDisplay, nameof(MainDisplay));
        ValidateColor(SideDisplay, nameof(SideDisplay));
        ValidateColor(EngineeringDisplay, nameof(EngineeringDisplay));
        ValidateColor(OverheadGlow, nameof(OverheadGlow));
        ValidateColor(HelmGlow, nameof(HelmGlow));
        return this;
    }

    private static void ValidateColor(Color color, string parameterName)
    {
        if (!float.IsFinite(color.R) || color.R < 0.0f || color.R > 1.0f
            || !float.IsFinite(color.G) || color.G < 0.0f || color.G > 1.0f
            || !float.IsFinite(color.B) || color.B < 0.0f || color.B > 1.0f
            || !float.IsFinite(color.A) || color.A < 0.0f || color.A > 1.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>
/// World-space attachment facts for the parent theater. The set is stationary:
/// every value here is fixed at construction and never follows the ship.
/// </summary>
internal sealed record BridgePlacements(
    CameraPose SeatedEye,
    CameraPose ApproachView,
    Vector3 ApproachFocus,
    Vector3 InstrumentCenter,
    Vector3 InstrumentNormal,
    Vector3 SideInstrumentCenter,
    Vector3 SideInstrumentNormal,
    Vector3 OverheadLight,
    Vector3 HelmLight,
    Vector3 AudioAnchor,
    Vector3 PropPivot);
