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
    OrbitalGravityTuning Orbital,
    DriftCurrentTuning GentleCurrent,
    DriftCurrentTuning SwiftCurrent,
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
        // The well stays centered on the visible planet so the ship bends
        // around the body the player already sees. Near-planet pull rivals
        // full thrust; at spawn range it is a faint crosswind.
        Orbital: new(
            Center: new PlanarVector(14.0, 0.0),
            Strength: 3.0,
            Swirl: 2.5,
            Radius: 7.0,
            MaximumForce: 9.0),
        // Wide, slow, minor push flowing east far below the spawn line,
        // clear of the planet well so each can be tested on its own.
        GentleCurrent: new(
            Center: new PlanarVector(0.0, -22.0),
            Direction: PlanarVector.UnitX,
            Width: 6.0,
            Length: 64.0,
            FlowSpeed: 2.2,
            ResponseGain: 0.9,
            WaveAmplitude: 0.15,
            WaveFrequency: 0.25,
            MaximumForce: 4.0),
        // Narrow, fast, powerful push flowing east far above the spawn line.
        SwiftCurrent: new(
            Center: new PlanarVector(2.0, 22.0),
            Direction: PlanarVector.UnitX,
            Width: 2.2,
            Length: 64.0,
            FlowSpeed: 8.0,
            ResponseGain: 2.2,
            WaveAmplitude: 0.20,
            WaveFrequency: 0.45,
            MaximumForce: 14.0),
        Presentation: new(
            ShipHeight: 0.10f,
            ShipColor: new Color(0.23f, 0.79f, 1.0f, 1.0f),
            PlanetDiameter: 4.0f,
            PlanetHeight: 0.0f,
            PlanetColor: new Color(0.96f, 0.72f, 0.25f, 1.0f),
            WakeLength: 7.0f,
            WakeThickness: 0.045f,
            WakeHeight: -0.30f,
            WakeColor: new Color(0.92f, 0.35f, 0.88f, 1.0f),
            GentleCurrentDepth: 0.35f,
            GentleCurrentHeight: -0.32f,
            GentleCurrentColor: new Color(0.35f, 0.95f, 0.60f, 1.0f),
            SwiftCurrentDepth: 0.12f,
            SwiftCurrentHeight: -0.28f,
            SwiftCurrentColor: new Color(1.0f, 0.38f, 0.18f, 1.0f),
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
        Orbital = Orbital.Validate(),
        GentleCurrent = GentleCurrent.Validate(),
        SwiftCurrent = SwiftCurrent.Validate(),
        Presentation = Presentation.Validate(),
        Camera = Camera.Validate(),
    };
}
