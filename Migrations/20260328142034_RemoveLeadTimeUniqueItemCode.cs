using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MRP_Bikev2.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLeadTimeUniqueItemCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemCode",
                table: "Items",
                column: "ItemCode",
                unique: true,
                filter: "[ItemCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_ItemCode",
                table: "Items");

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "Items",
                type: "int",
                nullable: true);
        }
    }
}
