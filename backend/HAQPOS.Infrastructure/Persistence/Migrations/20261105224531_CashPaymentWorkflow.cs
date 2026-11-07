using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashPaymentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashAccounts_Status",
                table: "CashAccounts");

            migrationBuilder.Sql("UPDATE \"CashAccounts\" SET \"Status\" = 'Pendiente' WHERE \"Status\" = 'Pagada' AND \"PaymentMethod\" = 'CXC'");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountReceived",
                table: "CashAccounts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardAuthorization",
                table: "CashAccounts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardBank",
                table: "CashAccounts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardLastFour",
                table: "CashAccounts",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "CashAccounts",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeGiven",
                table: "CashAccounts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentDetailsComplete",
                table: "CashAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashAccounts_PaymentDetails",
                table: "CashAccounts",
                sql: "(\"PaymentDetailsComplete\" = FALSE AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL) OR (\"PaymentDetailsComplete\" = TRUE AND ((\"PaymentMethod\" = 'EFECTIVO' AND \"AmountReceived\" >= \"Total\" AND \"ChangeGiven\" = \"AmountReceived\" - \"Total\" AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL) OR (\"PaymentMethod\" = 'TARJETA' AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" ~ '^[0-9]{4}$' AND length(\"CardAuthorization\") BETWEEN 1 AND 40 AND length(\"CardBank\") BETWEEN 1 AND 80 AND \"CardType\" IN ('CREDITO','DEBITO')) OR (\"PaymentMethod\" IN ('CXC','CORTESIA') AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashAccounts_Status",
                table: "CashAccounts",
                sql: "(\"Status\" = 'Abierta' AND \"ClosedAt\" IS NULL AND \"PaymentMethod\" IS NULL) OR (\"Status\" = 'Pagada' AND \"ClosedAt\" IS NOT NULL AND ((\"PaymentMethod\" IN ('EFECTIVO','TARJETA') AND \"Total\" > 0) OR (\"PaymentMethod\" = 'CORTESIA' AND \"Category\" = 'CORTESIA' AND \"Total\" = 0))) OR (\"Status\" = 'Pendiente' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" = 'CXC' AND \"Total\" > 0) OR (\"Status\" = 'Cancelada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashAccounts_PaymentDetails",
                table: "CashAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashAccounts_Status",
                table: "CashAccounts");

            migrationBuilder.Sql("UPDATE \"CashAccounts\" SET \"Status\" = 'Pagada' WHERE \"Status\" = 'Pendiente' AND \"PaymentMethod\" = 'CXC'");

            migrationBuilder.DropColumn(
                name: "AmountReceived",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "CardAuthorization",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "CardBank",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "CardLastFour",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "ChangeGiven",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "PaymentDetailsComplete",
                table: "CashAccounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashAccounts_Status",
                table: "CashAccounts",
                sql: "(\"Status\" = 'Abierta' AND \"ClosedAt\" IS NULL AND \"PaymentMethod\" IS NULL) OR (\"Status\" = 'Pagada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NOT NULL AND \"PaymentMethod\" IN ('EFECTIVO','CXC','TARJETA') AND \"Total\" > 0) OR (\"Status\" = 'Cancelada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NULL)");
        }
    }
}
