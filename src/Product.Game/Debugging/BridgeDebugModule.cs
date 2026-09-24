using Rusty.Engine.Debugging;
using Rusty.Space.Product.Bridge;

namespace Rusty.Space.Product.Debugging;

/// <summary>
/// Read-only live-debug view of the bridge set: what the recipe staged,
/// which identities the parent theater addresses, and where the attachment
/// facts landed. Reports state and never writes it.
/// </summary>
/// <remarks>
/// Public because the Engine emits catalog dispatch into the product
/// composition, which sees Product.Game as a referenced assembly. Only the
/// composition root constructs it; the commands read, never mutate.
/// </remarks>
public sealed class BridgeDebugModule : IDebugCommandModule
{
    private readonly BridgeSet bridge;

    internal BridgeDebugModule(BridgeSet bridge)
        => this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

    [DebugCommand("space.bridge", Description = "Shows the staged bridge set: part names, addressable identities, and attachment placements.")]
    public string Bridge()
    {
        BridgeLayout layout = bridge.Layout;
        BridgePlacements placements = bridge.Placements;
        string[] names = [.. bridge.PartNames];
        string surfaces = string.Join(", ", names.Take(bridge.ImplicitSurfaceCount));
        string parts = string.Join(", ", names.Skip(bridge.ImplicitSurfaceCount));
        return FormattableString.Invariant(
            $"""
            room        {layout.RoomLengthX:F2}x{layout.RoomWidthZ:F2}x{layout.WallHeight:F2} at ({layout.Anchor.X:F2}, {layout.Anchor.Y:F2}, {layout.Anchor.Z:F2})
            surfaces    {bridge.ImplicitSurfaceCount} implicit: {surfaces}
            parts       {names.Length - bridge.ImplicitSurfaceCount} presentation: {parts}
            seated eye  ({placements.SeatedEye.Position.X:F2}, {placements.SeatedEye.Position.Y:F2}, {placements.SeatedEye.Position.Z:F2}) yaw {placements.SeatedEye.YawDegrees:F1} pitch {placements.SeatedEye.PitchDegrees:F1}
            approach    ({placements.ApproachView.Position.X:F2}, {placements.ApproachView.Position.Y:F2}, {placements.ApproachView.Position.Z:F2}) yaw {placements.ApproachView.YawDegrees:F1} pitch {placements.ApproachView.PitchDegrees:F1} focus ({placements.ApproachFocus.X:F2}, {placements.ApproachFocus.Y:F2}, {placements.ApproachFocus.Z:F2})
            instrument  ({placements.InstrumentCenter.X:F2}, {placements.InstrumentCenter.Y:F2}, {placements.InstrumentCenter.Z:F2}) facing ({placements.InstrumentNormal.X:F1}, {placements.InstrumentNormal.Y:F1}, {placements.InstrumentNormal.Z:F1})
            lights      overhead ({placements.OverheadLight.X:F2}, {placements.OverheadLight.Y:F2}, {placements.OverheadLight.Z:F2})  helm ({placements.HelmLight.X:F2}, {placements.HelmLight.Y:F2}, {placements.HelmLight.Z:F2})
            audio       ({placements.AudioAnchor.X:F2}, {placements.AudioAnchor.Y:F2}, {placements.AudioAnchor.Z:F2})
            prop pivot  ({placements.PropPivot.X:F2}, {placements.PropPivot.Y:F2}, {placements.PropPivot.Z:F2})
            """);
    }
}
