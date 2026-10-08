namespace CromoBound.Engine.State;

/// <summary>A battlefield in play. Its card sits at <see cref="Place.BattlefieldCard"/>; its facedown card at <see cref="Place.Facedown"/>.</summary>
public sealed class BattlefieldState(int index, ObjectId card)
{
    public const int FacedownCapacity = 1;

    public int Index { get; } = index;
    public ObjectId Card { get; } = card;
    public PlayerId? Controller { get; set; }

    /// <summary>The player whose unit applied Contested (CR 190.3), or null when not contested.</summary>
    public PlayerId? ContestedBy { get; set; }
}
