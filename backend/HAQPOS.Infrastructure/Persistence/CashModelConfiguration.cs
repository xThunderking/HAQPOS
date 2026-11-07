using HAQPOS.Domain.Areas;
using HAQPOS.Domain.Cash;
using HAQPOS.Domain.Clients;
using HAQPOS.Domain.Inventory;
using HAQPOS.Domain.Menu;
using HAQPOS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Persistence;

internal static class CashModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        var shifts = model.Entity<CashShift>();
        shifts.ToTable("CashShifts", t => t.HasCheckConstraint("CK_CashShifts_OpeningAmount", "\"OpeningAmount\" BETWEEN 0 AND 999999999.99"));
        shifts.HasKey(x => x.Id);
        shifts.Property(x => x.Id).ValueGeneratedNever();
        shifts.Property(x => x.Number).UseIdentityByDefaultColumn();
        shifts.HasIndex(x => x.Number).IsUnique();
        shifts.HasIndex(x => x.CashierId).IsUnique().HasFilter("\"ClosedAt\" IS NULL");
        shifts.HasIndex(x => x.OpenedAt);
        shifts.Property(x => x.OpeningAmount).HasPrecision(14, 2);
        shifts.Property(x => x.CashierName).HasMaxLength(150);
        shifts.Property(x => x.EmployeeNumber).HasMaxLength(20);
        shifts.Property(x => x.AreaName).HasMaxLength(120);
        shifts.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        shifts.HasOne<AreaRecord>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);

        var accounts = model.Entity<CashAccount>();
        accounts.ToTable("CashAccounts", t =>
        {
            t.HasCheckConstraint("CK_CashAccounts_Total", "\"Total\" BETWEEN 0 AND 999999999.99");
            t.HasCheckConstraint("CK_CashAccounts_Status", "(\"Status\" = 'Abierta' AND \"ClosedAt\" IS NULL AND \"PaymentMethod\" IS NULL) OR (\"Status\" = 'Pagada' AND \"ClosedAt\" IS NOT NULL AND ((\"PaymentMethod\" IN ('EFECTIVO','TARJETA') AND \"Total\" > 0) OR (\"PaymentMethod\" = 'CORTESIA' AND \"Category\" = 'CORTESIA' AND \"Total\" = 0))) OR (\"Status\" = 'Pendiente' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" = 'CXC' AND \"Total\" > 0) OR (\"Status\" = 'Cancelada' AND \"ClosedAt\" IS NOT NULL AND \"PaymentMethod\" IS NULL)");
            t.HasCheckConstraint("CK_CashAccounts_PaymentDetails", "(\"PaymentDetailsComplete\" = FALSE AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL) OR (\"PaymentDetailsComplete\" = TRUE AND ((\"PaymentMethod\" = 'EFECTIVO' AND \"AmountReceived\" >= \"Total\" AND \"ChangeGiven\" = \"AmountReceived\" - \"Total\" AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL) OR (\"PaymentMethod\" = 'TARJETA' AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" ~ '^[0-9]{4}$' AND length(\"CardAuthorization\") BETWEEN 1 AND 40 AND length(\"CardBank\") BETWEEN 1 AND 80 AND \"CardType\" IN ('CREDITO','DEBITO')) OR (\"PaymentMethod\" IN ('CXC','CORTESIA') AND \"AmountReceived\" IS NULL AND \"ChangeGiven\" IS NULL AND \"CardLastFour\" IS NULL AND \"CardAuthorization\" IS NULL AND \"CardBank\" IS NULL AND \"CardType\" IS NULL)))");
        });
        accounts.HasKey(x => x.Id);
        accounts.Property(x => x.Id).ValueGeneratedNever();
        accounts.Property(x => x.Number).UseIdentityByDefaultColumn();
        accounts.HasIndex(x => x.Number).IsUnique();
        accounts.HasIndex(x => new { x.ShiftId, x.Status });
        accounts.Property(x => x.ClientName).HasMaxLength(150);
        accounts.Property(x => x.RegistrationNumber).HasMaxLength(10);
        accounts.Property(x => x.Category).HasMaxLength(20);
        accounts.Property(x => x.Office).HasMaxLength(80);
        accounts.Property(x => x.Status).HasMaxLength(12);
        accounts.Property(x => x.PaymentMethod).HasMaxLength(10);
        accounts.Property(x => x.AmountReceived).HasPrecision(14, 2);
        accounts.Property(x => x.ChangeGiven).HasPrecision(14, 2);
        accounts.Property(x => x.CardLastFour).HasMaxLength(4);
        accounts.Property(x => x.CardAuthorization).HasMaxLength(40);
        accounts.Property(x => x.CardBank).HasMaxLength(80);
        accounts.Property(x => x.CardType).HasMaxLength(8);
        accounts.Property(x => x.CancellationReason).HasMaxLength(250);
        accounts.Property(x => x.Total).HasPrecision(14, 2);
        accounts.Property(x => x.Version).IsConcurrencyToken();
        accounts.HasOne<CashShift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        accounts.HasOne<ClientRecord>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);

        var lines = model.Entity<CashAccountLine>();
        lines.ToTable("CashAccountLines", t =>
        {
            t.HasCheckConstraint("CK_CashAccountLines_Quantity", "\"Quantity\" BETWEEN 1 AND 100000");
            t.HasCheckConstraint("CK_CashAccountLines_Price", "\"UnitPrice\" > 0 AND \"UnitPrice\" <= 999999.99");
            t.HasCheckConstraint("CK_CashAccountLines_Source", "(\"InventoryItemId\" IS NOT NULL AND \"MenuProductId\" IS NULL) OR (\"InventoryItemId\" IS NULL AND \"MenuProductId\" IS NOT NULL)");
        });
        lines.HasKey(x => x.Id);
        lines.Property(x => x.Id).ValueGeneratedNever();
        lines.Property(x => x.Code).HasMaxLength(30);
        lines.Property(x => x.Name).HasMaxLength(120);
        lines.Property(x => x.Presentation).HasMaxLength(80);
        lines.Property(x => x.UnitPrice).HasPrecision(12, 2);
        lines.Property(x => x.RemovalReason).HasMaxLength(250);
        lines.HasIndex(x => x.AccountId);
        lines.HasOne<CashAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        lines.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        lines.HasOne<MenuProduct>().WithMany().HasForeignKey(x => x.MenuProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

