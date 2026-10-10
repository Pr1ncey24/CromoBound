namespace CromoBound.Client.Tests;

/// <summary>Deck JSON as a player pastes it. The client doesn't check legality, so made-up ids are fine.</summary>
internal static class SampleDecks
{
    public static string Json(string name = "Jinx aggro", int main = 40, int sideboard = 0)
    {
        var side = sideboard > 0 ? $$""", "sideboard": [ { "printing": "unit-2", "count": {{sideboard}} } ]""" : "";
        return $$"""
            {
              "name": "{{name}}",
              "legend": "legend-1",
              "champion": "champion-1",
              "main": [ { "printing": "unit-1", "count": {{main}} } ],
              "runes": [ { "printing": "rune-1", "count": 12 } ],
              "battlefields": [ "bf-1", "bf-2", "bf-3" ]{{side}}
            }
            """;
    }
}
