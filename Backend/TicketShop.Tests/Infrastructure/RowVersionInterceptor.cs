using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TicketShop.Core.Entities;

namespace TicketShop.Tests.Infrastructure;

internal sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        BumpVersions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        BumpVersions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    //KI GENERIERT: BumpVersions, damit die RowVersion bei jedem Speichern aktualisiert wird, da in den Tests keine echte Datenbank verwendet wird, die dies automatisch erledigt.
    private static void BumpVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Ticket>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(t => t.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
