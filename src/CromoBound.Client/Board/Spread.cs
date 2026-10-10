namespace CromoBound.Client.Board;

/// <summary>Lays a row of cards out in a fixed box: at their normal gap while they fit, overlapping evenly once they don't, so the
/// row never leaves its box. Lefts are relative to the box's start.</summary>
public static class Spread
{
    public static double[] Lefts(int count, double card, double box, double gap, bool center = false)
    {
        if (count == 0) return [];
        var natural = count * card + (count - 1) * gap;
        if (natural <= box)
        {
            var start = center ? (box - natural) / 2 : 0;
            return [.. Enumerable.Range(0, count).Select(i => start + i * (card + gap))];
        }
        var step = count == 1 ? 0 : Math.Max(0, (box - card) / (count - 1));
        return [.. Enumerable.Range(0, count).Select(i => i * step)];
    }
}

/// <summary>The rows the board lays cards out in, in canvas pixels (the zones' insides, padding taken off).</summary>
public static class SideBoxes
{
    public const double Card = 76;
    public const double RuneCard = 44;
    public const double HandCard = 104;
    public const double Back = 70;
    public const double RuneRow = 306;
    public const double BaseRow = 368;
    public const double LaneRow = 344;
    public const double LaneRowBesideHidden = 262;
    public const double HandRow = 560;
    public const double BackRow = 300;
}
