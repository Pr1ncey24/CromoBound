namespace CromoBound.Server.Storage;

/// <summary>An account (spec §5.1). <see cref="SecurityStamp"/> changes whenever the password, the role or the disabled flag
/// changes; sessions carrying an older stamp end.</summary>
internal sealed class UserEntity
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public required string NormalizedUserName { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool Disabled { get; set; }
    public required string SecurityStamp { get; set; }
    public DateTime CreatedAt { get; set; }
}
