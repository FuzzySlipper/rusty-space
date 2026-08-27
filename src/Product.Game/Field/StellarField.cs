using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

internal sealed class StellarField
{
    private const double MinimumWakeBehindPlanet = 0.0;
    private const double MinimumFieldIntensity = 0.0;
    private const double MaximumFieldIntensity = 1.0;
    private const double NoWake = 0.0;
    private const double UnmodifiedWakeScale = 1.0;
    private const double GaussianSquaredExponent = 2.0;
    private const double LongitudinalGradientMultiplier = 2.0;
    private const double DownstreamGateGradientMultiplier = -2.0;
    private const double LateralGradientMultiplier = -2.0;

    private readonly FieldTuning tuning;

    internal StellarField(FieldTuning tuning)
    {
        this.tuning = tuning.Validate();
    }

    internal FieldSample Sample(PlanarVector position)
    {
        double wake = WakeWeight(position);
        double wakeByPositionX = WakeWeightByPositionX(position);
        double wakeByPositionZ = WakeWeightByPositionZ(position, wake);

        return new FieldSample(
            tuning.StellarFlow + tuning.WakeFlow.Scale(wake),
            Math.Clamp(
                tuning.StellarIntensity + (tuning.WakeIntensityContribution * wake),
                MinimumFieldIntensity,
                MaximumFieldIntensity),
            new FieldFlowGradient(
                tuning.WakeFlow.X * wakeByPositionX,
                tuning.WakeFlow.X * wakeByPositionZ,
                tuning.WakeFlow.Z * wakeByPositionX,
                tuning.WakeFlow.Z * wakeByPositionZ),
            new PlanarVector(
                tuning.TurbulenceXAmplitude
                * wake
                * Math.Sin(
                    (position.X * tuning.TurbulenceXPositionXFrequency)
                    + (position.Z * tuning.TurbulenceXPositionZFrequency)),
                tuning.TurbulenceZAmplitude
                * wake
                * Math.Cos(
                    (position.X * tuning.TurbulenceZPositionXFrequency)
                    - (position.Z * tuning.TurbulenceZPositionZFrequency))));
    }

    private double WakeWeight(PlanarVector position)
    {
        double behindPlanet = tuning.PlanetPosition.X - position.X;
        if (behindPlanet <= MinimumWakeBehindPlanet)
        {
            return NoWake;
        }

        double longitudinal =
            (behindPlanet - tuning.WakeCenterBehindPlanet) / tuning.WakeLongitudinalScale;
        double lateral = position.Z / tuning.WakeLateralScale;
        double downstreamEdge = Math.Exp(-Math.Pow(
            behindPlanet / tuning.WakeDownstreamEdgeScale,
            GaussianSquaredExponent));
        return Math.Exp((-longitudinal * longitudinal) - (lateral * lateral))
            * (UnmodifiedWakeScale - downstreamEdge);
    }

    private double WakeWeightByPositionX(PlanarVector position)
    {
        double behindPlanet = tuning.PlanetPosition.X - position.X;
        if (behindPlanet <= MinimumWakeBehindPlanet)
        {
            return NoWake;
        }

        double longitudinal =
            (behindPlanet - tuning.WakeCenterBehindPlanet) / tuning.WakeLongitudinalScale;
        double lateral = position.Z / tuning.WakeLateralScale;
        double longitudinalEnvelope = Math.Exp(-longitudinal * longitudinal);
        double lateralEnvelope = Math.Exp(-lateral * lateral);
        double edgeEnvelope = Math.Exp(-Math.Pow(
            behindPlanet / tuning.WakeDownstreamEdgeScale,
            GaussianSquaredExponent));
        double downstreamGate = UnmodifiedWakeScale - edgeEnvelope;
        double longitudinalDerivative =
            LongitudinalGradientMultiplier
            * (behindPlanet - tuning.WakeCenterBehindPlanet)
            / Math.Pow(tuning.WakeLongitudinalScale, GaussianSquaredExponent)
            * longitudinalEnvelope;
        double downstreamGateDerivative =
            DownstreamGateGradientMultiplier
            * behindPlanet
            / Math.Pow(tuning.WakeDownstreamEdgeScale, GaussianSquaredExponent)
            * edgeEnvelope;
        return ((longitudinalDerivative * downstreamGate)
            + (longitudinalEnvelope * downstreamGateDerivative))
            * lateralEnvelope;
    }

    private double WakeWeightByPositionZ(PlanarVector position, double wake)
    {
        if (wake == NoWake)
        {
            return NoWake;
        }

        return LateralGradientMultiplier
            * position.Z
            / Math.Pow(tuning.WakeLateralScale, GaussianSquaredExponent)
            * wake;
    }
}
