namespace Rusty.Space.Product.Approach;

/// <summary>
/// Identity for one authored piece of approach geometry. A contact names the
/// rock a hull struck by this id rather than by an index into a list, so a
/// report survives the field being rearranged.
/// </summary>
internal readonly record struct ObstacleId(string Value)
{
    public override string ToString() => Value;
}
