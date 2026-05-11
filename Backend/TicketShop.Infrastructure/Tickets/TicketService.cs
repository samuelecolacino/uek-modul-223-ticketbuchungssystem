using Microsoft.EntityFrameworkCore;
using TicketShop.Application.Tickets;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Infrastructure.Tickets;

public class TicketService : ITicketService
{
    private const string ConcurrencyMessage =
        "Das Ticket wurde in der Zwischenzeit von einem anderen Benutzer gekauft.";

    private const string ForbiddenMessage =
        "Dieses Ticket ist nur für Administratoren verfügbar.";

    private readonly AppDbContext _context;

    public TicketService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AvailableCategoryDto>> GetAvailableGroupedAsync(bool includeAdminOnly, CancellationToken ct = default)
    {
        var categoryQuery = _context.TicketCategories.AsNoTracking();
        if (!includeAdminOnly)
        {
            categoryQuery = categoryQuery.Where(c => !c.IsAdminOnly);
        }

        var categories = await categoryQuery
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        var availableByCategory = await _context.Tickets
            .AsNoTracking()
            .Where(t => !t.IsSold)
            .GroupBy(t => t.TicketCategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Count = g.Count(),
                TicketIds = g.OrderBy(t => t.Id).Select(t => t.Id).ToList()
            })
            .ToListAsync(ct);

        var lookup = availableByCategory.ToDictionary(x => x.CategoryId);

        return categories
            .Select(c => lookup.TryGetValue(c.Id, out var a)
                ? new AvailableCategoryDto(c.Id, c.Name, c.Price, a.Count, a.TicketIds, c.IsAdminOnly)
                : new AvailableCategoryDto(c.Id, c.Name, c.Price, 0, Array.Empty<int>(), c.IsAdminOnly))
            .ToList();
    }

    public async Task<TicketPurchaseResult> BuyAsync(int ticketId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var ticket = await _context.Tickets
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.IsSold == false, ct);
        if (ticket is null)
        {
            return new TicketPurchaseResult(TicketPurchaseStatus.NotFoundOrAlreadySold, ticketId, null, "Ticket already sold");
        }

        if (ticket.Category.IsAdminOnly && !isAdmin)
        {
            return new TicketPurchaseResult(TicketPurchaseStatus.Forbidden, ticketId, null, ForbiddenMessage);
        }

        ticket.IsSold = true;
        ticket.UserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new TicketPurchaseResult(TicketPurchaseStatus.ConcurrencyConflict, ticketId, null, ConcurrencyMessage);
        }

        await transaction.CommitAsync(ct);
        return new TicketPurchaseResult(TicketPurchaseStatus.Success, ticketId, userId, null, ticket.TicketCategoryId);
    }
}
