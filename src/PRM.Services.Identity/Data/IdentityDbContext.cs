using Microsoft.EntityFrameworkCore;
using PRM.Services.Identity.Models;

namespace PRM.Services.Identity.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.AccountId);
            e.HasIndex(a => a.Username).IsUnique();
            e.HasIndex(a => a.Email).IsUnique();
            e.Property(a => a.Username).HasMaxLength(100).IsRequired();
            e.Property(a => a.Email).HasMaxLength(200).IsRequired();
            e.Property(a => a.PasswordHash).IsRequired();
            e.Property(a => a.FullName).HasMaxLength(200).IsRequired();
            e.Property(a => a.PhoneNumber).HasMaxLength(20);
            e.Property(a => a.IsActive).HasDefaultValue(true);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
