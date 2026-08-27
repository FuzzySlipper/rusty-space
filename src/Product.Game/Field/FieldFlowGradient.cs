namespace Rusty.Space.Product.Field;

internal readonly record struct FieldFlowGradient(
    double FlowXByPositionX,
    double FlowXByPositionZ,
    double FlowZByPositionX,
    double FlowZByPositionZ)
{
    internal double AbsoluteMagnitude =>
        Math.Abs(FlowXByPositionX)
        + Math.Abs(FlowXByPositionZ)
        + Math.Abs(FlowZByPositionX)
        + Math.Abs(FlowZByPositionZ);

    internal static FieldFlowGradient Zero { get; } = new(0.0, 0.0, 0.0, 0.0);
}
