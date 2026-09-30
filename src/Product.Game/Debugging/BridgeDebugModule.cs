using Rusty.Engine.Debugging;
using Rusty.Space.Product.Bridge;

namespace Rusty.Space.Product.Debugging;

/// <summary>
/// Read-only live-debug view of the bridge set and its theater: what the
/// recipe staged, which identities the reactions address, where the
/// attachment facts landed, and what the filters are doing this turn.
/// Reports state and never writes it.
/// </summary>
/// <remarks>
/// Public because the Engine emits catalog dispatch into the product
/// composition, which sees Product.Game as a referenced assembly. Only the
/// composition root constructs it; the commands read, never mutate.
/// </remarks>
public sealed class BridgeDebugModule : IDebugCommandModule
{
    private readonly BridgeSet bridge;
    private readonly BridgeTheater theater;
    private readonly Func<bool> seated;

    internal BridgeDebugModule(BridgeSet bridge, BridgeTheater theater, Func<bool> seated)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.theater = theater ?? throw new ArgumentNullException(nameof(theater));
        this.seated = seated ?? throw new ArgumentNullException(nameof(seated));
    }

    [DebugCommand("space.bridge", Description = "Shows the staged bridge set and its theater: parts, placements, filters, and reactions.")]
    public string Bridge()
    {
        BridgeLayout layout = bridge.Layout;
        BridgePlacements placements = bridge.Placements;
        TheaterTuning tuning = theater.Tuning;
        string[] names = [.. bridge.PartNames];
        string surfaces = string.Join(", ", names.Take(bridge.ImplicitSurfaceCount));
        string parts = string.Join(", ", names.Skip(bridge.ImplicitSurfaceCount));
        return FormattableString.Invariant(
            $"""
            room        {layout.RoomLengthX:F2}x{layout.RoomWidthZ:F2}x{layout.WallHeight:F2} at ({layout.Anchor.X:F2}, {layout.Anchor.Y:F2}, {layout.Anchor.Z:F2})
            surfaces    {bridge.ImplicitSurfaceCount} implicit: {surfaces}
            parts       {names.Length - bridge.ImplicitSurfaceCount} presentation: {parts}
            seated      {(seated() ? "at helm" : "at chart")}
            seated eye  ({placements.SeatedEye.Position.X:F2}, {placements.SeatedEye.Position.Y:F2}, {placements.SeatedEye.Position.Z:F2}) yaw {placements.SeatedEye.YawDegrees:F1} pitch {placements.SeatedEye.PitchDegrees:F1}
            instrument  ({placements.InstrumentCenter.X:F2}, {placements.InstrumentCenter.Y:F2}, {placements.InstrumentCenter.Z:F2}) facing ({placements.InstrumentNormal.X:F1}, {placements.InstrumentNormal.Y:F1}, {placements.InstrumentNormal.Z:F1})
            lights      overhead ({placements.OverheadLight.X:F2}, {placements.OverheadLight.Y:F2}, {placements.OverheadLight.Z:F2})  helm ({placements.HelmLight.X:F2}, {placements.HelmLight.Y:F2}, {placements.HelmLight.Z:F2})
            audio       ({placements.AudioAnchor.X:F2}, {placements.AudioAnchor.Y:F2}, {placements.AudioAnchor.Z:F2})
            prop pivot  ({placements.PropPivot.X:F2}, {placements.PropPivot.Y:F2}, {placements.PropPivot.Z:F2})
            filters     camera {tuning.CameraLeanCutoffHz:F1}Hz  prop {tuning.PropCutoffHz:F1}Hz  spool {tuning.SpoolCutoffHz:F1}Hz  load {tuning.LoadCutoffHz:F1}Hz  flicker {tuning.FaultFlickerHz:F1}Hz
            theater     spool {theater.FilteredSpool:F2}  load {theater.FilteredLoad:F2}  needle {theater.NeedleDegrees:F0}°  hum {theater.HumPitch:F2}x {theater.HumVolume:F2}  {theater.FaultText}  lamps fault:{(theater.Lamps.FaultLit ? "lit" : "rest")} ready:{(theater.Lamps.ReadyLit ? "lit" : "rest")}
            """);
    }
}
