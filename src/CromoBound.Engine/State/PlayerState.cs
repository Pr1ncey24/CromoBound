using CromoBound.Models.Cards;

namespace CromoBound.Engine.State;

/// <summary>Resources added and not yet spent (CR 166). Emptied at the start of each Main phase and at end of turn.</summary>
public sealed class RunePool
{
    public int Energy { get; set; }
    public Dictionary<Domain, int> Power { get; } = [];

    /// <summary>Power that pays any domain (e.g. added by an [A] effect).</summary>
    public int UniversalPower { get; set; }

    public bool IsEmpty => Energy == 0 && UniversalPower == 0 && Power.Values.All(v => v == 0);

    public void Clear()
    {
        Energy = 0;
        UniversalPower = 0;
        Power.Clear();
    }

    public void AddPower(Domain domain, int amount = 1) => Power[domain] = Power.GetValueOrDefault(domain) + amount;

    public RunePool Clone()
    {
        var copy = new RunePool { Energy = Energy, UniversalPower = UniversalPower };
        foreach (var (domain, amount) in Power) copy.Power[domain] = amount;
        return copy;
    }

    public void CopyFrom(RunePool other)
    {
        Energy = other.Energy;
        UniversalPower = other.UniversalPower;
        Power.Clear();
        foreach (var (domain, amount) in other.Power) Power[domain] = amount;
    }
}

public sealed class PlayerState(PlayerId id)
{
    public PlayerId Id { get; } = id;
    public int Points { get; set; }
    public int Xp { get; set; }
    public RunePool Pool { get; } = new();
}
