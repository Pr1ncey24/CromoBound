using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CromoBound.Server.Storage;

/// <summary>Used only by <c>dotnet ef</c> to build migrations, without starting the server. It never opens the file.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CromoDbContext>
{
    public CromoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CromoDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
