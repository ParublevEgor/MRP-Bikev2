using Microsoft.EntityFrameworkCore;
using MRP.Api.Models;

namespace MRP.Api.Data;

public class BikeContext : DbContext
{
    public BikeContext(DbContextOptions<BikeContext> options)
        : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<StockOperation> StockOperations => Set<StockOperation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>()
            .Property(i => i.ItemType)
            .HasConversion<string>();

        modelBuilder.Entity<Item>()
            .Property(i => i.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Item>()
            .Property(i => i.SellingPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Item>()
            .HasIndex(i => i.ItemCode)
            .IsUnique()
            .HasFilter("[ItemCode] IS NOT NULL");

        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ParentItem)
            .WithMany()
            .HasForeignKey(b => b.ParentItemID)
            .OnDelete(DeleteBehavior.ClientCascade);

        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ChildItem)
            .WithMany()
            .HasForeignKey(b => b.ChildItemID)
            .OnDelete(DeleteBehavior.ClientCascade);

        modelBuilder.Entity<Bom>()
            .HasIndex(b => new { b.ParentItemID, b.ChildItemID })
            .IsUnique();

        modelBuilder.Entity<StockOperation>()
            .Property(s => s.OperationType)
            .HasConversion<string>();

        modelBuilder.Entity<StockOperation>()
            .Property(s => s.Quantity)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockOperation>()
            .HasOne(s => s.Specification)
            .WithMany()
            .HasForeignKey(s => s.SpecificationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderLine>()
            .HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}