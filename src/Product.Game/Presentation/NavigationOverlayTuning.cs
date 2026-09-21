using System;
using Rusty.Engine;

namespace Rusty.Space.Product.Presentation;

/// <summary>
/// How the navigation view says what the environment is about to do to the ship.
/// </summary>
/// <remarks>
/// <para>
/// Each of these reads answers a different question, which is why they are drawn
/// differently and not merged into one undifferentiated overlay: which way the
/// hull is pointed, which way it is actually going, where the flow at a point
/// would take a coupled hull, how far a band's authority reaches past the slab
/// that marks it, and where the ship is on its way to.
/// </para>
/// <para>
/// Strengths are carried by length rather than color wherever a quantity has to
/// be read as more or less, because a length is comparable between neighbors at a
/// glance and a shade is not. The tuning-only vectors — each source's push and
/// each center of force — go on the Engine's debug layer, so a tuning pass can see
/// them without a player ever having to.
/// </para>
/// </remarks>
internal sealed record NavigationOverlayTuning(
    // Which way the hull is going, drawn beside the hull rather than on it, so a
    // ship pointing one way and drifting another reads as exactly that.
    float VelocityIndicatorThickness,
    float VelocityIndicatorLengthPerUnitSpeed,
    float VelocityIndicatorHeight,
    Color VelocityIndicatorColor,
    // Sample points along the projected line, far enough ahead to meet a current
    // before the ship does.
    float PathMarkerSize,
    float PathMarkerHeight,
    Color PathColor,
    // A lattice of local-flow rods, anchored on the world rather than on the ship,
    // each pointing where the flow at its point would carry a coupled hull.
    int FlowLatticeRadius,
    float FlowLatticeSpacing,
    float FlowRodThickness,
    float FlowRodLengthPerUnitSpeed,
    float FlowHeight,
    Color FlowColor,
    // A band's authority reaches past the slab that marks it: the outer slab is
    // where the band still has something to say, the inner one where it says it
    // at close to full strength. When the hull has wound its coupling down the
    // bands are drawn in the declined color, because on that trim they will not
    // catch it.
    float AuthorityWidthFactor,
    Color GentleAuthorityColor,
    Color SwiftAuthorityColor,
    Color DeclinedColor,
    // Tuning-only: each source's push and each center of force, on the debug
    // layer.
    float DebugVectorThickness,
    float DebugVectorLengthPerUnitForce,
    float DebugMarkerSize,
    float DebugHeight,
    Color DebugColor,
    Color DebugCenterColor,
    // Where the last contact left its mark: at the mount of the part that took it,
    // grown by how hard the hull arrived there.
    float StruckMarkSize,
    float StruckMarkPerUnitImpulse,
    float StruckMarkHeight,
    Color StruckMarkColor)
{
    private const int MinimumGridRadius = 1;
    private const int MaximumGridRadius = 8;
    private const float MinimumPositiveMagnitude = 0.0f;
    private const float MinimumColorComponent = 0.0f;
    private const float MaximumColorComponent = 1.0f;
    private const float MinimumAuthorityWidthFactor = 1.0f;

    internal NavigationOverlayTuning Validate()
    {
        ValidatePositiveFinite(VelocityIndicatorThickness, nameof(VelocityIndicatorThickness));
        ValidatePositiveFinite(
            VelocityIndicatorLengthPerUnitSpeed,
            nameof(VelocityIndicatorLengthPerUnitSpeed));
        ValidateFinite(VelocityIndicatorHeight, nameof(VelocityIndicatorHeight));
        ValidateColor(VelocityIndicatorColor, nameof(VelocityIndicatorColor));
        ValidatePositiveFinite(PathMarkerSize, nameof(PathMarkerSize));
        ValidateFinite(PathMarkerHeight, nameof(PathMarkerHeight));
        ValidateColor(PathColor, nameof(PathColor));
        if (FlowLatticeRadius is < MinimumGridRadius or > MaximumGridRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(FlowLatticeRadius));
        }

        ValidatePositiveFinite(FlowLatticeSpacing, nameof(FlowLatticeSpacing));
        ValidatePositiveFinite(FlowRodThickness, nameof(FlowRodThickness));
        ValidatePositiveFinite(FlowRodLengthPerUnitSpeed, nameof(FlowRodLengthPerUnitSpeed));
        ValidateFinite(FlowHeight, nameof(FlowHeight));
        ValidateColor(FlowColor, nameof(FlowColor));
        if (AuthorityWidthFactor < MinimumAuthorityWidthFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(AuthorityWidthFactor));
        }

        ValidateColor(GentleAuthorityColor, nameof(GentleAuthorityColor));
        ValidateColor(SwiftAuthorityColor, nameof(SwiftAuthorityColor));
        ValidateColor(DeclinedColor, nameof(DeclinedColor));
        ValidatePositiveFinite(DebugVectorThickness, nameof(DebugVectorThickness));
        ValidatePositiveFinite(
            DebugVectorLengthPerUnitForce,
            nameof(DebugVectorLengthPerUnitForce));
        ValidatePositiveFinite(DebugMarkerSize, nameof(DebugMarkerSize));
        ValidateFinite(DebugHeight, nameof(DebugHeight));
        ValidateColor(DebugColor, nameof(DebugColor));
        ValidateColor(DebugCenterColor, nameof(DebugCenterColor));
        ValidatePositiveFinite(StruckMarkSize, nameof(StruckMarkSize));
        ValidatePositiveFinite(StruckMarkPerUnitImpulse, nameof(StruckMarkPerUnitImpulse));
        ValidateFinite(StruckMarkHeight, nameof(StruckMarkHeight));
        ValidateColor(StruckMarkColor, nameof(StruckMarkColor));
        return this;
    }

    private static void ValidateColor(Color color, string parameterName)
    {
        ValidateColorComponent(color.R, parameterName);
        ValidateColorComponent(color.G, parameterName);
        ValidateColorComponent(color.B, parameterName);
        ValidateColorComponent(color.A, parameterName);
    }

    private static void ValidateColorComponent(float value, string parameterName)
    {
        if (!float.IsFinite(value)
            || value < MinimumColorComponent
            || value > MaximumColorComponent)
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

    private static void ValidatePositiveFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= MinimumPositiveMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
