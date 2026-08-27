using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

internal sealed record FieldTuning(
    double Coupling,
    PlanarVector PlanetPosition,
    PlanarVector StellarFlow,
    double StellarIntensity,
    double WakeCenterBehindPlanet,
    double WakeLongitudinalScale,
    double WakeLateralScale,
    double WakeDownstreamEdgeScale,
    PlanarVector WakeFlow,
    double WakeIntensityContribution,
    double TurbulenceXAmplitude,
    double TurbulenceXPositionXFrequency,
    double TurbulenceXPositionZFrequency,
    double TurbulenceZAmplitude,
    double TurbulenceZPositionXFrequency,
    double TurbulenceZPositionZFrequency,
    double GradientResponseFactor,
    double MaximumGradientResponseMagnitude,
    double ResponseMass,
    double ForwardResponse,
    double LateralResponse,
    double TurbulenceResponse)
{
    private const double MinimumCoupling = 0.0;
    private const double MaximumCoupling = 1.0;
    private const double MinimumNonNegativeMagnitude = 0.0;
    private const double MinimumPositiveMagnitude = 0.0;

    internal FieldTuning Validate()
    {
        if (!double.IsFinite(Coupling)
            || Coupling < MinimumCoupling
            || Coupling > MaximumCoupling)
        {
            throw new ArgumentOutOfRangeException(nameof(Coupling));
        }

        ValidateFinite(PlanetPosition, nameof(PlanetPosition));
        ValidateFinite(StellarFlow, nameof(StellarFlow));
        ValidateFinite(WakeFlow, nameof(WakeFlow));
        ValidateNonNegativeFinite(StellarIntensity, nameof(StellarIntensity));
        ValidatePositiveFinite(WakeCenterBehindPlanet, nameof(WakeCenterBehindPlanet));
        ValidatePositiveFinite(WakeLongitudinalScale, nameof(WakeLongitudinalScale));
        ValidatePositiveFinite(WakeLateralScale, nameof(WakeLateralScale));
        ValidatePositiveFinite(WakeDownstreamEdgeScale, nameof(WakeDownstreamEdgeScale));
        ValidateNonNegativeFinite(WakeIntensityContribution, nameof(WakeIntensityContribution));
        ValidateNonNegativeFinite(TurbulenceXAmplitude, nameof(TurbulenceXAmplitude));
        ValidateNonNegativeFinite(TurbulenceZAmplitude, nameof(TurbulenceZAmplitude));
        ValidateFinite(TurbulenceXPositionXFrequency, nameof(TurbulenceXPositionXFrequency));
        ValidateFinite(TurbulenceXPositionZFrequency, nameof(TurbulenceXPositionZFrequency));
        ValidateFinite(TurbulenceZPositionXFrequency, nameof(TurbulenceZPositionXFrequency));
        ValidateFinite(TurbulenceZPositionZFrequency, nameof(TurbulenceZPositionZFrequency));
        ValidateNonNegativeFinite(GradientResponseFactor, nameof(GradientResponseFactor));
        ValidatePositiveFinite(MaximumGradientResponseMagnitude, nameof(MaximumGradientResponseMagnitude));
        ValidatePositiveFinite(ResponseMass, nameof(ResponseMass));
        ValidateNonNegativeFinite(ForwardResponse, nameof(ForwardResponse));
        ValidateNonNegativeFinite(LateralResponse, nameof(LateralResponse));
        ValidateNonNegativeFinite(TurbulenceResponse, nameof(TurbulenceResponse));
        return this;
    }

    private static void ValidateFinite(PlanarVector value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < MinimumNonNegativeMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= MinimumPositiveMagnitude)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
