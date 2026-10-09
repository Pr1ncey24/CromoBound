using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>The server's SQLite database.</summary>
internal sealed class CromoDbContext(DbContextOptions<CromoDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<UserEntity>();
        user.ToTable("Users");
        user.Property(u => u.UserName).HasMaxLength(24);
        user.Property(u => u.NormalizedUserName).HasMaxLength(24);
        user.HasIndex(u => u.NormalizedUserName).IsUnique();
    }
}
