using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Data;

public sealed record ValidationIssue(string Location, string Message);

public static class EffectsValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(CardDatabase db)
    {
        var issues = new List<ValidationIssue>();
        foreach (var printing in db.Printings.Values)
            if (!db.Cards.ContainsKey(printing.CardId))
                issues.Add(new ValidationIssue($"printings.json {printing.Id}", $"cardId '{printing.CardId}' does not exist."));
        foreach (var card in db.Cards.Values)
            if (card.DefaultPrintingId is { } printingId && !db.Printings.ContainsKey(printingId))
                issues.Add(new ValidationIssue($"cards.json {card.Id}", $"defaultPrintingId '{printingId}' does not exist."));
        foreach (var effects in db.Effects.Values)
            new FileValidator(db, effects, issues).Run();
        return issues;
    }
}

internal sealed class FileValidator(CardDatabase db, LoadedEffects loaded, List<ValidationIssue> issues)
{
    private readonly string _fileName = Path.GetFileName(loaded.FilePath);
    private readonly HashSet<string> _costIds = new(StringComparer.Ordinal);
    private int _lineCount;

    private EffectsFile File => loaded.File;

    public void Run()
    {
        if (!string.Equals(_fileName, File.CardId + ".json", StringComparison.Ordinal))
            Error("", $"file name must be '{File.CardId}.json'.");
        if (!db.Cards.TryGetValue(File.CardId, out var card))
        {
            Error("", $"cardId '{File.CardId}' does not exist.");
            return;
        }
        _lineCount = RichText.Lines(card.Text.Rich).Count;

        foreach (var cost in File.AdditionalCosts)
            if (!_costIds.Add(cost.Id))
                Error("additionalCosts", $"additional cost id '{cost.Id}' is duplicated.");

        var baseScope = new HashSet<string>(StringComparer.Ordinal);
        WalkSteps(File.AsYouPlay, baseScope, "asYouPlay");

        for (var i = 0; i < File.AdditionalCosts.Count; i++)
        {
            var cost = File.AdditionalCosts[i];
            var at = $"additionalCosts[{i}]";
            WalkCost(cost.Cost, Copy(baseScope), at + ".cost");
            if (cost.ModifiesCost is not null) CheckNode(cost.ModifiesCost, baseScope, at + ".modifiesCost");
            WalkSteps(cost.OnPaid, Copy(baseScope), at + ".onPaid");
        }

        for (var i = 0; i < File.Keywords.Count; i++)
        {
            var keyword = File.Keywords[i];
            WalkCost(keyword.Cost, Copy(baseScope), $"keywords[{i}].cost");
            WalkSteps(keyword.Steps, Copy(baseScope), $"keywords[{i}].steps");
        }

        for (var i = 0; i < File.Abilities.Count; i++)
            WalkAbility(File.Abilities[i], Copy(baseScope), $"abilities[{i}]");
    }

    private void WalkAbility(Ability ability, HashSet<string> scope, string at)
    {
        CheckNode(ability, scope, at);
        switch (ability)
        {
            case SpellAbility a:
                WalkSteps(a.Steps, scope, at + ".steps");
                break;
            case TriggeredAbility a:
                WalkCost(a.Cost, Copy(scope), at + ".cost");
                WalkSteps(a.Steps, scope, at + ".steps");
                break;
            case ActivatedAbility a:
                WalkCost(a.Cost, Copy(scope), at + ".cost");
                WalkSteps(a.Steps, scope, at + ".steps");
                break;
            case ReplacementAbility a:
                WalkSteps(a.With, scope, at + ".with");
                break;
        }
    }

    private void WalkCost(Cost? cost, HashSet<string> scope, string at)
    {
        if (cost is null) return;
        CheckNode(cost, scope, at);
        WalkSteps(cost.Actions, scope, at + ".actions");
    }

