namespace CromoBound.Models.Json;

/// <summary>Serialize this collection even when it is empty (an empty list means something different from a missing one).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeepEmptyAttribute : Attribute;
