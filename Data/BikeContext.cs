using Microsoft.EntityFrameworkCore;
using MRP.Api.Models;

namespace MRP.Api.Data;

public class BikeContext : DbContext
{
    public BikeContext(DbContextOptions<BikeContext> options)
        : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Bom> Boms => Set<Bom>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>()
            .Property(i => i.ItemType)
            .HasConversion<string>();

        modelBuilder.Entity<Item>()
            .Property(i => i.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ParentItem)
            .WithMany()
            .HasForeignKey(b => b.ParentItemID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Bom>()
            .HasOne(b => b.ChildItem)
            .WithMany()
            .HasForeignKey(b => b.ChildItemID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}