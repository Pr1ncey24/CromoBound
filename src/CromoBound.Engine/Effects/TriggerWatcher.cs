using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>A triggered ability waiting to go on the chain (spec §4.6). <see cref="Source"/> may have left play (Deathknell).</summary>
internal sealed record PendingTrigger(PlayerId Controller, ObjectId Source, string SourceCardId, TriggeredAbility Ability);

/// <summary>Maps engine events to trigger events (spec §4.6) and finds the abilities they trigger. Pure: reads the game, changes
/// nothing. It runs as each event is emitted, so a dying unit (UnitDied comes before the move) is still there to be read.</summary>
internal static class TriggerWatcher
{
    public static List<PendingTrigger> Collect(Game game, GameEvent gameEvent) => gameEvent switch
    {
        UnitDied died => Own(game, died.Unit, died.Controller, TriggerEvent.Dies),
        StatusChanged { Status: StatusKind.Empowered, Value: true } empowered =>
            Own(game, empowered.Object, game.State[empowered.Object].Controller, TriggerEvent.BecameEmpowered),
        CardPlayed played => Own(game, played.Card, played.Controller, TriggerEvent.Played),
        BattlefieldScored scored => Scored(game, scored),
        _ => [],
    };

    /// <summary>The object's abilities that trigger on its own event ("subject": Self).</summary>
    private static List<PendingTrigger> Own(Game game, ObjectId id, PlayerId controller, TriggerEvent kind) =>
        !game.State.Exists(id) ? [] :
        [
            .. Triggered(game, id, kind)
                .Where(a => a.Trigger.Subject?.Ref == RefKind.Self && Holds(game, a, controller, id))
                .Select(a => new PendingTrigger(controller, id, game.State[id].CardId, a)),
        ];

    /// <summary>Hold and Conquer: the battlefield's own "when you hold (or conquer) here", when its controller is the scorer,
    /// and the scorer's units there whose own trigger it is (Hunt).</summary>
    private static List<PendingTrigger> Scored(Game game, BattlefieldScored scored)
    {
        var kind = scored.Kind == ScoreKind.Hold ? TriggerEvent.Hold : TriggerEvent.Conquer;
        var card = game.State.Battlefields[scored.Battlefield].Card;
        var found = new List<PendingTrigger>();
        if (game.HandledBy(game.State[card]) == scored.Player)
            found.AddRange(Triggered(game, card, kind)
                .Where(a => a.Trigger is { By.Kind: PlayerKind.You, Where.Ref: RefKind.Here } && Holds(game, a, scored.Player, card))
                .Select(a => new PendingTrigger(scored.Player, card, game.State[card].CardId, a)));
        foreach (var unit in game.UnitsAt(Place.Battlefield(scored.Battlefield)).Where(u => u.Controller == scored.Player))
            found.AddRange(Own(game, unit.Id, unit.Controller, kind));
        return found;
    }

    /// <summary>A trigger's "if" (docs/effects-fiora.md, Plan O deviation 3), read when the event happens, in the source's context.</summary>
    private static bool Holds(Game game, TriggeredAbility ability, PlayerId controller, ObjectId source) =>
        ability.If is not { } condition
        || ConditionResolver.Holds(game, new EffectContext { Controller = controller, Source = source, SourceCardId = game.State[source].CardId }, condition);

    private static IEnumerable<TriggeredAbility> Triggered(Game game, ObjectId id, TriggerEvent kind) =>
        game.Effects.For(game.State[id].CardId).Abilities.OfType<TriggeredAbility>().Where(a => a.Trigger.Event == kind);
}
