using CromoBound.Models.Effects;

namespace CromoBound.Engine.State;

public enum CombatRole { Attacker, Defender }

/// <summary>A temporary change to a unit's Might (e.g. "+2 this turn").</summary>
public sealed record MightModifier(int Amount, Duration Duration);

/// <summary>One physical card (or token) in a game. Mutable; owned by <see cref="GameState"/>.</summary>
public sealed class CardInstance
{
    internal CardInstance(ObjectId id, string cardId, string? printingId, PlayerId owner, bool isToken, Place place)
    {
        Id = id;
        CardId = cardId;
        PrintingId = printingId;
        Owner = owner;
        IsToken = isToken;
        Place = place;
        Controller = owner;
    }

    public ObjectId Id { get; private set; }
    public string CardId { get; }
    public string? PrintingId { get; }
    public PlayerId Owner { get; }
    public bool IsToken { get; }
    public Place Place { get; internal set; }

    public PlayerId Controller { get; set; }
    public bool Exhausted { get; set; }
    public bool Stunned { get; set; }
    public bool Buffed { get; set; }
    public bool Empowered { get; set; }
    public bool Facedown { get; set; }
    public CombatRole? Role { get; set; }
    public int Damage { get; set; }
    public List<MightModifier> Modifiers { get; } = [];
    public ObjectId? AttachedTo { get; set; }

    /// <summary>Turn number on which the card was hidden; it can be played from the next turn on (Hidden).</summary>
    public int? HiddenOnTurn { get; set; }

    /// <summary>CR 124: a card entering or leaving a non-board zone is a new object without temporary changes.</summary>
    internal void BecomeNewObject(ObjectId id)
    {
        Id = id;
        Controller = Owner;
        Exhausted = false;
        Stunned = false;
        Buffed = false;
        Empowered = false;
        Facedown = false;
        Role = null;
        Damage = 0;
        Modifiers.Clear();
        AttachedTo = null;
        HiddenOnTurn = null;
    }
}