    private void WalkSteps(IReadOnlyList<Step> steps, HashSet<string> scope, string path)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var at = $"{path}[{i}]";
            CheckNode(step, scope, at);
            switch (step)
            {
                case OptionalStep s:
                    WalkCost(s.Cost, Copy(scope), at + ".cost");
                    WalkSteps(s.Steps, Copy(scope), at + ".steps");
                    break;
                case IfStep s:
                    WalkSteps(s.Then, Copy(scope), at + ".then");
                    WalkSteps(s.Else, Copy(scope), at + ".else");
                    break;
                case ForEachStep s:
                    var inner = Copy(scope);
                    if (s.As is not null) inner.Add(s.As);
                    WalkSteps(s.Steps, inner, at + ".steps");
                    break;
                case RepeatStep s:
                    WalkSteps(s.Steps, Copy(scope), at + ".steps");
                    break;
                case ChooseOneStep s:
                    for (var m = 0; m < s.Modes.Count; m++) WalkSteps(s.Modes[m].Steps, Copy(scope), $"{at}.modes[{m}]");
                    break;
                case ChooseNStep s:
                    for (var m = 0; m < s.Modes.Count; m++) WalkSteps(s.Modes[m].Steps, Copy(scope), $"{at}.modes[{m}]");
                    break;
                case CreateDelayedStep s:
                    WalkAbility(s.Ability, Copy(scope), at + ".ability");
                    break;
                case PayStep s:
                    WalkCost(s.Cost, Copy(scope), at + ".cost");
                    break;
                case PlayTokenStep s:
                    if (!db.Cards.TryGetValue(s.Token, out var token) || token.Supertype != Supertype.Token)
                        Error(at, $"token '{s.Token}' is not a token card.");
                    break;
                case ScriptStep s when string.IsNullOrWhiteSpace(s.Script):
                    Error(at, "a Script step needs a 'script' handler name.");
                    break;
            }
            if (step.Store is not null) scope.Add(step.Store);
        }
    }

    // Checks references inside one node. Nested steps, abilities and costs are walked separately, in order.
    private void CheckNode(object node, HashSet<string> scope, string at)
    {
        foreach (var child in ChildValues(node)) Visit(child, scope, at);
    }

    private void Visit(object? value, HashSet<string> scope, string at)
    {
        switch (value)
        {
            case null or string or JsonNode:
                return;
            case Step or Ability or Cost or IEnumerable<Step>:
                return;
            case LineRef line:
                foreach (var number in line.Lines)
                    if (number > _lineCount)
                        Error(at, $"line {number} is out of range; the card text has {_lineCount} line(s).");
                return;
            case ObjectRef { Var: { } name }:
                RequireVar(name, scope, at);
                break;
            case PlayerRef { Var: { } name }:
                RequireVar(name, scope, at);
                break;
            case Value { Var: { } name }:
                RequireVar(name, scope, at);
                break;
            case Condition condition:
                if (condition.Did is { } did) RequireVar(did, scope, at);
                if (condition.Paid is { } paid && !_costIds.Contains(paid))
                    Error(at, $"'paid' refers to unknown additional cost '{paid}'.");
                break;
        }

        if (value is IEnumerable items)
        {
            foreach (var item in items) Visit(item, scope, at);
            return;
        }
        var type = value.GetType();
        if (!type.IsEnum && type.Namespace?.StartsWith("CromoBound.Models", StringComparison.Ordinal) == true)
            foreach (var child in ChildValues(value)) Visit(child, scope, at);
    }

    private static IEnumerable<object?> ChildValues(object node) =>
        node.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.GetValue(node));

    private void RequireVar(string name, HashSet<string> scope, string at)
    {
        if (!scope.Contains(name)) Error(at, $"variable '{name}' is used before it is stored.");
    }

    private static HashSet<string> Copy(HashSet<string> scope) => new(scope, scope.Comparer);

    private void Error(string at, string message) =>
        issues.Add(new ValidationIssue(at.Length == 0 ? _fileName : $"{_fileName} {at}", message));
}
