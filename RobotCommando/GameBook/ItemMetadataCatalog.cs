namespace RobotCommando.GameBook;

internal static class ItemMetadataCatalog
{
    private static readonly ImmutableDictionary<string, ItemMetadata> KnownItems =
        ImmutableDictionary.CreateRange(
            StringComparer.OrdinalIgnoreCase,
            [
                new KeyValuePair<string, ItemMetadata>(
                    "Medikit",
                    new ItemMetadata(
                        Tag: "Medikit",
                        Name: "Medikit",
                        Description: "Use: +1 stamina.",
                        Icon: "medikit.png",
                        IconGlyph: "\u271A")),
                new KeyValuePair<string, ItemMetadata>(
                    "Sword",
                    new ItemMetadata(
                        Tag: "Sword",
                        Name: "Sword",
                        Description: "Your trustworthy weapon.",
                        Icon: "sword.png",
                        IconGlyph: "\u2694")),
            ]);

    public static ItemMetadata Resolve(string tag, string name, string? description, string? icon)
    {
        if (KnownItems.TryGetValue(tag, out var known))
        {
            return new ItemMetadata(
                Tag: tag,
                Name: string.IsNullOrWhiteSpace(name) ? known.Name : name,
                Description: string.IsNullOrWhiteSpace(description) ? known.Description : description,
                Icon: string.IsNullOrWhiteSpace(icon) ? known.Icon : icon,
                IconGlyph: ResolveGlyph(icon ?? known.Icon, name, tag));
        }

        return new ItemMetadata(
            Tag: tag,
            Name: name,
            Description: description ?? string.Empty,
            Icon: icon ?? string.Empty,
            IconGlyph: ResolveGlyph(icon, name, tag));
    }

    public static ItemMetadata GetRequired(string tag)
        => KnownItems.TryGetValue(tag, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Unknown item metadata for '{tag}'.");

    private static string ResolveGlyph(string? icon, string name, string tag)
    {
        var key = $"{icon} {name} {tag}";

        if (key.Contains("medikit", StringComparison.OrdinalIgnoreCase)
            || key.Contains("medical", StringComparison.OrdinalIgnoreCase))
        {
            return "\u271A";
        }

        if (key.Contains("sword", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2694";
        }

        if (key.Contains("book", StringComparison.OrdinalIgnoreCase)
            || key.Contains("manual", StringComparison.OrdinalIgnoreCase)
            || key.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("countersign", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F4D6";
        }

        if (key.Contains("transponder", StringComparison.OrdinalIgnoreCase)
            || key.Contains("interface", StringComparison.OrdinalIgnoreCase))
        {
            return "\u25C8";
        }

        if (key.Contains("uniform", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F9E5";
        }

        return "\u25A3";
    }
}

internal readonly record struct ItemMetadata(
    string Tag,
    string Name,
    string Description,
    string Icon,
    string IconGlyph);
