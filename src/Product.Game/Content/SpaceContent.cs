using Rusty.Engine;

namespace Rusty.Space.Product.Content;

internal readonly record struct SpaceContent(int FileCount)
{
    internal static SpaceContent From(ProductContent content) => new(content.Files.Length);
}
