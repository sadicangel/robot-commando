namespace RobotCommando.GameBook;

internal static class ItemMetadataCatalog
{
    private static readonly ImmutableDictionary<string, ItemMetadata> KnownItems =
        ImmutableDictionary.CreateRange(
            StringComparer.OrdinalIgnoreCase,
            [
                new KeyValuePair<string, ItemMetadata>(
                    "Armor Plate",
                    new ItemMetadata(
                        Tag: "Armor Plate",
                        Name: "Armor Plate",
                        Description: "A modular armour plate that can restore 1 ARMOUR.",
                        Icon: "armor_plate.svg",
                        IconGlyph: "\u25A7")),
                new KeyValuePair<string, ItemMetadata>(
                    "Blue Potion",
                    new ItemMetadata(
                        Tag: "Blue Potion",
                        Name: "Blue Potion",
                        Description: "A volatile sleeping-sickness cure that must be dispersed all at once.",
                        Icon: "blue_potion.svg",
                        IconGlyph: "\u25D2")),
                new KeyValuePair<string, ItemMetadata>(
                    "City of Guardians Location",
                    new ItemMetadata(
                        Tag: "City of Guardians Location",
                        Name: "City of Guardians Location",
                        Description: "Map reference: 22.",
                        Icon: "city_of_guardians_location.svg",
                        IconGlyph: "\u25CE")),
                new KeyValuePair<string, ItemMetadata>(
                    "Cloak Model Reference",
                    new ItemMetadata(
                        Tag: "Cloak Model Reference",
                        Name: "Cloak Model Reference",
                        Description: "Model reference: 3. In the book, use reference 53.",
                        Icon: "cloak_model_reference.svg",
                        IconGlyph: "\u25A4")),
                new KeyValuePair<string, ItemMetadata>(
                    "Cloak of Invisibility",
                    new ItemMetadata(
                        Tag: "Cloak of Invisibility",
                        Name: "Cloak of Invisibility",
                        Description: "Bends light around you for a short time. It only works once.",
                        Icon: "cloak_of_invisibility.svg",
                        IconGlyph: "\u25CC")),
                new KeyValuePair<string, ItemMetadata>(
                    "Interface Transponder",
                    new ItemMetadata(
                        Tag: "Interface Transponder",
                        Name: "Interface Transponder",
                        Description: "+1 skill with robots.",
                        Icon: "interface_transponder.svg",
                        IconGlyph: "\u25C8")),
                new KeyValuePair<string, ItemMetadata>(
                    "Karossean Book Reference",
                    new ItemMetadata(
                        Tag: "Karossean Book Reference",
                        Name: "Karossean Book Reference",
                        Description: "Reference number: 111.",
                        Icon: "karossean_book_reference.svg",
                        IconGlyph: "\U0001F4D6")),
                new KeyValuePair<string, ItemMetadata>(
                    "Karossean Countersign",
                    new ItemMetadata(
                        Tag: "Karossean Countersign",
                        Name: "Karossean Countersign",
                        Description: "Countersign: 7.",
                        Icon: "karossean_countersign.svg",
                        IconGlyph: "\u25D1")),
                new KeyValuePair<string, ItemMetadata>(
                    "Karossean Password",
                    new ItemMetadata(
                        Tag: "Karossean Password",
                        Name: "Karossean Password",
                        Description: "Password: 88.",
                        Icon: "karossean_password.svg",
                        IconGlyph: "\u25D0")),
                new KeyValuePair<string, ItemMetadata>(
                    "Karossean Uniform",
                    new ItemMetadata(
                        Tag: "Karossean Uniform",
                        Name: "Karossean Uniform",
                        Description: "Enemy clothing useful for disguises.",
                        Icon: "karossean_uniform.svg",
                        IconGlyph: "\U0001F9E5")),
                new KeyValuePair<string, ItemMetadata>(
                    "Lavender Potion",
                    new ItemMetadata(
                        Tag: "Lavender Potion",
                        Name: "Lavender Potion",
                        Description: "A strengthened potion made with a fresh Man-trap Flower.",
                        Icon: "lavender_potion.svg",
                        IconGlyph: "\u25D2")),
                new KeyValuePair<string, ItemMetadata>(
                    "Luck Amulet",
                    new ItemMetadata(
                        Tag: "Luck Amulet",
                        Name: "Luck Amulet",
                        Description: "A scientifically proven good-luck charm.",
                        Icon: "luck_amulet.svg",
                        IconGlyph: "\u2726")),
                new KeyValuePair<string, ItemMetadata>(
                    "Medikit",
                    new ItemMetadata(
                        Tag: "Medikit",
                        Name: "Medikit",
                        Description: "Use: +1 stamina.",
                        Icon: "medikit.svg",
                        IconGlyph: "\u271A")),
                new KeyValuePair<string, ItemMetadata>(
                    "Seeker Missile",
                    new ItemMetadata(
                        Tag: "Seeker Missile",
                        Name: "Seeker Missile",
                        Description: "One-shot robot weapon: automatically hits and deals 10 ARMOUR damage.",
                        Icon: "seeker_missile.svg",
                        IconGlyph: "\u25B2")),
                new KeyValuePair<string, ItemMetadata>(
                    "Sword of State",
                    new ItemMetadata(
                        Tag: "Sword of State",
                        Name: "Sword of State",
                        Description: "A balanced ceremonial blade. +1 SKILL when fighting with it.",
                        Icon: "state_sword.svg",
                        IconGlyph: "\u2694")),
                new KeyValuePair<string, ItemMetadata>(
                    "Sword",
                    new ItemMetadata(
                        Tag: "Sword",
                        Name: "Sword",
                        Description: "Your trustworthy weapon.",
                        Icon: "sword.svg",
                        IconGlyph: "\u2694")),
                new KeyValuePair<string, ItemMetadata>(
                    "Tangler Field",
                    new ItemMetadata(
                        Tag: "Tangler Field",
                        Name: "Tangler Field",
                        Description: "Experimental field projector for disrupting flying machines.",
                        Icon: "tangler_field.svg",
                        IconGlyph: "\u273A")),
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
                IconGlyph: known.IconGlyph);
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
