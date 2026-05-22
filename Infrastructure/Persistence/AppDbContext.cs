using Microsoft.EntityFrameworkCore;
using JoyTopBackend.Domain.Entities;

namespace JoyTopBackend.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Place> Places => Set<Place>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PlaceRating> PlaceRatings => Set<PlaceRating>();
    public DbSet<PlaceVote> PlaceVotes => Set<PlaceVote>();
    public DbSet<PlaceLike> PlaceLikes => Set<PlaceLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Place>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        // SQLite doesn't support List<string> natively, so we use a value converter
        modelBuilder.Entity<Place>()
            .Property(p => p.Images)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}
