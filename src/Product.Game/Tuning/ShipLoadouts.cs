using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Tuning;

/// <summary>
/// The authored fits a hull can leave with. Each is the same frame with
/// different hardware on it, and each is meant to be recognizable from the
/// controls rather than from a readout.
/// </summary>
/// <remarks>
/// <para>
/// Mounts are local offsets from the hull's center of mass: <c>+X</c> toward the
/// bow, <c>+Z</c> toward starboard, against a hull half <c>0.5</c> wide along the
/// keel and <c>0.75</c> beam. Masses are the parts' own; the stock fit's total
/// mass and turn inertia are what the bare hull carried before parts existed, so
/// a stock ship still handles the way it did.
/// </para>
/// <para>
/// Actuator values are a response frequency in radians per second and a damping
/// ratio, so <c>12.0, 0.95</c> is willing hardware that arrives where it is told,
/// and <c>5.5, 0.18</c> is tired hardware that arrives late, arrives past the
/// demand, and comes back. Nothing here is random: every difference a player can
/// feel is authored here and reproducible.
/// </para>
/// </remarks>
internal static class ShipLoadouts
{
    /// <summary>
    /// Everything rated for the hull and in working order. Moderate coupling, a
    /// steering pair that answers evenly on both sides, and a response a player
    /// can predict after a few turns.
    /// </summary>
    internal static ShipLoadout Stock { get; } = new(
        Name: "stock",
        Emitter: new(
            Id: new PartId("emitter-stock"),
            Mount: new PlanarVector(0.15, 0.0),
            Mass: 0.10,
            Health: 1.0,
            CouplingGain: 1.0,
            Response: new ActuatorTuning(Frequency: 9.0, DampingRatio: 0.90)),
        Drive: new(
            Id: new PartId("drive-stock"),
            Mount: new PlanarVector(-0.45, 0.0),
            Mass: 0.16,
            Health: 1.0,
            Response: new ActuatorTuning(Frequency: 14.0, DampingRatio: 1.00)),
        PortStabilizer: new(
            Id: new PartId("stabilizer-port"),
            Mount: new PlanarVector(-0.20, -0.45),
            Mass: 0.06,
            Health: 1.0,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 12.0, DampingRatio: 0.95),
            LoadThreshold: 0.95,
            WearDampingRatio: 0.90,
            PullPerUnitLoad: 0.0),
        StarboardStabilizer: new(
            Id: new PartId("stabilizer-starboard"),
            Mount: new PlanarVector(-0.20, 0.45),
            Mass: 0.06,
            Health: 1.0,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 12.0, DampingRatio: 0.95),
            LoadThreshold: 0.95,
            WearDampingRatio: 0.90,
            PullPerUnitLoad: 0.0));

    /// <summary>
    /// A coupling coil off something bigger, wired in because it was there. It
    /// catches far more of the same flow, and it is hung forward of the center of
    /// mass, so the flow that drives the ship also swings the bow into itself.
    /// Faster, and the steering has to spend most of its authority disagreeing.
    /// </summary>
    internal static ShipLoadout OversizedScavengedEmitter { get; } = new(
        Name: "oversized-scavenged-emitter",
        Emitter: new(
            Id: new PartId("emitter-scavenged-oversized"),
            Mount: new PlanarVector(0.85, 0.0),
            Mass: 0.34,
            Health: 0.70,
            CouplingGain: 1.70,
            Response: new ActuatorTuning(Frequency: 6.0, DampingRatio: 0.45)),
        Drive: new(
            Id: new PartId("drive-stock"),
            Mount: new PlanarVector(-0.45, 0.0),
            Mass: 0.16,
            Health: 1.0,
            Response: new ActuatorTuning(Frequency: 14.0, DampingRatio: 1.00)),
        PortStabilizer: new(
            Id: new PartId("stabilizer-port"),
            Mount: new PlanarVector(-0.20, -0.45),
            Mass: 0.06,
            Health: 1.0,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 12.0, DampingRatio: 0.95),
            LoadThreshold: 0.95,
            WearDampingRatio: 0.90,
            PullPerUnitLoad: 0.0),
        StarboardStabilizer: new(
            Id: new PartId("stabilizer-starboard"),
            Mount: new PlanarVector(-0.20, 0.45),
            Mass: 0.06,
            Health: 1.0,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 12.0, DampingRatio: 0.95),
            LoadThreshold: 0.95,
            WearDampingRatio: 0.90,
            PullPerUnitLoad: 0.0));

    /// <summary>
    /// A ship whose starboard stabilizer is worn out but not yet replaced. It
    /// still has its rated peak authority, it just takes longer to get there and
    /// rings once the flow is hard enough to ring it, and under load it pulls the
    /// bow to one side. Turning to port and turning to starboard stop being the
    /// same maneuver.
    /// </summary>
    internal static ShipLoadout DamagedStabilizer { get; } = new(
        Name: "damaged-stabilizer",
        Emitter: new(
            Id: new PartId("emitter-stock"),
            Mount: new PlanarVector(0.15, 0.0),
            Mass: 0.10,
            Health: 1.0,
            CouplingGain: 1.0,
            Response: new ActuatorTuning(Frequency: 9.0, DampingRatio: 0.90)),
        Drive: new(
            Id: new PartId("drive-stock"),
            Mount: new PlanarVector(-0.45, 0.0),
            Mass: 0.16,
            Health: 1.0,
            Response: new ActuatorTuning(Frequency: 14.0, DampingRatio: 1.00)),
        PortStabilizer: new(
            Id: new PartId("stabilizer-port"),
            Mount: new PlanarVector(-0.20, -0.45),
            Mass: 0.06,
            Health: 1.0,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 12.0, DampingRatio: 0.95),
            LoadThreshold: 0.95,
            WearDampingRatio: 0.90,
            PullPerUnitLoad: 0.0),
        StarboardStabilizer: new(
            Id: new PartId("stabilizer-starboard-worn"),
            Mount: new PlanarVector(-0.20, 0.45),
            Mass: 0.06,
            Health: 0.90,
            ForceAuthority: 5.0,
            Response: new ActuatorTuning(Frequency: 5.5, DampingRatio: 0.90),
            LoadThreshold: 0.40,
            WearDampingRatio: 0.18,
            PullPerUnitLoad: 0.60));
}
