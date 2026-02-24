using Microsoft.EntityFrameworkCore;

namespace Cultiv.EnvironmentInspect.Data;

internal class EnvironmentInspectDbContext : DbContext
{
    public EnvironmentInspectDbContext(DbContextOptions<EnvironmentInspectDbContext> options) 
        : base(options)
    {
    }

    public DbSet<UserPreference> UserPreferences { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure the UserPreference entity
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("CultivEnvironmentInspectUserPreferences");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.UserKey, e.SettingKey }).IsUnique();
            entity.Property(e => e.UserKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(250);
            entity.Property(e => e.IsStarred).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Property(e => e.ModifiedDate).IsRequired();
        });
    }
}
