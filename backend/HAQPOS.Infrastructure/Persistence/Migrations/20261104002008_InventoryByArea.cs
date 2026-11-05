using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryByArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "Areas" ("Id", "Name", "NormalizedName", "Description", "CreatedById", "CreatedAt", "Version")
                SELECT gen_random_uuid(), 'Área General', 'ÁREA GENERAL',
                       'Área inicial para insumos registrados antes de separar inventarios por área.',
                       (SELECT "Id" FROM "Users" ORDER BY "CreatedAt" LIMIT 1), CURRENT_TIMESTAMP, gen_random_uuid()
                WHERE EXISTS (SELECT 1 FROM "InventoryItems")
                  AND NOT EXISTS (SELECT 1 FROM "Areas");

                UPDATE "InventoryItems"
                SET "AreaId" = (SELECT "Id" FROM "Areas" ORDER BY "CreatedAt", "Id" LIMIT 1)
                WHERE "AreaId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AreaId",
                table: "InventoryItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_Code",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_AreaId_Code",
                table: "InventoryItems",
                columns: new[] { "AreaId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Areas_AreaId",
                table: "InventoryItems",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Areas_AreaId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_AreaId_Code",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Code",
                table: "InventoryItems",
                column: "Code",
                unique: true);
        }
    }
}
