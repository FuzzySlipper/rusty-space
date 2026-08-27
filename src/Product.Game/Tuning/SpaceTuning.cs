using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Presentation;

namespace Rusty.Space.Product.Tuning;

internal sealed record SpaceTuning(
    FlightTuning Flight,
    FlightBodyTuning FlightBody,
    FieldTuning Field,
    SpacePresentationTuning Presentation)
{
    internal static SpaceTuning Defaults { get; } = new(
        Flight: new(
            MaximumSpeed: 12.0,
            MaximumThrust: 18.0,
            MaximumTurnRate: 3.0,
            ThrottleResponse: TimeSpan.FromSeconds(0.08),
            SteeringResponse: TimeSpan.FromSeconds(0.12)),
        FlightBody: new(
            SpawnPosition: PlanarVector.Zero,
            SpawnHeight: 0.0,
            SpawnHeadingRadians: 0.0,
            HalfExtents: new PlanarVector(0.5, 0.75),
            HalfHeight: 0.25,
            Mass: 2.0),
        Field: new(
            Coupling: 0.55,
            PlanetPosition: new PlanarVector(14.0, 0.0),
            StellarFlow: new PlanarVector(0.0, 1.75),
            StellarIntensity: 0.24,
            WakeCenterBehindPlanet: 5.0,
            WakeLongitudinalScale: 7.0,
            WakeLateralScale: 3.5,
            WakeDownstreamEdgeScale: 1.5,
            WakeFlow: new PlanarVector(1.2, 4.0),
            WakeIntensityContribution: 0.72,
            TurbulenceXAmplitude: 0.22,
            TurbulenceXPositionXFrequency: 0.16,
            TurbulenceXPositionZFrequency: 0.22,
            TurbulenceZAmplitude: 0.30,
            TurbulenceZPositionXFrequency: 0.13,
            TurbulenceZPositionZFrequency: 0.11,
            GradientResponseFactor: 0.12,
            MaximumGradientResponseMagnitude: 4.0,
            ResponseMass: 2.0,
            ForwardResponse: 0.85,
            LateralResponse: 1.8,
            TurbulenceResponse: 0.8),
        Presentation: new(
            ShipScale: new Vector3(1.4f, 0.3f, 0.7f),
            ShipHeight: 0.10f,
            ShipColor: new Color(0.23f, 0.79f, 1.0f, 1.0f),
            HeadingLength: 2.0f,
            HeadingThickness: 0.05f,
            HeadingHeight: 0.55f,
            HeadingColor: new Color(0.85f, 1.0f, 1.0f, 1.0f),
            VelocitySeconds: 0.60f,
            MinimumVelocityLength: 0.15f,
            MaximumVelocityLength: 12.0f,
            VelocityThickness: 0.06f,
            VelocityHeight: 0.25f,
            VelocityColor: new Color(1.0f, 0.55f, 0.10f, 1.0f),
            PlanetDiameter: 1.4f,
            PlanetHeight: 0.0f,
            PlanetColor: new Color(0.96f, 0.72f, 0.25f, 1.0f),
            WakeLength: 7.0f,
            WakeThickness: 0.045f,
            WakeHeight: -0.30f,
            WakeColor: new Color(0.92f, 0.35f, 0.88f, 1.0f)));

    internal SpaceTuning Validate() => this with
    {
        Flight = Flight.Validate(),
        FlightBody = FlightBody.Validate(),
        Field = Field.Validate(),
        Presentation = Presentation.Validate(),
    };
}
