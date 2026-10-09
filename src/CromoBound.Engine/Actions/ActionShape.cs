namespace CromoBound.Engine.Actions;

/// <summary>Catches actions whose non-nullable references are null, which JSON like <c>{"swaps":null}</c> can produce.</summary>
internal static class ActionShape
{
    /// <summary>The reason an action is malformed, or null when every reference the record declares non-nullable is set.</summary>
    public static string? Check(PlayerAction? action) => action switch
    {
        null => "There is no action.",
        StandardMove move => move.Units is null ? Missing(nameof(move.Units)) : null,
        AdjustCost cost => cost.AddPower is null ? Missing(nameof(cost.AddPower)) : cost.RemovePower is null ? Missing(nameof(cost.RemovePower)) : null,
        PayCost pay => pay.Exhaust is null ? Missing(nameof(pay.Exhaust)) : pay.Recycle is null ? Missing(nameof(pay.Recycle)) : null,
        AssignDamage assign => assign.Assignments is null ? Missing(nameof(assign.Assignments))
            : assign.Assignments.Any(a => a is null) ? "An assignment is missing." : null,
        PickBattlefield pick => pick.Printing is null ? Missing(nameof(pick.Printing)) : null,
        SubmitSideboard sideboard => sideboard.Swaps is null ? Missing(nameof(sideboard.Swaps))
            : sideboard.Swaps.Any(s => s is null || s.Out is null || s.In is null) ? "A swap is missing a card." : null,
        Mulligan mulligan => mulligan.SetAside is null ? Missing(nameof(mulligan.SetAside)) : null,
        ManualCreateToken token => token.TokenId is null ? Missing(nameof(token.TokenId)) : null,
        AddAbilityToChain ability => ability.Cost is { Power: null } ? "The cost has no power list." : null,
        ChooseTargets choose => choose.Targets is null ? Missing(nameof(choose.Targets)) : null,
        ChooseCards cards => cards.Cards is null ? Missing(nameof(cards.Cards)) : null,
        OrderTriggers order => order.Order is null ? Missing(nameof(order.Order)) : null,
        _ => null,
    };

    private static string Missing(string name) => $"'{name}' is missing.";
}
