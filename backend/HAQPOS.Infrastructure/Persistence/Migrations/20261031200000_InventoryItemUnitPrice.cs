using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

public partial class InventoryItemUnitPrice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "UnitPrice",
            table: "InventoryItems",
            type: "numeric(12,2)",
            precision: 12,
            scale: 2,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_InventoryItems_UnitPrice",
            table: "InventoryItems",
            sql: "\"UnitPrice\" IS NULL OR (\"UnitPrice\" > 0 AND \"UnitPrice\" <= 999999.99)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_InventoryItems_UnitPrice",
            table: "InventoryItems");

        migrationBuilder.DropColumn(name: "UnitPrice", table: "InventoryItems");
    }
}
