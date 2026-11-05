using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuByArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "MenuProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuProducts_AreaId",
                table: "MenuProducts",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuProducts_Areas_AreaId",
                table: "MenuProducts",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuProducts_Areas_AreaId",
                table: "MenuProducts");

            migrationBuilder.DropIndex(
                name: "IX_MenuProducts_AreaId",
                table: "MenuProducts");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "MenuProducts");
        }
    }
}
