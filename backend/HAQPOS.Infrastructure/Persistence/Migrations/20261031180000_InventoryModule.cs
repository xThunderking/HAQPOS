using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

public partial class InventoryModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InventoryItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Location = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                CurrentStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                MinimumStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryItems", x => x.Id);
                table.CheckConstraint("CK_InventoryItems_CurrentStock", "\"CurrentStock\" BETWEEN 0 AND 1000000000");
                table.CheckConstraint("CK_InventoryItems_MinimumStock", "\"MinimumStock\" BETWEEN 0 AND 1000000000");
            });

        migrationBuilder.CreateTable(
            name: "InventoryMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                StockBefore = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                StockAfter = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                Reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                table.CheckConstraint("CK_InventoryMovements_Quantity", "\"Quantity\" <> 0");
                table.CheckConstraint("CK_InventoryMovements_Type", "\"Type\" IN ('Entrada', 'Salida', 'Ajuste')");
                table.CheckConstraint("CK_InventoryMovements_QuantityLimit", "abs(\"Quantity\") <= 1000000000");
                table.CheckConstraint("CK_InventoryMovements_StockBefore", "\"StockBefore\" BETWEEN 0 AND 1000000000");
                table.CheckConstraint("CK_InventoryMovements_StockAfter", "\"StockAfter\" BETWEEN 0 AND 1000000000");
                table.ForeignKey("FK_InventoryMovements_InventoryItems_ItemId", x => x.ItemId, "InventoryItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryMovements_Users_ActorId", x => x.ActorId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InventoryItems_Code", "InventoryItems", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_InventoryMovements_ActorId", "InventoryMovements", "ActorId");
        migrationBuilder.CreateIndex("IX_InventoryMovements_ItemId_OccurredAt", "InventoryMovements", new[] { "ItemId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InventoryMovements");
        migrationBuilder.DropTable("InventoryItems");
    }
}
