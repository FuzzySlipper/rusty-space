namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What is bolted to one hull: the emitter, the drive, and the two sides of the
/// heading effector pair, each with where it sits and what it is rated for.
/// </summary>
/// <remarks>
/// A loadout is authored, not assembled at runtime. It is what lets the same
/// hull fly three different ways — a healthy stock fit, an oversized salvaged
/// emitter, a ship with a tired stabilizer — without any flight code knowing the
/// difference. Installation, removal, and repair belong to a later phase; a
/// loadout is what the ship leaves with.
/// </remarks>
internal sealed record ShipLoadout(
    string Name,
    FieldEmitterDefinition Emitter,
    MainDriveDefinition Drive,
    StabilizerDefinition PortStabilizer,
    StabilizerDefinition StarboardStabilizer)
{
    private const double MinimumLeverArm = 0.0;

    internal ShipLoadout Validate()
    {
        ArgumentNullException.ThrowIfNull(Name);
        Emitter.Validate();
        Drive.Validate();
        PortStabilizer.Validate();
        StarboardStabilizer.Validate();

        // A heading effector only yaws the ship by pushing to one side of the
        // keel. One mounted on the centerline would contribute nothing no matter
        // how hard it worked, so a fit like that is a mistake rather than a
        // design, and it is refused where it is authored.
        if (PortStabilizer.Mount.Z >= MinimumLeverArm || StarboardStabilizer.Mount.Z <= MinimumLeverArm)
        {
            throw new ArgumentOutOfRangeException(nameof(PortStabilizer));
        }

        return this;
    }
}
