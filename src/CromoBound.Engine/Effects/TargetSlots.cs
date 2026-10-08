using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>The target selectors of a step list (spec §5.1, rule 355): selectors with count or upTo over public board objects.</summary>
internal static class TargetSlots
{
    public static IReadOnlyList<ObjectRef> Of(IReadOnlyList<Step> steps) =>
        [.. steps.OfType<TargetStep>().Select(s => s.Target).Where(IsTarget)];

    public static bool IsTarget(ObjectRef reference) =>
        reference.Select is SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent
        && (reference.Count is not null || reference.UpTo is not null);

    /// <summary>The slot of a selector, found by reference: two equal-looking selectors are two slots (Falling Star). -1 when it isn't a slot.</summary>
    public static int IndexOf(IReadOnlyList<ObjectRef> slots, ObjectRef reference)
    {
        for (var i = 0; i < slots.Count; i++)
            if (ReferenceEquals(slots[i], reference)) return i;
        return -1;
    }
}
