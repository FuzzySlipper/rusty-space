using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// The part that turns throttle into push along the keel.
/// </summary>
/// <remarks>
/// <para>
/// The throttle the player works is a command; this is the hardware behind it.
/// <see cref="Response"/> shapes how fast the drive reaches the
/// thrust the throttle asks for, so a willing drive and a tired one can be
/// handed the same throttle position and produce different push for a step or
/// two while the spool catches up.
/// </para>
/// <para>
/// <see cref="PartDefinition.Mount"/> is the ship's center of main thrust. A
/// drive on the centerline pushes the hull straight along it; a drive hung off to
/// one side yaws the ship every time the throttle comes up, and the ship's
/// steering has to hold that off as a matter of course rather than notice it.
/// </para>
/// </remarks>
internal sealed record MainDriveDefinition(
    PartId Id,
    PlanarVector Mount,
    double Mass,
    double Health,
    ActuatorTuning Response)
    : PartDefinition(Id, PartRole.MainDrive, Mount, Mass, Health)
{
    internal MainDriveDefinition Validate()
    {
        ValidateIdentity();
        Response.Validate();
        return this;
    }
}
