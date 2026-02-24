using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cultiv.EnvironmentInspect.Data;

/// <summary>
/// Factory for creating the DbContext at design time (for migrations).
/// This is only used by EF Core tools and not at runtime.
/// </summary>
internal class EnvironmentInspectDbContextFactory : IDesignTimeDbContextFactory<EnvironmentInspectDbContext>
{
    public EnvironmentInspectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EnvironmentInspectDbContext>();
        
        // Use SQLite for design-time/migrations (this won't be used at runtime)
        optionsBuilder.UseSqlite("Data Source=environmentinspect.db");
        
        return new EnvironmentInspectDbContext(optionsBuilder.Options);
    }
}
