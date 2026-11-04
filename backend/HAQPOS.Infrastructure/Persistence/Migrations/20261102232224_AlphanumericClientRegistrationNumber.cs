using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlphanumericClientRegistrationNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clients_RegistrationNumber",
                table: "Clients");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clients_RegistrationNumber",
                table: "Clients",
                sql: "\"RegistrationNumber\" ~ '^[A-Za-z0-9]{1,10}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clients_RegistrationNumber",
                table: "Clients");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clients_RegistrationNumber",
                table: "Clients",
                sql: "\"RegistrationNumber\" ~ '^[0-9]{1,10}$'");
        }
    }
}
