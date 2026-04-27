using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobotCommando.GameBook;

internal sealed partial class BookEncounterNormalizer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingNoiseRegex = new(@"^[\.\s]+", RegexOptions.Compiled);
    private static readonly Regex LeadingBlockNumberRegex = new(@"^\d+\s+", RegexOptions.Compiled);
    private static readonly Regex IfClauseRegex = new(@"\bIf\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StatNoiseRegex = new(@"\b(ARMOUR|STAMINA|SKILL|SPEED|COMBAT BONUS|SPECIAL ABILITIES)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ArmorZeroRegex = new(@"(?<=ARMOUR\s+to\s+)[oO]\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ArmorReducedRegex = new(@"(?<=ARMOUR\s+is\s+reduced\s+to\s+)[oO]\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ImmutableArray<MonsterCatalogEntry> _monsterCatalog;

    public BookEncounterNormalizer(string? monsterCatalogPath)
    {
        _monsterCatalog = LoadMonsterCatalog(monsterCatalogPath);
    }

    public ImmutableArray<BookBlock> Normalize(IEnumerable<BookBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var normalizedBlocks = ImmutableArray.CreateBuilder<BookBlock>();

        foreach (var block in blocks.OrderBy(block => block.Id))
        {
            NormalizeChoices(block);

            if (block.Enemies.Count == 0 && block.Monsters.Count == 0)
            {
                TryNormalizeMonsterEncounter(block);
            }

            normalizedBlocks.Add(block);
        }

        return normalizedBlocks.ToImmutable();
    }

    private static void NormalizeChoices(BookBlock block)
    {
        foreach (var choice in block.Choices)
        {
            choice.Text = NormalizeChoiceText(choice.Text);
        }
    }

    private void TryNormalizeMonsterEncounter(BookBlock block)
    {
        if (_monsterCatalog.Length == 0 || block.Choices.Count == 0 || block.Choices.Count > 3)
        {
            return;
        }

        var candidate = FindBestMonsterCandidate(block.Text);
        if (candidate is null)
        {
            return;
        }

        var outcomeTargets = candidate.Value.Entry.GetOutcomeTargets();
        if (outcomeTargets.Count == 0)
        {
            return;
        }

        var existingTargets = block.Choices.Select(choice => choice.To).ToImmutableHashSet();
        if (existingTargets.Except(outcomeTargets).Any())
        {
            return;
        }

        var introText = block.Text[..candidate.Value.HeaderIndex].Trim();
        if (string.IsNullOrWhiteSpace(introText))
        {
            return;
        }

        var choiceTextByTarget = BuildChoiceTextByTarget(candidate.Value.Entry, block.Choices);
        if (choiceTextByTarget.Count == 0)
        {
            return;
        }

        var originalChoices = block.Choices.ToArray();
        block.Text = introText;
        block.Choices.Clear();

        foreach (var choice in BuildNormalizedChoices(originalChoices, choiceTextByTarget))
        {
            block.Choices.Add(choice);
        }

        block.Monsters.Add(candidate.Value.Entry.ToMonster());
    }

    private static IEnumerable<BookChoice> BuildNormalizedChoices(
        IEnumerable<BookChoice> choices,
        IReadOnlyDictionary<int, string> choiceTextByTarget)
        => choices
            .Select(choice => new BookChoice
            {
                To = choice.To,
                ShowWhenDisabled = choice.ShowWhenDisabled,
                Condition = choice.Condition,
                Effect = choice.Effect,
                Text = choiceTextByTarget[choice.To],
            })
            .ToArray();

    private static Dictionary<int, string> BuildChoiceTextByTarget(
        MonsterCatalogEntry entry,
        IEnumerable<BookChoice> existingChoices)
    {
        var groupedKinds = entry.GetOutcomeKindsByTarget();
        Dictionary<int, string> choiceTextByTarget = [];

        foreach (var choice in existingChoices)
        {
            if (!groupedKinds.TryGetValue(choice.To, out var kinds))
            {
                continue;
            }

            var cleaned = NormalizeChoiceText(choice.Text);
            choiceTextByTarget[choice.To] = IsUsableChoiceText(cleaned)
                ? cleaned
                : BuildFallbackChoiceText(kinds, entry.Name);
        }

        return choiceTextByTarget;
    }

    private MonsterCatalogMatch? FindBestMonsterCandidate(string text)
    {
        MonsterCatalogMatch? bestMatch = null;

        foreach (var entry in _monsterCatalog)
        {
            var match = entry.FindCombatHeader(text);
            if (match is null)
            {
                continue;
            }

            if (bestMatch is null
                || match.Value.Entry.Name.Length > bestMatch.Value.Entry.Name.Length
                || (match.Value.Entry.Name.Length == bestMatch.Value.Entry.Name.Length
                    && match.Value.HeaderIndex > bestMatch.Value.HeaderIndex))
            {
                bestMatch = match;
            }
        }

        return bestMatch;
    }

    private static bool IsUsableChoiceText(string text)
        => !string.IsNullOrWhiteSpace(text)
            && text.StartsWith("If ", StringComparison.OrdinalIgnoreCase);

    private static string BuildFallbackChoiceText(
        ImmutableArray<BattleOutcomeKind> kinds,
        string entityName)
    {
        if (kinds.Contains(BattleOutcomeKind.Win) && kinds.Contains(BattleOutcomeKind.Escape))
        {
            return $"If you defeat the {entityName}, or if you successfully Escape.";
        }

        if (kinds.Contains(BattleOutcomeKind.Win))
        {
            return $"If you defeat the {entityName}.";
        }

        if (kinds.Contains(BattleOutcomeKind.Lose))
        {
            return "If it defeats you.";
        }

        return "If you Escape.";
    }

    private static string NormalizeChoiceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex.Replace(text, " ").Trim();
        normalized = LeadingNoiseRegex.Replace(normalized, string.Empty);
        normalized = LeadingBlockNumberRegex.Replace(normalized, string.Empty);
        normalized = normalized
            .Replace("ARMOVR", "ARMOUR", StringComparison.OrdinalIgnoreCase)
            .Replace("Retum", "Return", StringComparison.OrdinalIgnoreCase)
            .Replace("Tum", "Turn", StringComparison.OrdinalIgnoreCase)
            .Replace("reduced too", "reduced to 0", StringComparison.OrdinalIgnoreCase)
            .Replace("ARMOUR too", "ARMOUR to 0", StringComparison.OrdinalIgnoreCase);
        normalized = ArmorZeroRegex.Replace(normalized, "0");
        normalized = ArmorReducedRegex.Replace(normalized, "0");

        var ifMatch = IfClauseRegex.Match(normalized);
        if (ifMatch.Success && ifMatch.Index > 0 && StatNoiseRegex.IsMatch(normalized[..ifMatch.Index]))
        {
            normalized = normalized[ifMatch.Index..].TrimStart();
        }

        if (normalized.StartsWith("None ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["None ".Length..];
        }

        if (!string.IsNullOrWhiteSpace(normalized)
            && !normalized.EndsWith(".", StringComparison.Ordinal)
            && !normalized.EndsWith("!", StringComparison.Ordinal)
            && !normalized.EndsWith("?", StringComparison.Ordinal))
        {
            normalized += ".";
        }

        return normalized;
    }

    private static ImmutableArray<MonsterCatalogEntry> LoadMonsterCatalog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var monsters = ImmutableArray.CreateBuilder<MonsterCatalogEntry>();

        foreach (var monsterProperty in document.RootElement.EnumerateObject())
        {
            var value = monsterProperty.Value;
            monsters.Add(new MonsterCatalogEntry(
                name: value.GetProperty("name").GetString() ?? monsterProperty.Name,
                description: value.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                frame: value.TryGetProperty("types", out var frame) ? ConvertFrame(frame.GetInt32()) : RobotFrame.Unspecified,
                armor: value.TryGetProperty("armor", out var armor) ? armor.GetInt32() : 0,
                armorMax: value.TryGetProperty("armorMax", out var armorMax) ? armorMax.GetInt32() : 0,
                skill: value.TryGetProperty("skill", out var skill) ? skill.GetInt32() : 0,
                skillMax: value.TryGetProperty("skillMax", out var skillMax) ? skillMax.GetInt32() : 0,
                speed: value.TryGetProperty("speed", out var speed) ? ConvertSpeed(speed.GetInt32()) : SpeedBand.Static,
                speedMax: value.TryGetProperty("speedMax", out var speedMax) ? ConvertSpeed(speedMax.GetInt32()) : SpeedBand.Static,
                battleOutcome: value.TryGetProperty("battleResult", out var battleResult) ? ConvertBattleOutcome(battleResult) : null));
        }

        return monsters.ToImmutable();
    }

    private static RobotFrame ConvertFrame(int value)
        => value switch
        {
            1 => RobotFrame.Dinosaur,
            2 => RobotFrame.Humanoid,
            _ => RobotFrame.Unspecified,
        };

    private static SpeedBand ConvertSpeed(int value)
        => value switch
        {
            1 => SpeedBand.Slow,
            2 => SpeedBand.Average,
            3 => SpeedBand.Fast,
            4 => SpeedBand.VeryFast,
            5 => SpeedBand.UltraFast,
            _ => SpeedBand.Static,
        };

    private static BattleOutcome? ConvertBattleOutcome(JsonElement battleResult)
    {
        BattleOutcome? outcome = null;

        if (battleResult.TryGetProperty("win", out var win))
        {
            outcome ??= new BattleOutcome();
            outcome.Win = win.GetInt32();
            outcome.WinSpecified = true;
        }

        if (battleResult.TryGetProperty("lose", out var lose))
        {
            outcome ??= new BattleOutcome();
            outcome.Lose = lose.GetInt32();
            outcome.LoseSpecified = true;
        }

        if (battleResult.TryGetProperty("escape", out var escape))
        {
            outcome ??= new BattleOutcome();
            outcome.Escape = escape.GetInt32();
            outcome.EscapeSpecified = true;
        }

        return outcome;
    }

    private readonly record struct MonsterCatalogMatch(MonsterCatalogEntry Entry, int HeaderIndex);

    private enum BattleOutcomeKind
    {
        Win,
        Lose,
        Escape,
    }

    private sealed class MonsterCatalogEntry
    {
        private readonly Regex _headerPattern;

        public MonsterCatalogEntry(
            string name,
            string description,
            RobotFrame frame,
            int armor,
            int armorMax,
            int skill,
            int skillMax,
            SpeedBand speed,
            SpeedBand speedMax,
            BattleOutcome? battleOutcome)
        {
            Name = name;
            Description = description;
            Frame = frame;
            Armor = armor;
            ArmorMax = armorMax;
            Skill = skill;
            SkillMax = skillMax;
            Speed = speed;
            SpeedMax = speedMax;
            BattleOutcome = battleOutcome;

            var tokens = name
                .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Regex.Escape);
            _headerPattern = new Regex(
                $@"\b{string.Join(@"[\s-]*", tokens)}\b[\s:;\-–—]{{0,8}}(?:ARMOUR|ARMOVR|STAMINA|SKILL|SPEED|COMBAT\s+BONUS)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public string Name { get; }

        public string Description { get; }

        public RobotFrame Frame { get; }

        public int Armor { get; }

        public int ArmorMax { get; }

        public int Skill { get; }

        public int SkillMax { get; }

        public SpeedBand Speed { get; }

        public SpeedBand SpeedMax { get; }

        public BattleOutcome? BattleOutcome { get; }

        public MonsterCatalogMatch? FindCombatHeader(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = _headerPattern.Match(text);
            return match.Success
                ? new MonsterCatalogMatch(this, match.Index)
                : null;
        }

        public ImmutableHashSet<int> GetOutcomeTargets()
        {
            ImmutableHashSet<int>.Builder targets = ImmutableHashSet.CreateBuilder<int>();

            if (BattleOutcome?.WinSpecified == true && BattleOutcome.Win > 0)
            {
                targets.Add(BattleOutcome.Win);
            }

            if (BattleOutcome?.LoseSpecified == true && BattleOutcome.Lose > 0)
            {
                targets.Add(BattleOutcome.Lose);
            }

            if (BattleOutcome?.EscapeSpecified == true && BattleOutcome.Escape > 0)
            {
                targets.Add(BattleOutcome.Escape);
            }

            return targets.ToImmutable();
        }

        public Dictionary<int, ImmutableArray<BattleOutcomeKind>> GetOutcomeKindsByTarget()
        {
            Dictionary<int, ImmutableArray<BattleOutcomeKind>.Builder> builders = [];
            AddOutcome(BattleOutcome?.WinSpecified == true ? BattleOutcome.Win : 0, BattleOutcomeKind.Win);
            AddOutcome(BattleOutcome?.LoseSpecified == true ? BattleOutcome.Lose : 0, BattleOutcomeKind.Lose);
            AddOutcome(BattleOutcome?.EscapeSpecified == true ? BattleOutcome.Escape : 0, BattleOutcomeKind.Escape);

            return builders.ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutable());

            void AddOutcome(int target, BattleOutcomeKind kind)
            {
                if (target <= 0)
                {
                    return;
                }

                if (!builders.TryGetValue(target, out var builder))
                {
                    builder = ImmutableArray.CreateBuilder<BattleOutcomeKind>();
                    builders[target] = builder;
                }

                builder.Add(kind);
            }
        }

        public BookMonster ToMonster()
            => new()
            {
                Tag = "Monster",
                Name = Name,
                Description = Description,
                Frame = Frame,
                Armor = Armor,
                ArmorMax = ArmorMax > 0 ? ArmorMax : Armor,
                Skill = Skill,
                SkillMax = SkillMax > 0 ? SkillMax : Skill,
                Speed = Speed,
                SpeedMax = SpeedMax != SpeedBand.Static ? SpeedMax : Speed,
                BattleOutcome = CloneBattleOutcome(BattleOutcome),
            };
    }

    private static BattleOutcome? CloneBattleOutcome(BattleOutcome? battleOutcome)
        => battleOutcome is null
            ? null
            : new BattleOutcome
            {
                Win = battleOutcome.Win,
                WinSpecified = battleOutcome.WinSpecified,
                Lose = battleOutcome.Lose,
                LoseSpecified = battleOutcome.LoseSpecified,
                Escape = battleOutcome.Escape,
                EscapeSpecified = battleOutcome.EscapeSpecified,
            };
}
