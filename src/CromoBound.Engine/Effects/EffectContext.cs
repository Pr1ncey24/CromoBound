using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>A stored step result ("store"): the objects, players or number it produced, and whether the step happened.</summary>
internal sealed record EffectVar(IReadOnlyList<ObjectId> Objects, IReadOnlyList<PlayerId> Players, int? Number, bool Happened)
{
    public static EffectVar Empty { get; } = new([], [], null, false);
}

/// <summary>Everything one resolution carries (spec §4.2). Built when a card is played or an ability goes on the chain.</summary>
internal sealed class EffectContext
{
    public required PlayerId Controller { get; init; }

    /// <summary>The object the ability belongs to: the spell on the chain, or the permanent.</summary>
    public ObjectId? Source { get; init; }

    /// <summary>The source's card id, kept even after the source leaves play.</summary>
    public required string SourceCardId { get; init; }

    /// <summary>The target selectors (see <see cref="TargetSlots"/>), in JSON order.</summary>
    public IReadOnlyList<ObjectRef> Slots { get; init; } = [];

    /// <summary>The targets chosen so far; entry i belongs to <see cref="Slots"/>[i].</summary>
    public List<IReadOnlyList<ObjectId>> Targets { get; } = [];

    public Dictionary<string, EffectVar> Vars { get; } = new(StringComparer.Ordinal);
}
