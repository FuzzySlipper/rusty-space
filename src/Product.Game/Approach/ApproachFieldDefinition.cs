namespace Rusty.Space.Product.Approach;

/// <summary>
/// One authored approach space: a name a chart can carry and the geometry that
/// makes the line through it worth flying.
/// </summary>
/// <remarks>
/// <para>
/// An approach space is deliberately small and sparse. What it needs is a few
/// masses a player can read a line between, not a field dense enough to require
/// a corridor to be searched for; the play is in which gap is chosen and how
/// fast the hull is carrying when it commits to one.
/// </para>
/// <para>
/// The definition only says what stands where. Getting it into the world and
/// keeping it there is the Engine's, through <see cref="ApproachField"/>.
/// </para>
/// </remarks>
internal sealed record ApproachFieldDefinition(
    string Name,
    IReadOnlyList<ObstacleDefinition> Obstacles)
{
    internal ApproachFieldDefinition Validate()
    {
        ArgumentNullException.ThrowIfNull(Name);
        ArgumentNullException.ThrowIfNull(Obstacles);

        foreach (ObstacleDefinition obstacle in Obstacles)
        {
            switch (obstacle)
            {
                case Boulder boulder:
                    boulder.Validate();
                    break;
                case WreckBlock block:
                    block.Validate();
                    break;
                default:
                    throw new ArgumentException(
                        $"Approach space {Name} carries an obstacle of an unknown shape.",
                        nameof(Obstacles));
            }
        }

        return this;
    }
}
