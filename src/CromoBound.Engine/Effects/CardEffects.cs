using CromoBound.Data;
using CromoBound.Engine.Rules;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What a card does as the engine runs it (spec §4.1). <see cref="ManualLines"/> are the 1-based text lines players
/// resolve by hand; <see cref="Unsupported"/> says why a mapped file is played as Unmapped.</summary>
internal sealed record CardEffectInfo(
    MappingStatus Status,
    IReadOnlyList<Ability> Abilities,
    IReadOnlySet<DisplayKeyword> Keywords,
    IReadOnlyList<int> ManualLines,
    IReadOnlyList<string> Unsupported);

/// <summary>Effects per card id, cached. A file the engine can't fully run yet is treated as Unmapped, so the card plays by hand as in 2a.</summary>
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
        var unsupported = EffectsSupport.Problems(file);
        if (unsupported.Count > 0) return Unmapped(card, all, unsupported);

        IReadOnlySet<DisplayKeyword> keywords = file.Keywords.Select(k => Enum.Parse<DisplayKeyword>(k.Keyword.ToString())).ToHashSet();
        if (file.Status == MappingStatus.Full) return new(MappingStatus.Full, file.Abilities, keywords, [], []);
        var covered = file.Abilities.Where(a => a.Line is not null).SelectMany(a => a.Line!.Lines).ToHashSet();
        List<int> manual = [.. all.Where(n => !covered.Contains(n) && !CardKeywords.IsKeywordLine(lines[n - 1]))];
        return new(MappingStatus.Partial, file.Abilities, keywords, manual, []);
    }

    private static CardEffectInfo Unmapped(Card card, List<int> lines, IReadOnlyList<string> unsupported) =>
        new(MappingStatus.Unmapped, [], CardKeywords.Own(card), lines, unsupported);
}
