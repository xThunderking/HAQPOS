using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAQPOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HaqposDbContext))]
[Migration("20261105090000_InventoryPieceStockAndAreaBarcodes")]
public sealed partial class InventoryPieceStockAndAreaBarcodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "InputQuantity", table: "InventoryMovements", type: "numeric(18,3)", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "InputUnit", table: "InventoryMovements", type: "character varying(24)", maxLength: 24, nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_InventoryMovements_InputSnapshot",
            table: "InventoryMovements",
            sql: "(\"InputQuantity\" IS NULL AND \"InputUnit\" IS NULL) OR (\"InputQuantity\" IS NOT NULL AND \"InputQuantity\" >= 0 AND \"InputUnit\" IN ('Pieza', 'Caja 12', 'Caja 24', 'Caja 48'))");

        migrationBuilder.Sql(
            """
            LOCK TABLE "InventoryItems" IN ACCESS EXCLUSIVE MODE;

            UPDATE "InventoryItems"
            SET "CurrentStock" = "CurrentStock" * CASE UPPER("Unit")
                    WHEN 'CAJA 12' THEN 12 WHEN 'CAJA 24' THEN 24 WHEN 'CAJA 48' THEN 48 ELSE 1 END,
                "MinimumStock" = "MinimumStock" * CASE UPPER("Unit")
                    WHEN 'CAJA 12' THEN 12 WHEN 'CAJA 24' THEN 24 WHEN 'CAJA 48' THEN 48 ELSE 1 END,
                "UnitPrice" = CASE WHEN "UnitPrice" IS NULL THEN NULL ELSE ROUND("UnitPrice" /
                    CASE UPPER("Unit") WHEN 'CAJA 12' THEN 12 WHEN 'CAJA 24' THEN 24
                        WHEN 'CAJA 48' THEN 48 ELSE 1 END, 2) END,
                "Unit" = 'Pieza';

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
                        RAISE EXCEPTION USING ERRCODE = '23505',
                            CONSTRAINT = 'UX_MenuInventory_GlobalBarcode',
                            MESSAGE = 'Barcode is already assigned to another product.';
                    END IF;
                ELSE
                    IF EXISTS (
                        SELECT 1 FROM "InventoryItems"
                        WHERE UPPER("Code") = UPPER(NEW."Code")
                          AND "AreaId" = NEW."AreaId" AND "Id" <> NEW."Id"
                    ) OR EXISTS (
                        SELECT 1 FROM "MenuProducts"
                        WHERE UPPER("Code") = UPPER(NEW."Code")
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23505',
                            CONSTRAINT = 'UX_MenuInventory_GlobalBarcode',
                            MESSAGE = 'Barcode is already assigned to another product in this area or menu.';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $function$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION public.haqpos_ensure_global_barcode_unique()
            RETURNS trigger LANGUAGE plpgsql AS $function$
            BEGIN
                PERFORM pg_advisory_xact_lock(20261104, 748452);
                IF EXISTS (SELECT 1 FROM "MenuProducts" WHERE UPPER("Code") = UPPER(NEW."Code") AND "Id" <> NEW."Id")
                   OR EXISTS (SELECT 1 FROM "InventoryItems" WHERE UPPER("Code") = UPPER(NEW."Code") AND "Id" <> NEW."Id") THEN
                    RAISE EXCEPTION USING ERRCODE = '23505', CONSTRAINT = 'UX_MenuInventory_GlobalBarcode',
                        MESSAGE = 'Barcode is already assigned to another product.';
                END IF;
                RETURN NEW;
            END;
            $function$;
            """);
        migrationBuilder.DropCheckConstraint("CK_InventoryMovements_InputSnapshot", "InventoryMovements");
        migrationBuilder.DropColumn("InputQuantity", "InventoryMovements");
        migrationBuilder.DropColumn("InputUnit", "InventoryMovements");
    }
}
