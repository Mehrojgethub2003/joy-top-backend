using Microsoft.EntityFrameworkCore;
using JoyTopBackend.Domain.Entities;

namespace JoyTopBackend.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Place> Places => Set<Place>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // SQLite doesn't support List<string> natively, so we use a value converter
        modelBuilder.Entity<Place>()
            .Property(p => p.Images)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}
