using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// The part that decides how much of the local field and of every drift band the
/// hull actually feels, and how quickly it gets there.
/// </summary>
/// <remarks>
/// <para>
/// The coupling actuator is the player's demand; this is the hardware that has to
/// answer it. <see cref="CouplingGain"/> scales the demand: an emitter rated
/// above <c>1.0</c> catches more of the same flow than the hull was trimmed for,
/// which buys speed and hands the controller a fight it did not ask for.
/// </para>
/// <para>
/// <see cref="PartDefinition.Mount"/> is the ship's center of field coupling.
/// Fitted forward of the center of mass, the same flow that pushes the hull also
/// swings the bow into it, and the ship weathervanes: the farther the emitter
/// sits from the center, the more strongly.
/// </para>
/// <para>
/// <see cref="Response"/> is the emitter's own actuator. A coupling coil that
/// reaches its setting slowly, or overshoots it, means the push the player asked
/// for is not the push the hull gets this instant — which is what makes an
/// oversized salvaged coil feel like it is arguing with the controller.
/// </para>
/// </remarks>
internal sealed record FieldEmitterDefinition(
    PartId Id,
    PlanarVector Mount,
    double Mass,
    double Health,
    double CouplingGain,
    ActuatorTuning Response)
    : PartDefinition(Id, PartRole.FieldEmitter, Mount, Mass, Health)
{
    private const double MinimumGain = 0.0;

    internal FieldEmitterDefinition Validate()
    {
        ValidateIdentity();

        if (!double.IsFinite(CouplingGain) || CouplingGain < MinimumGain)
        {
            throw new ArgumentOutOfRangeException(nameof(CouplingGain));
        }

        Response.Validate();
        return this;
    }
}
