using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>The server's SQLite database. Its schema comes from the migrations in <c>Storage/Migrations</c>.</summary>
internal sealed class CromoDbContext(DbContextOptions<CromoDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<UserEntity>();
        user.ToTable("Users");
        user.Property(u => u.UserName).HasMaxLength(24);
        user.Property(u => u.NormalizedUserName).HasMaxLength(24);
        user.HasIndex(u => u.NormalizedUserName).IsUnique();

        var match = model.Entity<MatchEntity>();
        match.ToTable("Matches");
        match.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
        match.HasIndex(m => m.Status);
        match.HasOne<UserEntity>().WithMany().HasForeignKey(m => m.Seat0UserId).OnDelete(DeleteBehavior.Restrict);
        match.HasOne<UserEntity>().WithMany().HasForeignKey(m => m.Seat1UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
