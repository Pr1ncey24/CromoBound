using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>A rune in a player's Base, as payment sees it.</summary>
public sealed record RuneInfo(ObjectId Id, Domain Domain, bool Exhausted);

/// <summary>Cost calculation and payment matching (CR 356-357, spec §7.4).</summary>
public static class Payment
{
    private static readonly IReadOnlyList<Domain> AllDomains = [.. Enum.GetValues<Domain>().Where(d => d != Domain.Colorless)];

    /// <summary>The printed cost (0 base when played from Hidden), plus Accelerate: 1 energy and one [C] (Self) symbol.</summary>
    public static TotalCost CostOf(Card card, bool fromHidden, bool accelerate)
    {
        var energy = fromHidden ? 0 : card.Cost?.Energy ?? 0;
        var power = new List<PowerSymbol>();
        if (!fromHidden && card.Cost is { } cost) power.AddRange(cost.Power);
        if (accelerate)
        {
            energy += 1;
            power.Add(PowerSymbol.Self);
        }
        return new TotalCost(energy, power);
    }

    /// <summary>Applies a player's cost adjustment. Missing symbols to remove are ignored; energy never goes below 0.</summary>
    public static TotalCost Adjust(TotalCost cost, int energy, IEnumerable<PowerSymbol> add, IEnumerable<PowerSymbol> remove)
    {
        var power = cost.Power.ToList();
        foreach (var symbol in remove) power.Remove(symbol);
        power.AddRange(add);
        return new TotalCost(Math.Max(0, cost.Energy + energy), power);
    }

    /// <summary>Pays the cost from the pool. Returns false and leaves the pool untouched when it can't.</summary>
    public static bool TryPay(RunePool pool, TotalCost cost, IReadOnlyList<Domain> cardDomains)
    {
        var work = pool.Clone();
        if (work.Energy < cost.Energy) return false;
        work.Energy -= cost.Energy;
        foreach (var symbol in cost.Power.OrderBy(Rank))
            if (!TakePower(work, Accepts(symbol, cardDomains))) return false;
        pool.CopyFrom(work);
        return true;
    }

    /// <summary>A payment that works: the pool first, then ready runes exhausted for energy, then runes recycled for power
    /// (preferring runes already exhausted). Null when the runes can't cover the cost.</summary>
    public static PaymentSuggestion? Suggest(RunePool pool, TotalCost cost, IReadOnlyList<Domain> cardDomains, IReadOnlyList<RuneInfo> runes)
    {
        var work = pool.Clone();
        var energyNeeded = Math.Max(0, cost.Energy - work.Energy);
        var exhaust = runes.Where(r => !r.Exhausted).Take(energyNeeded).ToList();
        if (exhaust.Count < energyNeeded) return null;
        work.Energy += exhaust.Count - cost.Energy;

        var recycle = new List<RuneInfo>();
        foreach (var symbol in cost.Power.OrderBy(Rank))
        {
            var accepted = Accepts(symbol, cardDomains);
            if (TakePower(work, accepted)) continue;
            var rune = runes
                .Where(r => !recycle.Contains(r) && accepted.Contains(r.Domain))
                .OrderBy(r => r.Exhausted || exhaust.Contains(r) ? 0 : 1)
                .FirstOrDefault();
            if (rune is null) return null;
            recycle.Add(rune);
        }
        return new PaymentSuggestion([.. exhaust.Select(r => r.Id)], [.. recycle.Select(r => r.Id)]);
    }

    /// <summary>Specific domains first, then [C] (Self), then [A] (Any): the narrowest symbols claim power first.</summary>
    private static int Rank(PowerSymbol symbol) => symbol switch
    {
        PowerSymbol.Self => 1,
        PowerSymbol.Any => 2,
        _ => 0,
    };

    /// <summary>Domains a symbol accepts: its own; Self any of the card's domains (any domain if it has none, CR 135.2.e.6); Any every domain.</summary>
    private static IReadOnlyList<Domain> Accepts(PowerSymbol symbol, IReadOnlyList<Domain> cardDomains)
    {
        var colored = cardDomains.Where(d => d != Domain.Colorless).ToList();
        return symbol switch
        {
            PowerSymbol.Any => AllDomains,
            PowerSymbol.Self => colored.Count > 0 ? colored : AllDomains,
            _ => [Enum.Parse<Domain>(symbol.ToString())],
        };
    }

    /// <summary>Takes one power from the accepted domain with the most available, else one universal power.</summary>
    private static bool TakePower(RunePool pool, IReadOnlyList<Domain> accepted)
    {
        Domain? best = null;
        foreach (var domain in accepted)
            if (pool.Power.GetValueOrDefault(domain) > 0 && (best is null || pool.Power[domain] > pool.Power[best.Value]))
                best = domain;
        if (best is { } chosen)
        {
            pool.Power[chosen]--;
            return true;
        }
        if (pool.UniversalPower == 0) return false;
        pool.UniversalPower--;
        return true;
    }
}
