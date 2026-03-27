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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>()
            .Property(i => i.ItemType)
            .HasConversion<string>();

        modelBuilder.Entity<Item>()
            .Property(i => i.UnitCost)
            .HasPrecision(18, 2);

        // Два FK из Bom в Item: в SQL Server нельзя включить ON DELETE CASCADE на оба без конфликта путей.
        // ClientCascade — каскад на стороне EF при удалении Item (строки BOM и связанный склад удаляются порядком SaveChanges).
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
    }
}