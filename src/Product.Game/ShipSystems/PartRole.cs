namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// What an installed part is for, named for the behavior it serves rather than
/// for the component it is built from. A part's role decides which of the
/// ship's centers it occupies and which demand its actuator answers.
/// </summary>
internal enum PartRole
{
    /// <summary>
    /// Turns the local field and every drift band into push on the hull. Its
    /// mount is the ship's center of field coupling.
    /// </summary>
    FieldEmitter,

    /// <summary>
    /// Pushes along the keel. Its mount is the ship's center of main thrust.
    /// </summary>
    MainDrive,

    /// <summary>
    /// Yaws the ship by pushing on opposite sides of the keel. Mounted in a
    /// mirrored pair: one toward port, one toward starboard.
    /// </summary>
    Stabilizer,
}
