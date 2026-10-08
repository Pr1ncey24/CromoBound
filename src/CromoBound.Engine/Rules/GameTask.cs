namespace CromoBound.Engine.Rules;

/// <summary>A piece of mandatory rules procedure (CR 333). Tracks its own progress so it can pause for a decision.</summary>
internal abstract class GameTask
{
    /// <summary>True once the task has run; cleanups never interrupt a started task.</summary>
    public bool Started { get; set; }

    /// <summary>True when finished. False when not finished yet (usually after asking a player); it runs again later.</summary>
    public abstract bool Run(Game game);

    /// <summary>Turn-structure tasks don't start while a chain or showdown is running; priority or focus is asked first.</summary>
    internal virtual bool WaitsForNeutralOpen => false;
}

internal sealed class StepTask(Action<Game> step, bool waitsForNeutralOpen = false) : GameTask
{
    internal override bool WaitsForNeutralOpen => waitsForNeutralOpen;

    public override bool Run(Game game)
    {
        step(game);
        return true;
    }
}
