using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

public partial class AddInventoryPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Presentation",
            table: "InventoryItems",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Presentation",
            table: "InventoryItems");
    }
}
