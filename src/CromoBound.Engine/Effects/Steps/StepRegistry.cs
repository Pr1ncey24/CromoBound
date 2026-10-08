using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The step types the engine runs, each with its handler. A new step is one handler plus one line here.</summary>
internal static class StepRegistry
{
    private static readonly Dictionary<Type, IStepHandler> Handlers = new()
    {
        [typeof(DrawStep)] = new DrawHandler(),
        [typeof(BurnStep)] = new BurnHandler(),
        [typeof(DealStep)] = new DealHandler(),
        [typeof(KillStep)] = new KillHandler(),
    };

    public static bool Supports(Type stepType) => Handlers.ContainsKey(stepType);

    public static IStepHandler For(Step step) => Handlers[step.GetType()];
}
