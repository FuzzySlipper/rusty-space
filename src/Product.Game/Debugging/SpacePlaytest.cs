using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Space.Product.Flight;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Debugging;

/// <summary>Live facts and ordinary action bindings for the Engine's shared playtest module.</summary>
internal sealed class SpacePlaytest
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const double FlightActionMilliseconds = 400.0;
    private const double TapMilliseconds = 50.0;
    private readonly SpaceFlight flight;
    private readonly ProductInputConfiguration input;
    private readonly DamageTuning damage;

    internal SpacePlaytest(SpaceFlight flight, ProductInputConfiguration input, DamageTuning damage)
    {
        this.flight = flight;
        this.input = input;
        this.damage = damage;
    }

    internal PlaytestDebugModule Module => new(Observe, Action,
        ["thrust", "left", "right", "couple", "uncouple", "emergency-uncouple", "attitude-hold", "patch", "reset", "helm"],
        (_, _) => DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments,
            "Space has no mouse-look control; turn the hull with left/right or switch chart/helm."));

    internal DebugCommandResult Observe()
    {
        FlightReadout hull = flight.Readout;
        InstalledShip ship = flight.Ship;
        return DebugCommandResult.Success(JsonSerializer.Serialize(new
        {
            fixedStep = flight.FixedStepCount,
            units = "Planar metres: +X spawn-forward, +Z starboard; positive heading turns toward +Z. Position is hull centre.",
            player = new
            {
                position = new { x = hull.Position.X, y = 0.0, z = hull.Position.Z },
                yawDegrees = hull.HeadingRadians * 180.0 / Math.PI,
                velocity = new { x = hull.LinearVelocity.X, z = hull.LinearVelocity.Z },
                speed = hull.LinearVelocity.Magnitude,
                yawRate = hull.AngularVelocity,
            },
            control = flight.LastCommand,
            fit = ship.LoadoutName,
            systems = ship.ReadSystems(),
            telemetry = flight.Telemetry,
            impacts = flight.ImpactCount,
            parts = new[] { ship.Emitter, ship.MainDrive, ship.PortStabilizer, ship.StarboardStabilizer }
                .Select(part => new { id = part.Id.Value, part.Health, part.Temperature, part.OutOfTrim, part.RepairProgress, part.ActuatorValue }),
            chart = flight.Approach.Name,
            obstacles = flight.Approach.Obstacles.Select(obstacle => new
            {
                id = obstacle.Id.Value,
                position = new { x = obstacle.Position.X, z = obstacle.Position.Z },
            }),
        }, Json));
    }

    internal PlaytestAction Action(string id)
    {
        (string? intent, bool hold, double ms) = id switch
        {
            "thrust" => ("space.flight.thrust", true, FlightActionMilliseconds),
            "left" => ("space.flight.turn-left", true, FlightActionMilliseconds),
            "right" => ("space.flight.turn-right", true, FlightActionMilliseconds),
            "couple" => ("space.flight.couple", true, FlightActionMilliseconds),
            "uncouple" => ("space.flight.uncouple", true, FlightActionMilliseconds),
            "emergency-uncouple" => ("space.flight.emergency-uncouple", true, FlightActionMilliseconds),
            "attitude-hold" => ("space.flight.stabilizer", false, TapMilliseconds),
            "patch" => ("space.flight.repair", true, damage.RepairTime.TotalMilliseconds + TapMilliseconds),
            "reset" => ("space.flight.reset", false, TapMilliseconds),
            "helm" => ("space.bridge.sit", false, TapMilliseconds),
            _ => (null, false, 0.0),
        };
        if (intent is null)
            return new(id, "", 0.0, false, false, "unknown-action");

        // Resolve the host-admitted manifest; the diagnostics do not carry a
        // second physical control table or inject named intents themselves.
        foreach (ProductInputMapping mapping in input.PhysicalMappings.Span)
        {
            if (mapping.Intent.Span.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(intent))
                && mapping.Keyboard != KeyboardControl.None
                && mapping.Edge == (hold ? InputEdge.Held : InputEdge.Pressed))
            {
                return new(id, mapping.Keyboard.ToString(), ms, hold);
            }
        }
        return new(id, "", ms, hold, false, "keyboard-binding-unavailable");
    }
}
