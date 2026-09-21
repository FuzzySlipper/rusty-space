using System;
using Rusty.Space.Product.Field;
using Rusty.Space.Product.Navigation;
using Rusty.Space.Product.ShipSystems;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// How a hull at one state turns the environment and its own fitted hardware
/// into the push the Engine is asked to integrate.
/// </summary>
/// <remarks>
/// <para>
/// Each source names itself, lands at the center the fitted hardware puts it at,
/// and is joined only where the single <see cref="DynamicsAction"/> is built, so
/// the product can always say which source moved the ship. Flow-coupled push —
/// the stellar field and every drift band — arrives at the emitter's mount; main
/// thrust at the drive's mount; the orbital well at the center of mass itself,
/// because a mass relation has no lever about the point its mass sits at.
/// </para>
/// <para>
/// This is resolved per fixed substep for the hull and per sample for the
/// projected line the navigation view draws. Both readings go through this one
/// rule deliberately: a projected path that resolved its own copy of the forces
/// would be free to drift away from the ship it claims to predict, and a line on
/// the screen that disagrees with the hull by the time it is drawn is worse than
/// no line at all.
/// </para>
/// </remarks>
internal sealed class HullForceModel
{
    private readonly FieldResponse fieldResponse;
    private readonly DriftCurrent gentleCurrent;
    private readonly DriftCurrent swiftCurrent;
    private readonly OrbitalGravity gravity;

    internal HullForceModel(
        FieldResponse fieldResponse,
        DriftCurrent gentleCurrent,
        DriftCurrent swiftCurrent,
        OrbitalGravity gravity)
    {
        this.fieldResponse = fieldResponse
            ?? throw new ArgumentNullException(nameof(fieldResponse));
        this.gentleCurrent = gentleCurrent
            ?? throw new ArgumentNullException(nameof(gentleCurrent));
        this.swiftCurrent = swiftCurrent
            ?? throw new ArgumentNullException(nameof(swiftCurrent));
        this.gravity = gravity ?? throw new ArgumentNullException(nameof(gravity));
    }

    /// <summary>
    /// Every push acting on a hull at this state, split by source. The effort is
    /// what the fitted actuators reached, not what was asked for.
    /// </summary>
    internal FlightForces Resolve(
        FlightBodyState body,
        InstalledShip ship,
        FieldSample fieldSample,
        ShipEffort effort,
        double mass)
    {
        ArgumentNullException.ThrowIfNull(ship);

        PlanarVector couplingCenter = ship.FieldCouplingCenter(body.HeadingRadians);
        PlanarVector thrustCenter = ship.MainThrustCenter(body.HeadingRadians);
        double couplingLevel = effort.Coupling;

        return new FlightForces(
            MainDrive: AtPoint(effort.DriveForce, thrustCenter),
            // The effector pair's yaw, worn pull and jammed vane included: those
            // are inside what the pair's actuators reached, so naming them again
            // here would bill the hull twice for work already counted.
            Steering: new FlightWrench(PlanarVector.Zero, effort.HeadingTorque),
            Field: AtPoint(
                fieldResponse.Resolve(body, fieldSample, couplingLevel, mass).Force,
                couplingCenter),
            GentleCurrent: AtPoint(
                gentleCurrent.Resolve(
                    body.Position,
                    body.LinearVelocity,
                    mass,
                    couplingLevel).Force,
                couplingCenter),
            SwiftCurrent: AtPoint(
                swiftCurrent.Resolve(
                    body.Position,
                    body.LinearVelocity,
                    mass,
                    couplingLevel).Force,
                couplingCenter),
            OrbitalPull: gravity.Resolve(body.Position, mass));
    }

    /// <summary>
    /// The same push, with the turn it causes because it lands away from the
    /// center of mass.
    /// </summary>
    private static FlightWrench AtPoint(PlanarVector force, PlanarVector offsetFromCenter) =>
        new(force, PlanarFrame.YawTorque(offsetFromCenter, force));
}
