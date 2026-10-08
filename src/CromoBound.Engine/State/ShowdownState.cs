namespace CromoBound.Engine.State;

/// <summary>The showdown in progress (CR 341-348). A combat showdown also has an attacker and a defender (CR 464).</summary>
public sealed class ShowdownState(int battlefield)
{
    public int Battlefield { get; } = battlefield;
    public bool IsCombat { get; set; }
    public PlayerId? Attacker { get; set; }
    public PlayerId? Defender { get; set; }

    /// <summary>Consecutive passes with an empty chain; it ends when every player has passed in a row.</summary>
    public int Passes { get; set; }
}
