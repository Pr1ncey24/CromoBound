using CromoBound.Data;
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Rules;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What a card does as the engine runs it (spec §4.1). <see cref="ManualLines"/> are the 1-based text lines players
/// resolve by hand; <see cref="Unsupported"/> says why a mapped file is played as Unmapped. <see cref="KeywordEntries"/> are the
/// file's keywords with their values and costs (empty for Unmapped cards, whose keywords come from their text).</summary>
internal sealed record CardEffectInfo(
    MappingStatus Status,
    IReadOnlyList<Ability> Abilities,
    IReadOnlySet<DisplayKeyword> Keywords,
    IReadOnlyList<int> ManualLines,
    IReadOnlyList<string> Unsupported,
    IReadOnlyList<KeywordEntry> KeywordEntries);

/// <summary>Effects per card id, cached. A file the engine can't fully run yet is treated as Unmapped, so the card plays by hand as in 2a. Runes get no abilities: basic runes keep 2a's UseRune (spec §13).</summary>
internal sealed class CardEffects(CardDatabase db)
{
    private readonly Dictionary<string, CardEffectInfo> _cache = new(StringComparer.Ordinal);

    public CardEffectInfo For(string cardId)
    {
        if (!_cache.TryGetValue(cardId, out var info)) _cache[cardId] = info = Build(db.Cards[cardId]);
        return info;
    }

    private CardEffectInfo Build(Card card)
    {
        var lines = RichText.Lines(card.Text.Rich);
        List<int> all = [.. Enumerable.Range(1, lines.Count)];
        if (!db.Effects.TryGetValue(card.Id, out var loaded) || loaded.File.Status == MappingStatus.Unmapped)
            return Unmapped(card, all, []);
        var file = loaded.File;
        if (card.Type == CardType.Rune) return new(file.Status, [], new HashSet<DisplayKeyword>(), [], [], []);
        var unsupported = EffectsSupport.Problems(file, card.Type);
        if (unsupported.Count > 0) return Unmapped(card, all, unsupported);

        IReadOnlySet<DisplayKeyword> keywords = file.Keywords.Select(k => Enum.Parse<DisplayKeyword>(k.Keyword.ToString())).ToHashSet();
        IReadOnlyList<Ability> abilities = [.. file.Abilities, .. KeywordAbilities(file, lines)];
        if (file.Status == MappingStatus.Full) return new(MappingStatus.Full, abilities, keywords, [], [], file.Keywords);
        var covered = file.Abilities.Where(a => a.Line is not null).SelectMany(a => a.Line!.Lines).ToHashSet();
        List<int> manual = [.. all.Where(n => !covered.Contains(n) && !CardKeywords.IsKeywordLine(lines[n - 1]))];
        return new(MappingStatus.Partial, abilities, keywords, manual, [], file.Keywords);
    }

    /// <summary>The abilities keywords stand for (spec §8.1), listed after the file's own so ability indices follow the JSON. Each
    /// carries the keyword's text line, so its chain item has text.</summary>
    private static IEnumerable<Ability> KeywordAbilities(EffectsFile file, IReadOnlyList<string> lines)
    {
        foreach (var entry in file.Keywords)
        {
            var line = KeywordLine(lines, entry.Keyword);
            switch (entry.Keyword)
            {
                case MechanicalKeyword.Deathknell:
                    yield return OwnTrigger(TriggerEvent.Dies, entry.Steps, line);
                    break;
                case MechanicalKeyword.Vision:
                    yield return OwnTrigger(TriggerEvent.Played, [new PredictStep()], line);
                    break;
                case MechanicalKeyword.Hunt:
                    IReadOnlyList<Step> gain = [new GainXpStep { Amount = entry.Value!.Value }];
                    yield return OwnTrigger(TriggerEvent.Conquer, gain, line);
                    yield return OwnTrigger(TriggerEvent.Hold, gain, line);
                    break;
                case MechanicalKeyword.Empower:
                    yield return new ActivatedAbility
                    {
                        Line = line,
                        Cost = entry.Cost,
                        UseOnlyIf = new Condition { Not = new Condition { Empowered = true } },
                        Steps = [new EmpowerStep { Target = ObjectRef.Self }],
                    };
                    break;
                case MechanicalKeyword.Equip:
                    yield return new ActivatedAbility
                    {
                        Line = line,
                        Cost = entry.Cost,
                        Steps =
                        [
                            new AttachStep
                            {
                                Target = ObjectRef.Self,
                                To = new ObjectRef { Select = SelectKind.Unit, Count = 1, Filter = new Filter { Relation = Relation.Friendly } },
                            },
                        ],
                    };
                    break;
                case MechanicalKeyword.Weaponmaster:
                    yield return OwnTrigger(TriggerEvent.Played, [new WeaponmasterStep()], line);
                    break;
            }
        }
    }

    /// <summary>The first text line that starts with the keyword ("[Deathknell] ...", "[Hunt 3] ..."), or null.</summary>
    private static LineRef? KeywordLine(IReadOnlyList<string> lines, MechanicalKeyword keyword)
    {
        for (var n = 1; n <= lines.Count; n++)
            if (lines[n - 1].StartsWith($"[{keyword}", StringComparison.Ordinal)) return n;
        return null;
    }

    /// <summary>"When I ...": a trigger on the card's own event.</summary>
    private static TriggeredAbility OwnTrigger(TriggerEvent kind, IReadOnlyList<Step> steps, LineRef? line) =>
        new() { Line = line, Trigger = new Trigger { Event = kind, Subject = ObjectRef.Self }, Steps = steps };

    private static CardEffectInfo Unmapped(Card card, List<int> lines, IReadOnlyList<string> unsupported) =>
        new(MappingStatus.Unmapped, [], CardKeywords.Own(card), lines, unsupported, []);
}
