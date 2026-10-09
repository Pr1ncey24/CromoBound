using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Values effects change while they apply (spec §4.7). Evaluated live: nothing is stored, so replay stays trivial.</summary>
internal static class Modifiers
{
    /// <summary>Printed Might, +1 while buffed, manual and effect Might modifiers, Assault X while attacking and Shield X while
    /// defending (CR 807, 814).</summary>
    public static int MightOf(Game game, CardInstance unit)
    {
        var combat = unit.Role switch
        {
            CombatRole.Attacker => KeywordValue(game, unit, MechanicalKeyword.Assault),
            CombatRole.Defender => KeywordValue(game, unit, MechanicalKeyword.Shield),
            _ => 0,
        };
        return (game.CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount) + combat;
    }

    /// <summary>The sum of a numbered keyword's values on the card: a missing value is 1, and several instances stack.</summary>
    public static int KeywordValue(Game game, CardInstance instance, MechanicalKeyword keyword) =>
        game.Effects.For(instance.CardId).KeywordEntries.Where(k => k.Keyword == keyword).Sum(k => k.Value ?? 1);
}
