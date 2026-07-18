using Microsoft.EntityFrameworkCore;
using OrderModel = PRM.Services.Order.Models.Order;
using PRM.Services.Order.Models;
using PRM.Services.Order.Models.Enums;

namespace PRM.Services.Order.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<OrderModel> Orders => Set<OrderModel>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Order — NO FK to external tables (TableId, HandledBy are logical refs)
        modelBuilder.Entity<OrderModel>(e =>
        {
            e.HasKey(o => o.OrderId);
            e.Property(o => o.Status).HasDefaultValue(1);
            e.Property(o => o.TotalAmount).HasColumnType("numeric(18,2)");
            e.Property(o => o.CreatedAt).HasDefaultValueSql("NOW()");
            // No HasForeignKey for TableId or HandledBy
        });

        // OrderItem — FK to Order (same DB), NO FK to MenuItem (cross-service)
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(oi => oi.OrderItemId);
            e.Property(oi => oi.UnitPrice).HasColumnType("numeric(18,2)");
            e.Property(oi => oi.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(oi => oi.Status).HasConversion<int>(); // Default set in application code (Pending=1)
            e.HasOne(oi => oi.Order)
             .WithMany(o => o.OrderItems)
             .HasForeignKey(oi => oi.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            // MenuItemId is a plain int column — no FK constraint
        });

        // Payment — FK to Order (same DB)
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(p => p.PaymentId);
            e.Property(p => p.Amount).HasColumnType("numeric(18,2)");
            e.Property(p => p.Method).HasConversion<int>();
            e.Property(p => p.Status).HasConversion<int>(); // Default set in application code (Pending=1)
            e.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasOne(p => p.Order)
             .WithOne(o => o.Payment)
             .HasForeignKey<Payment>(p => p.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            // Unique: 1 payment per order
            e.HasIndex(p => p.OrderId).IsUnique();
        });

        // Feedback — FK to Order (same DB), UNIQUE per OrderId (anti-spam)
        modelBuilder.Entity<Feedback>(e =>
        {
            e.HasKey(f => f.FeedbackId);
            e.Property(f => f.IsHidden).HasDefaultValue(false);
            e.Property(f => f.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasOne(f => f.Order)
             .WithMany(o => o.Feedbacks)
             .HasForeignKey(f => f.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            // UNIQUE constraint: only 1 feedback per order
            e.HasIndex(f => f.OrderId).IsUnique();
            // TableId is plain int — no FK to Restaurant DB
        });
    }
}
