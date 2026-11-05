using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Persistence;

public static class GlobalBarcodeWriteLock
{
    public static async Task AcquireAsync(HaqposDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN PERFORM pg_advisory_xact_lock(20261104, 748452); END $$;",
            cancellationToken);
    }
}
