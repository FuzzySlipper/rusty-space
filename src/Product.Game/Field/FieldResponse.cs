using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

internal sealed class FieldResponse
{
    private const double MinimumIntensity = 0.0;
    private const double MaximumIntensity = 1.0;
    private const double BaselineGradientResponse = 1.0;
    private const double NoFieldCoupling = 0.0;
    private const double NoYawTorque = 0.0;

    private readonly FieldTuning tuning;

    internal FieldResponse(FieldTuning tuning)
    {
        this.tuning = tuning.Validate();
    }

    internal FlightWrench Resolve(FlightBodyState body, FieldSample sample)
    {
        if (tuning.Coupling == NoFieldCoupling)
        {
            return FlightWrench.Zero;
        }

        PlanarVector forward = new(Math.Cos(body.HeadingRadians), Math.Sin(body.HeadingRadians));
        PlanarVector right = new(-forward.Z, forward.X);
        PlanarVector relativeVelocity = body.LinearVelocity - sample.FlowVelocity;
        double forwardSlip = relativeVelocity.Dot(forward);
        double lateralSlip = relativeVelocity.Dot(right);
        double gradientResponse = BaselineGradientResponse + (tuning.GradientResponseFactor
            * Math.Min(sample.Gradient.AbsoluteMagnitude, tuning.MaximumGradientResponseMagnitude));
        double responseScale = tuning.Coupling
            * Math.Clamp(sample.Intensity, MinimumIntensity, MaximumIntensity)
            * gradientResponse;
        double turbulenceForward = sample.Turbulence.Dot(forward);
        double turbulenceLateral = sample.Turbulence.Dot(right);
        PlanarVector localForce = new(
            ((-forwardSlip * tuning.ForwardResponse)
                + (turbulenceForward * tuning.TurbulenceResponse))
            * responseScale
            * tuning.ResponseMass,
            ((-lateralSlip * tuning.LateralResponse)
                + (turbulenceLateral * tuning.TurbulenceResponse))
            * responseScale
            * tuning.ResponseMass);

        return new FlightWrench(
            forward.Scale(localForce.X) + right.Scale(localForce.Z),
            NoYawTorque);
    }
}
