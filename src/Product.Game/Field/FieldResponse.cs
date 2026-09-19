using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

internal sealed class FieldResponse
{
    private const double MinimumIntensity = 0.0;
    private const double MaximumIntensity = 1.0;
    private const double BaselineGradientResponse = 1.0;
    private const double NoYawTorque = 0.0;

    private readonly FieldTuning tuning;

    internal FieldResponse(FieldTuning tuning)
    {
        this.tuning = tuning.Validate();
    }

    /// <summary>
    /// The push the local field puts on the hull. Coupling is the ship's choice
    /// rather than a property of the field: at zero the field is declined
    /// entirely and the hull keeps the velocity it arrived with. Mass is the
    /// ship's real mass, so this source scales exactly as the drift bands and
    /// the orbital well do.
    /// </summary>
    internal FlightWrench Resolve(
        FlightBodyState body,
        FieldSample sample,
        double coupling,
        double mass)
    {
        PlanarVector forward = body.Forward;
        PlanarVector right = body.Right;
        PlanarVector relativeVelocity = body.LinearVelocity - sample.FlowVelocity;
        double forwardSlip = relativeVelocity.Dot(forward);
        double rightSlip = relativeVelocity.Dot(right);
        double gradientResponse = BaselineGradientResponse + (tuning.GradientResponseFactor
            * Math.Min(sample.Gradient.AbsoluteMagnitude, tuning.MaximumGradientResponseMagnitude));
        double responseScale = coupling
            * Math.Clamp(sample.Intensity, MinimumIntensity, MaximumIntensity)
            * gradientResponse;
        double turbulenceForward = sample.Turbulence.Dot(forward);
        double turbulenceRight = sample.Turbulence.Dot(right);
        PlanarVector localForce = new(
            ((-forwardSlip * tuning.ForwardResponse)
                + (turbulenceForward * tuning.TurbulenceResponse))
            * responseScale
            * mass,
            ((-rightSlip * tuning.LateralResponse)
                + (turbulenceRight * tuning.TurbulenceResponse))
            * responseScale
            * mass);

        return new FlightWrench(
            forward.Scale(localForce.X) + right.Scale(localForce.Z),
            NoYawTorque);
    }
}
