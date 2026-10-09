using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class UserStoreTests
{
    [Theory]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("Player_One-2", true)]
    [InlineData("has space", false)]
    [InlineData("way-too-long-for-a-username", false)]
    [InlineData("café", false)]
    public void Usernames_have_3_to_24_letters_digits_underscores_or_dashes(string userName, bool valid) =>
        Assert.Equal(valid, UserStore.UserNameProblem(userName) is null);

    [Fact]
    public void Passwords_need_at_least_12_characters()
    {
        Assert.NotNull(UserStore.PasswordProblem("eleven-char"));
        Assert.NotNull(UserStore.PasswordProblem(null));
        Assert.Null(UserStore.PasswordProblem("twelve-chars"));
    }

    [Fact]
    public async Task Names_are_unique_whatever_their_case()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("Player1");

        await factory.WithStoreAsync(async store =>
        {
            var (user, error) = await store.CreateAsync("PLAYER1", ServerFactory.PlayerPassword, isAdmin: false);

            Assert.Null(user);
            Assert.Equal("That username is taken.", error);
            Assert.Equal("Player1", (await store.FindAsync("player1"))?.UserName);
        });
    }

    [Fact]
    public async Task A_password_change_replaces_the_hash_and_the_security_stamp()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");

        await factory.WithStoreAsync(async store =>
        {
            var user = (await store.FindAsync("player1"))!;
            var (hash, stamp) = (user.PasswordHash, user.SecurityStamp);

            await store.SetPasswordAsync(user, "a-brand-new-password");

            Assert.NotEqual(hash, user.PasswordHash);
            Assert.NotEqual(stamp, user.SecurityStamp);
            Assert.True(await store.VerifyAsync(user, "a-brand-new-password"));
            Assert.False(await store.VerifyAsync(user, ServerFactory.PlayerPassword));
        });
    }
}
