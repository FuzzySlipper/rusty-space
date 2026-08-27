using System.Numerics;
using Rusty.Engine;

namespace Rusty.Space.Product.Presentation;

internal sealed record SpacePresentationTuning(
    Vector3 ShipScale,
    float ShipHeight,
    Color ShipColor,
    float HeadingLength,
    float HeadingThickness,
    float HeadingHeight,
    Color HeadingColor,
    float VelocitySeconds,
    float MinimumVelocityLength,
    float MaximumVelocityLength,
    float VelocityThickness,
    float VelocityHeight,
    Color VelocityColor,
    float PlanetDiameter,
    float PlanetHeight,
    Color PlanetColor,
    float WakeLength,
    float WakeThickness,
    float WakeHeight,
    Color WakeColor)
{
    private const float MinimumPositiveMagnitude = 0.0f;
    private const float MinimumColorComponent = 0.0f;
    private const float MaximumColorComponent = 1.0f;

    internal SpacePresentationTuning Validate()
    {
        ValidatePositiveFinite(ShipScale.X, nameof(ShipScale));
        ValidatePositiveFinite(ShipScale.Y, nameof(ShipScale));
        ValidatePositiveFinite(ShipScale.Z, nameof(ShipScale));
        ValidateFinite(ShipHeight, nameof(ShipHeight));
        ValidateColor(ShipColor, nameof(ShipColor));
        ValidatePositiveFinite(HeadingLength, nameof(HeadingLength));
        ValidatePositiveFinite(HeadingThickness, nameof(HeadingThickness));
        ValidateFinite(HeadingHeight, nameof(HeadingHeight));
        ValidateColor(HeadingColor, nameof(HeadingColor));
        ValidatePositiveFinite(VelocitySeconds, nameof(VelocitySeconds));
        ValidatePositiveFinite(MinimumVelocityLength, nameof(MinimumVelocityLength));
        ValidatePositiveFinite(MaximumVelocityLength, nameof(MaximumVelocityLength));
        if (MinimumVelocityLength > MaximumVelocityLength)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumVelocityLength));
        }

        ValidatePositiveFinite(VelocityThickness, nameof(VelocityThickness));
        ValidateFinite(VelocityHeight, nameof(VelocityHeight));
        ValidateColor(VelocityColor, nameof(VelocityColor));
        ValidatePositiveFinite(PlanetDiameter, nameof(PlanetDiameter));
        ValidateFinite(PlanetHeight, nameof(PlanetHeight));
        ValidateColor(PlanetColor, nameof(PlanetColor));
        ValidatePositiveFinite(WakeLength, nameof(WakeLength));
        ValidatePositiveFinite(WakeThickness, nameof(WakeThickness));
        ValidateFinite(WakeHeight, nameof(WakeHeight));
        ValidateColor(WakeColor, nameof(WakeColor));
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
