using System;
using MRP.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MRP_Bikev2.Migrations
{
    [DbContext(typeof(BikeContext))]
    [Migration("20260329105310_AddItemSellingPrice")]
    partial class AddItemSellingPrice
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("MRP.Api.Models.Bom", b =>
                {
                    b.Property<int>("BOMID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("BOMID"));

                    b.Property<int>("ChildItemID")
                        .HasColumnType("int");

                    b.Property<int>("ParentItemID")
                        .HasColumnType("int");

                    b.Property<decimal>("Quantity")
                        .HasColumnType("decimal(10,2)");

                    b.HasKey("BOMID");

                    b.HasIndex("ChildItemID");

                    b.HasIndex("ParentItemID", "ChildItemID")
                        .IsUnique();

                    b.ToTable("Boms");
                });

            modelBuilder.Entity("MRP.Api.Models.Item", b =>
                {
                    b.Property<int>("ItemID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ItemID"));

                    b.Property<string>("ItemCode")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("ItemName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("ItemType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<decimal?>("SellingPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Unit")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("UnitCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("ItemID");

                    b.HasIndex("ItemCode")
                        .IsUnique()
                        .HasFilter("[ItemCode] IS NOT NULL");

                    b.ToTable("Items");
                });

            modelBuilder.Entity("MRP.Api.Models.StockOperation", b =>
                {
                    b.Property<int>("StockOperationID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("StockOperationID"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<string>("OperationType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("Quantity")
                        .HasPrecision(18, 4)
                        .HasColumnType("decimal(18,4)");

                    b.Property<int>("SpecificationId")
                        .HasColumnType("int");

                    b.HasKey("StockOperationID");

                    b.HasIndex("SpecificationId");

                    b.ToTable("StockOperations");
                });

            modelBuilder.Entity("MRP.Api.Models.Bom", b =>
                {
                    b.HasOne("MRP.Api.Models.Item", "ChildItem")
                        .WithMany()
                        .HasForeignKey("ChildItemID")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.HasOne("MRP.Api.Models.Item", "ParentItem")
                        .WithMany()
                        .HasForeignKey("ParentItemID")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.Navigation("ChildItem");

                    b.Navigation("ParentItem");
                });

            modelBuilder.Entity("MRP.Api.Models.StockOperation", b =>
                {
                    b.HasOne("MRP.Api.Models.Bom", "Specification")
                        .WithMany()
                        .HasForeignKey("SpecificationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Specification");
                });
#pragma warning restore 612, 618
        }
    }
}
