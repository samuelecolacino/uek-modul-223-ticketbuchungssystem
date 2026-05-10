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
        var grouped = await _context.Tickets
            .AsNoTracking()
            .Where(t => !t.IsSold)
            .Include(t => t.Category)
            .GroupBy(t => new { t.TicketCategoryId, t.Category!.Name, t.Category.Price })
            .Select(g => new AvailableCategoryDto(
                g.Key.TicketCategoryId,
                g.Key.Name,
                g.Key.Price,
                g.Count(),
                g.Select(t => t.Id).ToList()))
            .ToListAsync(ct);

        return grouped;
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
        return new TicketPurchaseResult(TicketPurchaseStatus.Success, ticketId, userId, null);
    }
}
