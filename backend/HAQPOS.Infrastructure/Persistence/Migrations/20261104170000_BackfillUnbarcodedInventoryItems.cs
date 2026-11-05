using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HaqposDbContext))]
[Migration("20261104170000_BackfillUnbarcodedInventoryItems")]
public partial class BackfillUnbarcodedInventoryItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            LOCK TABLE "MenuProducts", "InventoryItems" IN ACCESS EXCLUSIVE MODE;

            DO $migration$
            DECLARE
                unbarcoded_item RECORD;
                candidate BIGINT := 100000000;
            BEGIN
                PERFORM pg_advisory_xact_lock(20261104, 748452);

                FOR unbarcoded_item IN
                    SELECT "Id"
                    FROM "InventoryItems"
                    WHERE BTRIM("Code") = ''
                       OR (UPPER(BTRIM("Code")) = UPPER(BTRIM("Name"))
                           AND BTRIM("Code") !~ '^[0-9]{8,14}$')
                    ORDER BY "CreatedAt", "Id"
                LOOP
                    LOOP
                        IF candidate > 999999999 THEN
                            RAISE EXCEPTION 'No available nine-digit barcode remains.';
                        END IF;

                        EXIT WHEN NOT EXISTS
                        (
                            SELECT 1 FROM "MenuProducts"
                            WHERE UPPER("Code") = candidate::text
                            UNION ALL
                            SELECT 1 FROM "InventoryItems"
                            WHERE UPPER("Code") = candidate::text
                        );

                        candidate := candidate + 1;
                    END LOOP;

                    UPDATE "InventoryItems"
                    SET "Code" = candidate::text
                    WHERE "Id" = unbarcoded_item."Id";

                    candidate := candidate + 1;
                END LOOP;
            END
            $migration$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Generated barcodes remain valid if the application is rolled back.
    }
}
