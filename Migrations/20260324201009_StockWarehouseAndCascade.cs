using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MRP_Bikev2.Migrations
{
    public partial class StockWarehouseAndCascade : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    ItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ItemID);
                });

            migrationBuilder.CreateTable(
                name: "Boms",
                columns: table => new
                {
                    BOMID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentItemID = table.Column<int>(type: "int", nullable: false),
                    ChildItemID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boms", x => x.BOMID);
                    table.ForeignKey(
                        name: "FK_Boms_Items_ChildItemID",
                        column: x => x.ChildItemID,
                        principalTable: "Items",
                        principalColumn: "ItemID");
                    table.ForeignKey(
                        name: "FK_Boms_Items_ParentItemID",
                        column: x => x.ParentItemID,
                        principalTable: "Items",
                        principalColumn: "ItemID");
                });

            migrationBuilder.CreateTable(
                name: "StockOperations",
                columns: table => new
                {
                    StockOperationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecificationId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockOperations", x => x.StockOperationID);
                    table.ForeignKey(
                        name: "FK_StockOperations_Boms_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Boms",
                        principalColumn: "BOMID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boms_ChildItemID",
                table: "Boms",
                column: "ChildItemID");

            migrationBuilder.CreateIndex(
                name: "IX_Boms_ParentItemID_ChildItemID",
                table: "Boms",
                columns: new[] { "ParentItemID", "ChildItemID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_SpecificationId",
                table: "StockOperations",
                column: "SpecificationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockOperations");

            migrationBuilder.DropTable(
                name: "Boms");

            migrationBuilder.DropTable(
                name: "Items");
        }
    }
}
