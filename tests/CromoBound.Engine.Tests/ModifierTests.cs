using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ModifierTests
{
    [Fact]
    public void Daring_poro_has_assault_1_only_while_attacking()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("daring-poro"));
        var poro = game.Put("daring-poro", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(2, engine.MightOf(poro));
        game.State[poro].Role = CombatRole.Attacker;
        Assert.Equal(3, engine.MightOf(poro));
        game.State[poro].Role = CombatRole.Defender;
        Assert.Equal(2, engine.MightOf(poro));
    }

    [Fact]
    public void Mutated_mouser_has_shield_2_only_while_defending()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("mutated-mouser"));
        var mouser = game.Put("mutated-mouser", Place.Base(P1));
        var engine = game.Start();

        game.State[mouser].Role = CombatRole.Attacker;
        Assert.Equal(1, engine.MightOf(mouser));
        game.State[mouser].Role = CombatRole.Defender;
        Assert.Equal(3, engine.MightOf(mouser));
    }

    [Fact]
    public void Several_instances_stack_and_a_missing_value_counts_as_1()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", """
            { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Assault", "value": 2 }, { "keyword": "Assault" } ] }
            """)));
        var unit = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        game.State[unit].Role = CombatRole.Attacker;

        Assert.Equal(6, engine.MightOf(unit));
    }

    [Fact]
    public void Buffs_and_manual_modifiers_still_count()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("daring-poro"));
        var poro = game.Put("daring-poro", Place.Base(P1));
        var engine = game.Start();
        game.State[poro].Buffed = true;
        game.State[poro].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.State[poro].Role = CombatRole.Attacker;

        Assert.Equal(6, engine.MightOf(poro));
    }
}
