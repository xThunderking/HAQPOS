using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HaqposDbContext))]
[Migration("20261104160000_GlobalMenuInventoryBarcodes")]
public partial class GlobalMenuInventoryBarcodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            LOCK TABLE "MenuProducts", "InventoryItems" IN ACCESS EXCLUSIVE MODE;

            DO $migration$
            DECLARE
                duplicate_item RECORD;
                candidate BIGINT := 100000000;
            BEGIN
                FOR duplicate_item IN
                    SELECT inventory."Id"
                    FROM "InventoryItems" AS inventory
                    WHERE EXISTS
                    (
                        SELECT 1 FROM "MenuProducts" AS menu
                        WHERE UPPER(menu."Code") = UPPER(inventory."Code")
                    )
                    OR EXISTS
                    (
                        SELECT 1 FROM "InventoryItems" AS earlier
                        WHERE UPPER(earlier."Code") = UPPER(inventory."Code")
                          AND (earlier."CreatedAt", earlier."Id") < (inventory."CreatedAt", inventory."Id")
                    )
                    ORDER BY inventory."CreatedAt", inventory."Id"
                LOOP
                    LOOP
                        IF candidate > 999999999 THEN
                            RAISE EXCEPTION 'No available nine-digit barcode remains.';
                        END IF;

                        EXIT WHEN NOT EXISTS
                        (
                            SELECT 1 FROM "MenuProducts" AS menu
                            WHERE UPPER(menu."Code") = candidate::text
                            UNION ALL
                            SELECT 1 FROM "InventoryItems" AS inventory
                            WHERE UPPER(inventory."Code") = candidate::text
                        );

                        candidate := candidate + 1;
                    END LOOP;

                    UPDATE "InventoryItems"
                    SET "Code" = candidate::text
                    WHERE "Id" = duplicate_item."Id";

                    candidate := candidate + 1;
                END LOOP;
            END
            $migration$;

            CREATE OR REPLACE FUNCTION public.haqpos_ensure_global_barcode_unique()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                PERFORM pg_advisory_xact_lock(20261104, 748452);

                IF TG_TABLE_NAME = 'MenuProducts' THEN
                    IF EXISTS (
                        SELECT 1 FROM "MenuProducts"
                        WHERE UPPER("Code") = UPPER(NEW."Code") AND "Id" <> NEW."Id"
                    ) OR EXISTS (
                        SELECT 1 FROM "InventoryItems"
                        WHERE UPPER("Code") = UPPER(NEW."Code")
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23505',
                            CONSTRAINT = 'UX_MenuInventory_GlobalBarcode',
                            MESSAGE = 'Barcode is already assigned to another product.';
                    END IF;
                ELSE
                    IF EXISTS (
                        SELECT 1 FROM "InventoryItems"
                        WHERE UPPER("Code") = UPPER(NEW."Code") AND "Id" <> NEW."Id"
                    ) OR EXISTS (
                        SELECT 1 FROM "MenuProducts"
                        WHERE UPPER("Code") = UPPER(NEW."Code")
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23505',
                            CONSTRAINT = 'UX_MenuInventory_GlobalBarcode',
                            MESSAGE = 'Barcode is already assigned to another product.';
                    END IF;
                END IF;

                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER "TR_MenuProducts_GlobalBarcode"
            BEFORE INSERT OR UPDATE OF "Code" ON "MenuProducts"
            FOR EACH ROW EXECUTE FUNCTION public.haqpos_ensure_global_barcode_unique();

            CREATE TRIGGER "TR_InventoryItems_GlobalBarcode"
            BEFORE INSERT OR UPDATE OF "Code" ON "InventoryItems"
            FOR EACH ROW EXECUTE FUNCTION public.haqpos_ensure_global_barcode_unique();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS "TR_MenuProducts_GlobalBarcode" ON "MenuProducts";
            DROP TRIGGER IF EXISTS "TR_InventoryItems_GlobalBarcode" ON "InventoryItems";
            DROP FUNCTION IF EXISTS public.haqpos_ensure_global_barcode_unique();
            """);
    }
}
