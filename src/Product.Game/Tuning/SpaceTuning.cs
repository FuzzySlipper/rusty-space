using System;
using Rusty.Engine;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.Presentation;
using Rusty.Space.Product.Viewing;

namespace Rusty.Space.Product.Tuning;

internal sealed record SpaceTuning(
    FlightTuning Flight,
    FlightBodyTuning FlightBody,
    FieldTuning Field,
    SpacePresentationTuning Presentation,
    CameraTuning Camera)
{
    internal static SpaceTuning Defaults { get; } = new(
        Flight: new(
            MaximumSpeed: 12.0,
            MaximumThrust: 6.0,
            MaximumTurnRate: 2.1,
            ThrottleResponse: TimeSpan.FromSeconds(0.20),
            SteeringResponse: TimeSpan.FromSeconds(0.25)),
        FlightBody: new(
            SpawnPosition: PlanarVector.Zero,
            SpawnHeight: 0.0,
            SpawnHeadingRadians: 0.0,
            HalfExtents: new PlanarVector(0.5, 0.75),
            HalfHeight: 0.25,
            Mass: 2.0),
        Field: new(
            Coupling: 0.0,
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
            ShipHeight: 0.10f,
            ShipColor: new Color(0.23f, 0.79f, 1.0f, 1.0f),
            PlanetDiameter: 1.4f,
            PlanetHeight: 0.0f,
            PlanetColor: new Color(0.96f, 0.72f, 0.25f, 1.0f),
            WakeLength: 7.0f,
            WakeThickness: 0.045f,
            WakeHeight: -0.30f,
            WakeColor: new Color(0.92f, 0.35f, 0.88f, 1.0f),
            StarGridRadius: 8,
            StarSpacing: 12.0f,
            StarHeight: -0.65f,
            StarDiameter: 0.16f,
            StarColor: new Color(0.82f, 0.90f, 1.0f, 1.0f)),
        Camera: new(
            PitchDegrees: -55.0,
            YawDegrees: 90.0,
            HeightAboveShip: 24.0,
            BackDistance: 17.0,
            PositionSmoothing: TimeSpan.FromSeconds(0.35),
            FovYDegrees: 55.0,
            NearPlane: 0.1,
            FarPlane: 500.0,
            MinimumZoomScale: 0.65,
            MaximumZoomScale: 2.5,
            WheelZoomSensitivity: 0.003));

    internal SpaceTuning Validate() => this with
    {
        Flight = Flight.Validate(),
        FlightBody = FlightBody.Validate(),
        Field = Field.Validate(),
        Presentation = Presentation.Validate(),
        Camera = Camera.Validate(),
    };
}
