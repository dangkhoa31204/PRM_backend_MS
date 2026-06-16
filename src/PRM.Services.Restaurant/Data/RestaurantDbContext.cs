using Microsoft.EntityFrameworkCore;
using PRM.Services.Restaurant.Models;
using PRM.Shared.Enums;

namespace PRM.Services.Restaurant.Data;

public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options) { }

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Table> Tables => Set<Table>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(e =>
        {
            e.HasKey(m => m.MenuItemId);
            e.Property(m => m.Name).HasMaxLength(200).IsRequired();
            e.Property(m => m.Price).HasColumnType("numeric(18,2)");
            e.Property(m => m.Category).HasConversion<int>();
            e.Property(m => m.IsAvailable).HasDefaultValue(true);
            e.Property(m => m.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Table>(e =>
        {
            e.HasKey(t => t.TableId);
            e.Property(t => t.Status).HasDefaultValue(1); // Available
            e.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
