using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HaqposDbContext))]
[Migration("20261104150000_BackfillShortMenuBarcodes")]
public partial class BackfillShortMenuBarcodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            LOCK TABLE "MenuProducts" IN ACCESS EXCLUSIVE MODE;
            DROP INDEX "IX_MenuProducts_Code";

            WITH numbered_products AS
            (
                SELECT "Id",
                    (100000000 + ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") - 1)::text AS "ShortCode"
                FROM "MenuProducts"
            )
            UPDATE "MenuProducts" AS products
            SET "Code" = numbered_products."ShortCode"
            FROM numbered_products
            WHERE products."Id" = numbered_products."Id";

            CREATE UNIQUE INDEX "IX_MenuProducts_Code" ON "MenuProducts" ("Code");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Short identifiers remain valid if the application is rolled back.
    }
}
