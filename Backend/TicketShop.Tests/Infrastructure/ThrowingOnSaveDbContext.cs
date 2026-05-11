using Microsoft.EntityFrameworkCore;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Tests.Infrastructure;

internal sealed class ThrowingOnSaveDbContext : TestAppDbContext
{
    public ThrowingOnSaveDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public override int SaveChanges()
        => throw new DbUpdateConcurrencyException("Simulated concurrent update conflict.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new DbUpdateConcurrencyException("Simulated concurrent update conflict.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new DbUpdateConcurrencyException("Simulated concurrent update conflict.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new DbUpdateConcurrencyException("Simulated concurrent update conflict.");
}
