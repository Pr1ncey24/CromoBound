namespace CromoBound.Engine.State;

/// <summary>A player by turn-order index (0 = first seat). Players are always a list, never "me and you".</summary>
public readonly record struct PlayerId(int Index)
{
    public override string ToString() => $"P{Index + 1}";
}

/// <summary>A game object. A card gets a new id each time it crosses into or out of a non-board zone (CR 124).</summary>
public readonly record struct ObjectId(int Value)
{
    public override string ToString() => $"#{Value}";
}
