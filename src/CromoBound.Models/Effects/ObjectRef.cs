using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>Either a fixed reference (<c>ref</c>), a stored variable (<c>var</c>) or a selector (<c>select</c>).</summary>
public sealed record ObjectRef : IJsonOnDeserialized
{
    public RefKind? Ref { get; init; }
    public string? Var { get; init; }
    public SelectKind? Select { get; init; }
    public int? Count { get; init; }
    public int? UpTo { get; init; }
    public bool? All { get; init; }
    public Filter? Filter { get; init; }

    public static ObjectRef Self { get; } = new() { Ref = RefKind.Self };

    public static ObjectRef Variable(string name) => new() { Var = name };

    void IJsonOnDeserialized.OnDeserialized()
    {
        var forms = (Ref is null ? 0 : 1) + (Var is null ? 0 : 1) + (Select is null ? 0 : 1);
        if (forms != 1)
            throw new JsonException("An object reference needs exactly one of 'ref', 'var' or 'select'.");
        var quantities = (Count is null ? 0 : 1) + (UpTo is null ? 0 : 1) + (All is true ? 1 : 0);
        if (Select is null && (quantities > 0 || Filter is not null))
            throw new JsonException("'count', 'upTo', 'all' and 'filter' are only valid together with 'select'.");
        if (quantities > 1)
            throw new JsonException("A selector takes at most one of 'count', 'upTo' or 'all'.");
    }
}
