using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Persistence;

public sealed class HaqposDbContext(DbContextOptions<HaqposDbContext> options) : DbContext(options);
