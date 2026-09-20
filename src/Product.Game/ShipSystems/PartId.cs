namespace Rusty.Space.Product.ShipSystems;

/// <summary>
/// Identity for one installed part. A loadout names its parts so the product can
/// say which emitter or which stabilizer a reading belongs to, rather than
/// pointing at an index into a list.
/// </summary>
internal readonly record struct PartId(string Value)
{
    public override string ToString() => Value;
}
