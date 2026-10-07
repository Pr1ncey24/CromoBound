using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(DrawStep), "Draw")]
[JsonDerivedType(typeof(DiscardStep), "Discard")]
[JsonDerivedType(typeof(BurnStep), "Burn")]
[JsonDerivedType(typeof(RecycleStep), "Recycle")]
[JsonDerivedType(typeof(BanishStep), "Banish")]
[JsonDerivedType(typeof(ReturnToHandStep), "ReturnToHand")]
[JsonDerivedType(typeof(RevealStep), "Reveal")]
[JsonDerivedType(typeof(LookAtStep), "LookAt")]
[JsonDerivedType(typeof(PredictStep), "Predict")]
[JsonDerivedType(typeof(CounterStep), "Counter")]
[JsonDerivedType(typeof(PlayStep), "Play")]
[JsonDerivedType(typeof(PlayTokenStep), "PlayToken")]
[JsonDerivedType(typeof(DealStep), "Deal")]
[JsonDerivedType(typeof(HealStep), "Heal")]
[JsonDerivedType(typeof(KillStep), "Kill")]
[JsonDerivedType(typeof(StunStep), "Stun")]
[JsonDerivedType(typeof(BuffStep), "Buff")]
[JsonDerivedType(typeof(SpendBuffStep), "SpendBuff")]
[JsonDerivedType(typeof(ReadyStep), "Ready")]
[JsonDerivedType(typeof(ExhaustStep), "Exhaust")]
[JsonDerivedType(typeof(MoveStep), "Move")]
[JsonDerivedType(typeof(RecallStep), "Recall")]
[JsonDerivedType(typeof(AttachStep), "Attach")]
[JsonDerivedType(typeof(DetachStep), "Detach")]
[JsonDerivedType(typeof(EmpowerStep), "Empower")]
[JsonDerivedType(typeof(GainControlStep), "GainControl")]
[JsonDerivedType(typeof(ModifyMightStep), "ModifyMight")]
[JsonDerivedType(typeof(GrantKeywordStep), "GrantKeyword")]
[JsonDerivedType(typeof(GrantTagStep), "GrantTag")]
[JsonDerivedType(typeof(AddStep), "Add")]
[JsonDerivedType(typeof(ChannelStep), "Channel")]
[JsonDerivedType(typeof(ScoreStep), "Score")]
[JsonDerivedType(typeof(GainXpStep), "GainXp")]
[JsonDerivedType(typeof(SpendXpStep), "SpendXp")]
[JsonDerivedType(typeof(PayStep), "Pay")]
[JsonDerivedType(typeof(ExtraTurnStep), "ExtraTurn")]
[JsonDerivedType(typeof(ChoosePlayerStep), "ChoosePlayer")]
[JsonDerivedType(typeof(ChooseCardStep), "ChooseCard")]
[JsonDerivedType(typeof(NameTagStep), "NameTag")]
[JsonDerivedType(typeof(NameCardStep), "NameCard")]
[JsonDerivedType(typeof(OptionalStep), "Optional")]
[JsonDerivedType(typeof(IfStep), "If")]
[JsonDerivedType(typeof(ForEachStep), "ForEach")]
[JsonDerivedType(typeof(RepeatStep), "Repeat")]
[JsonDerivedType(typeof(ChooseOneStep), "ChooseOne")]
[JsonDerivedType(typeof(ChooseNStep), "ChooseN")]
[JsonDerivedType(typeof(CreateDelayedStep), "CreateDelayed")]
[JsonDerivedType(typeof(ScriptStep), "Script")]
public abstract record Step
{
    /// <summary>Saves this step's result (chosen object/player/number) under a variable name.</summary>
    public string? Store { get; init; }

    /// <summary>C# fallback handler name ([CardScript]).</summary>
    public string? Script { get; init; }

    /// <summary>Who performs the step. Default: the ability's controller.</summary>
    public PlayerRef? Player { get; init; }

    /// <summary>Who makes the choice. Default: the ability's controller.</summary>
    public PlayerRef? Chooser { get; init; }
}

public abstract record TargetStep : Step
{
    public required ObjectRef Target { get; init; }
}
