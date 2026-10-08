using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

public class PaymentTests
{
    private static readonly Domain[] FuryChaos = [Domain.Fury, Domain.Chaos];

    private static RunePool Pool(int energy = 0, int universal = 0, params (Domain Domain, int Amount)[] power)
    {
        var pool = new RunePool { Energy = energy, UniversalPower = universal };
        foreach (var (domain, amount) in power) pool.AddPower(domain, amount);
        return pool;
    }

    [Fact]
    public void Cost_is_printed_or_zero_base_from_hidden_and_accelerate_adds_energy_and_self()
    {
        var card = EngineTestDb.Create().Cards["unit-3"];

        var printed = Payment.CostOf(card, fromHidden: false, accelerate: false);
        var hidden = Payment.CostOf(card, fromHidden: true, accelerate: true);

        Assert.Equal(3, printed.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, printed.Power);
        Assert.Equal(1, hidden.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, hidden.Power);
    }

    [Fact]
    public void Adjusting_never_goes_below_zero_energy()
    {
        var adjusted = Payment.Adjust(new TotalCost(2, [PowerSymbol.Fury]), -5, [PowerSymbol.Any], [PowerSymbol.Fury]);

        Assert.Equal(0, adjusted.Energy);
        Assert.Equal(new[] { PowerSymbol.Any }, adjusted.Power);
    }

    [Fact]
    public void Specific_domains_are_matched_before_self_and_any()
    {
        var pool = Pool(power: [(Domain.Fury, 1), (Domain.Chaos, 1)]);

        Assert.True(Payment.TryPay(pool, new TotalCost(0, [PowerSymbol.Self, PowerSymbol.Fury]), FuryChaos));
        Assert.True(pool.IsEmpty);
    }

    [Fact]
    public void A_failed_payment_leaves_the_pool_untouched()
    {
        var pool = Pool(energy: 1, power: [(Domain.Calm, 1)]);

        Assert.False(Payment.TryPay(pool, new TotalCost(1, [PowerSymbol.Fury]), FuryChaos));
        Assert.Equal(1, pool.Energy);
        Assert.Equal(1, pool.Power[Domain.Calm]);
    }

    [Fact]
    public void Universal_power_pays_any_symbol_and_any_takes_any_domain()
    {
        var pool = Pool(universal: 1, power: [(Domain.Calm, 1)]);

        Assert.True(Payment.TryPay(pool, new TotalCost(0, [PowerSymbol.Fury, PowerSymbol.Any]), FuryChaos));
        Assert.True(pool.IsEmpty);
    }

    [Fact]
    public void Suggestion_exhausts_for_energy_and_recycles_matching_runes_preferring_exhausted_ones()
    {
        RuneInfo[] runes =
        [
            new(new(1), Domain.Fury, false), new(new(2), Domain.Fury, false),
            new(new(3), Domain.Chaos, false), new(new(4), Domain.Fury, false),
        ];

        var suggestion = Payment.Suggest(new RunePool(), new TotalCost(3, [PowerSymbol.Chaos]), FuryChaos, runes)!;

        Assert.Equal(new[] { new ObjectId(1), new ObjectId(2), new ObjectId(3) }, suggestion.Exhaust);
        Assert.Equal(new[] { new ObjectId(3) }, suggestion.Recycle);
    }

    [Fact]
    public void Suggestion_uses_the_pool_first_and_is_null_when_impossible()
    {
        RuneInfo[] runes = [new(new(1), Domain.Fury, false)];

        var fromPool = Payment.Suggest(Pool(energy: 2), new TotalCost(2, []), FuryChaos, runes)!;

        Assert.Empty(fromPool.Exhaust);
        Assert.Null(Payment.Suggest(new RunePool(), new TotalCost(1, [PowerSymbol.Calm]), FuryChaos, runes));
    }
}
