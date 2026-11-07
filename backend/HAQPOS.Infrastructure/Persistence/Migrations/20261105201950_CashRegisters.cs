using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashRegisters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AreaName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashShifts", x => x.Id);
                    table.CheckConstraint("CK_CashShifts_OpeningAmount", "\"OpeningAmount\" BETWEEN 0 AND 999999999.99");
                    table.ForeignKey(
                        name: "FK_CashShifts_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashShifts_Users_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Office = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAccounts", x => x.Id);
                    table.CheckConstraint("CK_CashAccounts_Status", "(\"Status\" = 'Abierta' AND \"ClosedAt\" IS NULL AND \"PaymentMethod\" IS NULL) OR (\"Status\" = 'Pagada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NOT NULL AND \"PaymentMethod\" IN ('EFECTIVO','CXC','TARJETA') AND \"Total\" > 0) OR (\"Status\" = 'Cancelada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NULL)");
                    table.CheckConstraint("CK_CashAccounts_Total", "\"Total\" BETWEEN 0 AND 999999999.99");
                    table.ForeignKey(
                        name: "FK_CashAccounts_CashShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashAccounts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CashAccountLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MenuProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Presentation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovalReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAccountLines", x => x.Id);
                    table.CheckConstraint("CK_CashAccountLines_Price", "\"UnitPrice\" > 0 AND \"UnitPrice\" <= 999999.99");
                    table.CheckConstraint("CK_CashAccountLines_Quantity", "\"Quantity\" BETWEEN 1 AND 100000");
                    table.CheckConstraint("CK_CashAccountLines_Source", "(\"InventoryItemId\" IS NOT NULL AND \"MenuProductId\" IS NULL) OR (\"InventoryItemId\" IS NULL AND \"MenuProductId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CashAccountLines_CashAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "CashAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashAccountLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashAccountLines_MenuProducts_MenuProductId",
                        column: x => x.MenuProductId,
                        principalTable: "MenuProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashAccountLines_AccountId",
                table: "CashAccountLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CashAccountLines_InventoryItemId",
                table: "CashAccountLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CashAccountLines_MenuProductId",
                table: "CashAccountLines",
                column: "MenuProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_ClientId",
                table: "CashAccounts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_Number",
                table: "CashAccounts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_ShiftId_Status",
                table: "CashAccounts",
                columns: new[] { "ShiftId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_AreaId",
                table: "CashShifts",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_CashierId",
                table: "CashShifts",
                column: "CashierId",
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_Number",
                table: "CashShifts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_OpenedAt",
                table: "CashShifts",
                column: "OpenedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashAccountLines");

            migrationBuilder.DropTable(
                name: "CashAccounts");

            migrationBuilder.DropTable(
                name: "CashShifts");
        }
    }
}

