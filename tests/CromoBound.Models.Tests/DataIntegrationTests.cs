using CromoBound.Data;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

public class DataIntegrationTests
{
    private static readonly Lazy<CardDatabase> Db = new(() => CardRepository.Load(RepoPaths.Data));

    private static readonly string[] SampleIds =
    [
        "vanguard-sergeant", "fury-rune", "progress-day", "falling-star", "vengeance", "mystic-poro",
        "rengar-trophy-hunter", "soaring-scout", "shadow-temple", "garbage-grabber", "noxus-hopeful", "kharox",
    ];

    private static EffectsFile Effects(string cardId) => Db.Value.Effects[cardId].File;

    [Fact]
    public void Repository_data_has_no_validation_issues()
    {
        var issues = EffectsValidator.Validate(Db.Value);
        Assert.True(issues.Count == 0, string.Join("\n", issues.Select(i => $"{i.Location}: {i.Message}")));
    }

    [Fact]
    public void Every_effects_file_round_trips_stably()
    {
        var unstable = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(RepoPaths.Data, "effects"), "*.json"))
        {
            var once = CromoJson.Serialize(CromoJson.Deserialize<EffectsFile>(File.ReadAllText(path)));
            var twice = CromoJson.Serialize(CromoJson.Deserialize<EffectsFile>(once));
            if (once != twice) unstable.Add(Path.GetFileName(path));
        }
        Assert.Empty(unstable);
    }

    [Fact]
    public void Every_sample_card_is_fully_mapped()
    {
        foreach (var id in SampleIds)
            Assert.True(Db.Value.StatusOf(id) == MappingStatus.Full, $"{id} is not Full");
    }

    [Fact]
    public void Vanguard_sergeant_is_scaffolded_vanilla()
    {
        var file = Effects("vanguard-sergeant");
        Assert.Empty(file.Keywords);
        Assert.Empty(file.Abilities);
        Assert.Equal(4, Db.Value.Cards["vanguard-sergeant"].Might);
    }

    [Fact]
    public void Fury_rune_has_two_reaction_add_abilities()
    {
        var abilities = Effects("fury-rune").Abilities.Cast<ActivatedAbility>().ToList();

        Assert.Equal(2, abilities.Count);
        Assert.All(abilities, a => Assert.Equal(Timing.Reaction, a.Timing));
        Assert.True(abilities[0].Cost!.ExhaustSelf);
        Assert.Equal(1, Assert.IsType<AddStep>(Assert.Single(abilities[0].Steps)).Energy!.Literal);
        Assert.Equal(RefKind.Self, Assert.IsType<RecycleStep>(Assert.Single(abilities[1].Cost!.Actions)).Target!.Ref);
        Assert.Equal(new[] { PowerSymbol.Fury }, Assert.IsType<AddStep>(Assert.Single(abilities[1].Steps)).Power);
    }

    [Fact]
    public void Progress_day_draws_four()
    {
        var spell = Assert.IsType<SpellAbility>(Assert.Single(Effects("progress-day").Abilities));
        Assert.Equal(4, Assert.IsType<DrawStep>(Assert.Single(spell.Steps)).Amount.Literal);
    }

    [Fact]
    public void Falling_star_is_one_spell_with_two_targeted_deals()
    {
        var spell = Assert.IsType<SpellAbility>(Assert.Single(Effects("falling-star").Abilities));

        Assert.Equal(new[] { 1, 2 }, spell.Line!.Lines);
        Assert.Equal(2, spell.Steps.Count);
        Assert.All(spell.Steps, step =>
        {
            var deal = Assert.IsType<DealStep>(step);
            Assert.Equal(3, deal.Amount.Literal);
            Assert.Equal(SelectKind.Unit, deal.Target.Select);
            Assert.Equal(1, deal.Target.Count);
        });
    }

    [Fact]
    public void Vengeance_kills_one_unit()
    {
        var spell = Assert.IsType<SpellAbility>(Assert.Single(Effects("vengeance").Abilities));
        var kill = Assert.IsType<KillStep>(Assert.Single(spell.Steps));
        Assert.Equal(SelectKind.Unit, kill.Target.Select);
    }

    [Fact]
    public void Mystic_poro_is_scaffolded_with_vision() =>
        Assert.Equal(MechanicalKeyword.Vision, Assert.Single(Effects("mystic-poro").Keywords).Keyword);

    [Fact]
    public void Rengar_has_ambush_and_battlefield_permission()
    {
        var file = Effects("rengar-trophy-hunter");

        Assert.Equal(MechanicalKeyword.Ambush, Assert.Single(file.Keywords).Keyword);
        var passive = Assert.IsType<PassiveAbility>(Assert.Single(file.Abilities));
        var permission = Assert.IsType<PermissionModifier>(Assert.Single(passive.Modifiers));
        Assert.Equal(Permission.PlayToBattlefieldWithEnemyUnits, permission.Permission);
    }

    [Fact]
    public void Soaring_scout_deathknell_channels_exhausted()
    {
        var deathknell = Assert.Single(Effects("soaring-scout").Keywords);
        Assert.Equal(MechanicalKeyword.Deathknell, deathknell.Keyword);
        var channel = Assert.IsType<ChannelStep>(Assert.Single(deathknell.Steps));
        Assert.True(channel.Exhausted);
    }

    [Fact]
    public void Shadow_temple_burns_three_on_hold()
    {
        var trigger = Assert.IsType<TriggeredAbility>(Assert.Single(Effects("shadow-temple").Abilities));

        Assert.Equal(TriggerEvent.Hold, trigger.Trigger.Event);
        Assert.Equal(RefKind.Here, trigger.Trigger.Where!.Ref);
        Assert.Equal(3, Assert.IsType<BurnStep>(Assert.Single(trigger.Steps)).Amount.Literal);
    }

    [Fact]
    public void Garbage_grabber_activated_ability_has_combined_cost()
    {
        var ability = Assert.IsType<ActivatedAbility>(Assert.Single(Effects("garbage-grabber").Abilities));

        Assert.Equal(1, ability.Cost!.Energy);
        Assert.True(ability.Cost.ExhaustSelf);
        Assert.Equal(3, Assert.IsType<RecycleStep>(Assert.Single(ability.Cost.Actions)).Count!.Literal);
        Assert.IsType<DrawStep>(Assert.Single(ability.Steps));
    }

    [Fact]
    public void Noxus_hopeful_costs_two_less_with_legion()
    {
        var passive = Assert.IsType<PassiveAbility>(Assert.Single(Effects("noxus-hopeful").Abilities));

        Assert.True(passive.Condition!.Legion);
        Assert.Equal(2, Assert.IsType<CostReductionModifier>(Assert.Single(passive.Modifiers)).Energy!.Literal);
    }

    [Fact]
    public void Kharox_matches_the_documented_structure()
    {
        var file = Effects("kharox");

        Assert.Equal(MechanicalKeyword.Empower, Assert.Single(file.Keywords).Keyword);
        var trigger = Assert.IsType<TriggeredAbility>(Assert.Single(file.Abilities));
        Assert.Equal(TriggerEvent.BecameEmpowered, trigger.Trigger.Event);
        Assert.Equal(3, trigger.Steps.Count);
        var optional = Assert.IsType<OptionalStep>(trigger.Steps[2]);
        Assert.True(optional.Reflexive);
        Assert.Equal(PlayCostMode.IgnoreAll, Assert.IsType<PlayStep>(optional.Steps[1]).Cost);
    }
}
