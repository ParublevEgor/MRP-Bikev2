using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MRP_Bikev2.Migrations
{
    /// <inheritdoc />
    public partial class AddStockOperationItemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "StockOperations",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.ItemId = b.ChildItemID
                FROM StockOperations s
                INNER JOIN Boms b ON s.SpecificationId = b.BOMID
                WHERE s.ItemId IS NULL
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "StockOperations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_ItemId",
                table: "StockOperations",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockOperations_Items_ItemId",
                table: "StockOperations",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "ItemID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Orders] (
                        [OrderID] int NOT NULL IDENTITY,
                        [OrderDate] datetime2 NOT NULL,
                        [DueDate] datetime2 NOT NULL,
                        [Status] nvarchar(20) NOT NULL,
                        CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderID])
                    );
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[OrderLines]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [OrderLines] (
                        [OrderLineID] int NOT NULL IDENTITY,
                        [OrderID] int NOT NULL,
                        [ItemID] int NOT NULL,
                        [Quantity] int NOT NULL,
                        CONSTRAINT [PK_OrderLines] PRIMARY KEY ([OrderLineID]),
                        CONSTRAINT [FK_OrderLines_Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [Items] ([ItemID]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_OrderLines_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_OrderLines_ItemID] ON [OrderLines] ([ItemID]);
                    CREATE INDEX [IX_OrderLines_OrderID] ON [OrderLines] ([OrderID]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockOperations_Items_ItemId",
                table: "StockOperations");

            migrationBuilder.DropIndex(
                name: "IX_StockOperations_ItemId",
                table: "StockOperations");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "StockOperations");
        }
    }
}
