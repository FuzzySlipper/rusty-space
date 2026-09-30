using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace Rusty.Space.Product.Bridge;

/// <summary>Authored meshing resolution and join clearances for the bridge solids.</summary>
internal sealed record BridgeRecipeTuning(
    float ExtractionMargin,
    float CutOvershoot,
    float DefaultCellSize,
    float SurfaceOffset,
    float Adaptivity,
    float FloorCellSize,
    float WallCellSize,
    float OverheadCellSize,
    float ConsoleCellSize,
    float SeatCellSize,
    float CabinetCellSize,
    float BeamOffsetX,
    float BeamHalfWidth,
    float BeamDrop,
    float StripFaceOverlap,
    BridgeSeatDefinition Seat)
{
    internal static BridgeRecipeTuning Defaults { get; } = new(
        ExtractionMargin: 0.3f, CutOvershoot: 0.1f,
        DefaultCellSize: 0.12f, SurfaceOffset: 0.0f, Adaptivity: 0.25f,
        FloorCellSize: 0.15f, WallCellSize: 0.12f, OverheadCellSize: 0.12f,
        ConsoleCellSize: 0.05f, SeatCellSize: 0.06f, CabinetCellSize: 0.08f,
        BeamOffsetX: 1.0f, BeamHalfWidth: 0.09f, BeamDrop: 0.25f,
        StripFaceOverlap: 0.02f,
        Seat: new(PedestalHalfWidth: 0.15f, PedestalTop: 0.42f,
            CushionHalfWidth: 0.35f, CushionTop: 0.55f,
            BackRearOffsetX: -0.40f, BackFrontOffsetX: -0.25f, BackTop: 1.25f));

    internal BridgeRecipeTuning Validate()
    {
        if (new[] { ExtractionMargin, CutOvershoot, DefaultCellSize, FloorCellSize,
            WallCellSize, OverheadCellSize, ConsoleCellSize, SeatCellSize, CabinetCellSize,
            BeamOffsetX, BeamHalfWidth, BeamDrop, StripFaceOverlap }
            .Any(value => !float.IsFinite(value) || value <= 0.0f))
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeRecipeTuning));
        }
        if (!float.IsFinite(SurfaceOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(SurfaceOffset));
        }
        if (!float.IsFinite(Adaptivity) || Adaptivity < 0.0f || Adaptivity > 1.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Adaptivity));
        }
        Seat.Validate();
        return this;
    }

    internal RecipeSampling Sampling => new(
        DefaultCellSize, SurfaceOffset, Adaptivity, ImplicitMaterialBoundaryMode.Interpolated);
}

/// <summary>Seat solids measured from the layout's seat center and finished floor.</summary>
internal sealed record BridgeSeatDefinition(
    float PedestalHalfWidth, float PedestalTop,
    float CushionHalfWidth, float CushionTop,
    float BackRearOffsetX, float BackFrontOffsetX, float BackTop)
{
    internal BridgeSeatDefinition Validate()
    {
        if (!float.IsFinite(PedestalHalfWidth) || PedestalHalfWidth <= 0.0f
            || !float.IsFinite(CushionHalfWidth) || CushionHalfWidth < PedestalHalfWidth
            || !float.IsFinite(PedestalTop) || PedestalTop <= 0.0f
            || !float.IsFinite(CushionTop) || CushionTop <= PedestalTop
            || !float.IsFinite(BackTop) || BackTop <= CushionTop
            || !float.IsFinite(BackRearOffsetX) || !float.IsFinite(BackFrontOffsetX)
            || BackRearOffsetX >= BackFrontOffsetX)
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeSeatDefinition));
        }
        return this;
    }
}
