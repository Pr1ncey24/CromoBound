using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Actions;

/// <summary>Escape hatches for effects resolved by hand (spec §8). Either player, any time during play; logged and highlighted.</summary>
public abstract record ManualAction : PlayerAction;

/// <summary>Draw, discard, kill, banish, return to hand, recall, recycle… Not for cards on the chain (use ManualCounter).</summary>
public sealed record ManualMoveCard(ObjectId Card, Place Destination, DeckPosition Position = DeckPosition.Top) : ManualAction;

public sealed record ManualDamage(ObjectId Unit, int Amount) : ManualAction;

public sealed record ManualHeal(ObjectId Unit, int Amount) : ManualAction;

public sealed record ManualSetStatus(ObjectId Card, StatusKind Status, bool Value) : ManualAction;

public sealed record ManualModifyMight(ObjectId Unit, int Amount, Duration Duration) : ManualAction;

public sealed record ManualAdjustPoints(PlayerId Player, int Amount) : ManualAction;

public sealed record ManualAdjustXp(PlayerId Player, int Amount) : ManualAction;

/// <summary>Adds (or, with negative amounts, removes) energy, power of one domain, and universal power. Never below 0.</summary>
public sealed record ManualAdjustPool(PlayerId Player, int Energy, Domain? Domain = null, int Power = 0, int UniversalPower = 0) : ManualAction;

public sealed record ManualCreateToken(string TokenId, Place Location, PlayerId Controller) : ManualAction;

public sealed record ManualGainControl(ObjectId Card, PlayerId Player) : ManualAction;

public sealed record ManualShuffle(PlayerId Owner, PlaceKind Deck) : ManualAction;

/// <summary>The submitting player looks at the top cards of a deck; only they see which cards.</summary>
public sealed record ManualLookAtTop(PlayerId Owner, PlaceKind Deck, int Count) : ManualAction;

/// <summary>Shows a card from a hidden zone to both players.</summary>
public sealed record ManualReveal(ObjectId Card) : ManualAction;

public sealed record ManualCounter(int ChainItem) : ManualAction;

/// <summary>Attaches gear in play to a unit in play (Equip or Weaponmaster resolved by hand, unmapped Equipment).</summary>
public sealed record ManualAttach(ObjectId Gear, ObjectId Unit) : ManualAction;

/// <summary>Detaches gear; at a battlefield, cleanup then recalls it to its controller's Base.</summary>
public sealed record ManualDetach(ObjectId Gear) : ManualAction;

/// <summary>Puts a triggered or activated ability (a line of the source's text, 1-based) on the chain, with an optional cost.</summary>
public sealed record AddAbilityToChain(ObjectId Source, int Line, AbilityKind Kind) : ManualAction
{
    public TotalCost? Cost { get; init; }
}
