using Microsoft.EntityFrameworkCore;
using TicketShop.Application.Tickets;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Infrastructure.Tickets;

public class TicketService : ITicketService
{
    private const string ConcurrencyMessage =
        "Das Ticket wurde in der Zwischenzeit von einem anderen Benutzer gekauft.";

    private readonly AppDbContext _context;

    public TicketService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AvailableCategoryDto>> GetAvailableGroupedAsync(CancellationToken ct = default)
    {
        var categories = await _context.TicketCategories
            .AsNoTracking()
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
                ? new AvailableCategoryDto(c.Id, c.Name, c.Price, a.Count, a.TicketIds)
                : new AvailableCategoryDto(c.Id, c.Name, c.Price, 0, Array.Empty<int>()))
            .ToList();
    }

    public async Task<TicketPurchaseResult> BuyAsync(int ticketId, int userId, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var ticket = _context.Tickets.FirstOrDefault(t => t.Id == ticketId && t.IsSold == false);
        if (ticket is null)
        {
            return new TicketPurchaseResult(TicketPurchaseStatus.NotFoundOrAlreadySold, ticketId, null, "Ticket already sold");
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
