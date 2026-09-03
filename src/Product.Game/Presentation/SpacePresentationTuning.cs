using System;
using System.Numerics;
using Rusty.Engine;

namespace Rusty.Space.Product.Presentation;

internal sealed record SpacePresentationTuning(
    float ShipHeight,
    Color ShipColor,
    float PlanetDiameter,
    float PlanetHeight,
    Color PlanetColor,
    float WakeLength,
    float WakeThickness,
    float WakeHeight,
    Color WakeColor,
    int StarGridRadius,
    float StarSpacing,
    float StarHeight,
    float StarDiameter,
    Color StarColor)
{
    private const int MinimumStarGridRadius = 1;
    private const float MinimumPositiveMagnitude = 0.0f;
    private const float MinimumColorComponent = 0.0f;
    private const float MaximumColorComponent = 1.0f;

    internal SpacePresentationTuning Validate()
    {
        ValidateFinite(ShipHeight, nameof(ShipHeight));
        ValidateColor(ShipColor, nameof(ShipColor));
        ValidatePositiveFinite(PlanetDiameter, nameof(PlanetDiameter));
        ValidateFinite(PlanetHeight, nameof(PlanetHeight));
        ValidateColor(PlanetColor, nameof(PlanetColor));
        ValidatePositiveFinite(WakeLength, nameof(WakeLength));
        ValidatePositiveFinite(WakeThickness, nameof(WakeThickness));
        ValidateFinite(WakeHeight, nameof(WakeHeight));
        ValidateColor(WakeColor, nameof(WakeColor));
        if (StarGridRadius < MinimumStarGridRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(StarGridRadius));
        }

        ValidatePositiveFinite(StarSpacing, nameof(StarSpacing));
        ValidateFinite(StarHeight, nameof(StarHeight));
        ValidatePositiveFinite(StarDiameter, nameof(StarDiameter));
        ValidateColor(StarColor, nameof(StarColor));
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
