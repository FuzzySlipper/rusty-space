using System.Numerics;

namespace Rusty.Space.Product.Bridge;

/// <summary>Face clearances, sizes and local placements of the independently posed instruments.</summary>
internal sealed record BridgeInstrumentTuning(
    float BackClearance,
    float ControlStripInset,
    float DisplayThickness,
    float LampSize,
    float LampFaceGap,
    float StripLampSpacing,
    float NeedleFaceOffset,
    float NeedleHeight,
    Vector3 NeedleSize,
    float EngineeringDisplayFaceOffset,
    float EngineeringDisplayHeight,
    Vector3 EngineeringDisplaySize,
    float StatusLampHeight,
    float StatusLampWallInset,
    float TaskLampOffsetX)
{
    internal static BridgeInstrumentTuning Defaults { get; } = new(
        BackClearance: 0.02f, ControlStripInset: 0.2f, DisplayThickness: 0.03f,
        LampSize: 0.07f, LampFaceGap: 0.001f, StripLampSpacing: 0.5f,
        NeedleFaceOffset: -0.04f, NeedleHeight: 0.45f, NeedleSize: new(0.025f, 0.34f, 0.025f),
        EngineeringDisplayFaceOffset: 0.016f, EngineeringDisplayHeight: 1.2f,
        EngineeringDisplaySize: new(0.03f, 0.30f, 0.50f),
        StatusLampHeight: 1.45f, StatusLampWallInset: 0.25f, TaskLampOffsetX: 0.6f);

    internal BridgeInstrumentTuning Validate()
    {
        if (new[] { BackClearance, ControlStripInset, DisplayThickness, LampSize, LampFaceGap,
            StripLampSpacing, NeedleHeight, EngineeringDisplayFaceOffset, EngineeringDisplayHeight,
            StatusLampHeight, StatusLampWallInset, TaskLampOffsetX, NeedleSize.X, NeedleSize.Y,
            NeedleSize.Z, EngineeringDisplaySize.X, EngineeringDisplaySize.Y, EngineeringDisplaySize.Z }
            .Any(value => !float.IsFinite(value) || value <= 0.0f)
            || !float.IsFinite(NeedleFaceOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeInstrumentTuning));
        }
        return this;
    }

    internal float LampHalfSize => LampSize / 2.0f;
}

internal sealed record BridgeLightingTuning(float OverheadIntensity, float HelmIntensity, float Decay)
{
    internal static BridgeLightingTuning Defaults { get; } = new(2.0f, 1.0f, 2.0f);

    internal BridgeLightingTuning Validate()
    {
        if (new[] { OverheadIntensity, HelmIntensity, Decay }.Any(value => !float.IsFinite(value) || value < 0.0f))
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeLightingTuning));
        }
        return this;
    }
}
