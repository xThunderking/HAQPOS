using Microsoft.EntityFrameworkCore;
using HAQPOS.Domain.Inventory;
using HAQPOS.Domain.Users;
using HAQPOS.Domain.Menu;

namespace HAQPOS.Infrastructure.Persistence;

public sealed class HaqposDbContext(DbContextOptions<HaqposDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<MenuProduct> MenuProducts => Set<MenuProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var users = modelBuilder.Entity<UserAccount>();
        users.ToTable("Users", t =>
        {
            t.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('Administrador', 'Supervisor', 'Cajero')");
            t.HasCheckConstraint("CK_Users_EmployeeNumber", "\"EmployeeNumber\" ~ '^[0-9]{1,20}$'");
        });
        users.HasKey(x => x.Id);
        users.Property(x => x.EmployeeNumber).HasMaxLength(20).IsRequired();
        users.HasIndex(x => x.EmployeeNumber).IsUnique();
        users.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        users.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        users.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        users.Property(x => x.Version).IsConcurrencyToken();

        var sessions = modelBuilder.Entity<UserSession>();
        sessions.HasKey(x => x.Id);
        sessions.HasIndex(x => x.UserId);
        sessions.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var audit = modelBuilder.Entity<SecurityAuditEvent>();
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(80).IsRequired();
        audit.Property(x => x.IpAddress).HasMaxLength(64);
        audit.HasIndex(x => x.OccurredAt);

        var inventoryItems = modelBuilder.Entity<InventoryItem>();
        inventoryItems.ToTable("InventoryItems", t =>
        {
            t.HasCheckConstraint("CK_InventoryItems_CurrentStock", "\"CurrentStock\" BETWEEN 0 AND 1000000000");
            t.HasCheckConstraint("CK_InventoryItems_MinimumStock", "\"MinimumStock\" BETWEEN 0 AND 1000000000");
            t.HasCheckConstraint("CK_InventoryItems_UnitPrice", "\"UnitPrice\" IS NULL OR (\"UnitPrice\" > 0 AND \"UnitPrice\" <= 999999.99)");
        });
        inventoryItems.HasKey(x => x.Id);
        inventoryItems.Property(x => x.Code).HasMaxLength(30).IsRequired();
        inventoryItems.HasIndex(x => x.Code).IsUnique();
        inventoryItems.Property(x => x.Name).HasMaxLength(120).IsRequired();
        inventoryItems.Property(x => x.Category).HasMaxLength(80).IsRequired();
        inventoryItems.Property(x => x.Unit).HasMaxLength(24).IsRequired();
        inventoryItems.Property(x => x.Location).HasMaxLength(80);
        inventoryItems.Property(x => x.CurrentStock).HasPrecision(18, 3);
        inventoryItems.Property(x => x.MinimumStock).HasPrecision(18, 3);
        inventoryItems.Property(x => x.UnitPrice).HasPrecision(12, 2);
        inventoryItems.Property(x => x.Version).IsConcurrencyToken();

        var inventoryMovements = modelBuilder.Entity<InventoryMovement>();
        inventoryMovements.ToTable("InventoryMovements", t =>
        {
            t.HasCheckConstraint("CK_InventoryMovements_Quantity", "\"Quantity\" <> 0");
            t.HasCheckConstraint("CK_InventoryMovements_Type", "\"Type\" IN ('Entrada', 'Salida', 'Ajuste')");
            t.HasCheckConstraint("CK_InventoryMovements_QuantityLimit", "abs(\"Quantity\") <= 1000000000");
            t.HasCheckConstraint("CK_InventoryMovements_StockBefore", "\"StockBefore\" BETWEEN 0 AND 1000000000");
            t.HasCheckConstraint("CK_InventoryMovements_StockAfter", "\"StockAfter\" BETWEEN 0 AND 1000000000");
        });
        inventoryMovements.HasKey(x => x.Id);
        inventoryMovements.Property(x => x.Type).HasConversion<string>().HasMaxLength(12).IsRequired();
        inventoryMovements.Property(x => x.Quantity).HasPrecision(18, 3);
        inventoryMovements.Property(x => x.StockBefore).HasPrecision(18, 3);
        inventoryMovements.Property(x => x.StockAfter).HasPrecision(18, 3);
        inventoryMovements.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        inventoryMovements.HasIndex(x => new { x.ItemId, x.OccurredAt });
        inventoryMovements.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        inventoryMovements.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);

        var menuProducts = modelBuilder.Entity<MenuProduct>();
        menuProducts.ToTable("MenuProducts", t =>
        {
            t.HasCheckConstraint("CK_MenuProducts_Price", "\"Price\" > 0 AND \"Price\" <= 999999.99");
        });
        menuProducts.HasKey(x => x.Id);
        menuProducts.Property(x => x.Code).HasMaxLength(30).IsRequired();
        menuProducts.HasIndex(x => x.Code).IsUnique();
        menuProducts.Property(x => x.Name).HasMaxLength(120).IsRequired();
        menuProducts.Property(x => x.Category).HasMaxLength(60).IsRequired();
        menuProducts.Property(x => x.Description).HasMaxLength(250);
        menuProducts.Property(x => x.Price).HasPrecision(12, 2);
        menuProducts.Property(x => x.Version).IsConcurrencyToken();
    }
}
