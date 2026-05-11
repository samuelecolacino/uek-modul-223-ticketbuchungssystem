namespace TicketShop.Application.Tickets;

public interface ITicketService
{
    Task<IReadOnlyList<AvailableCategoryDto>> GetAvailableGroupedAsync(CancellationToken ct = default);
    Task<TicketPurchaseResult> BuyAsync(int ticketId, int userId, CancellationToken ct = default);
}

public enum TicketPurchaseStatus
{
    Success,
    NotFoundOrAlreadySold,
    ConcurrencyConflict
}

public record TicketPurchaseResult(TicketPurchaseStatus Status, int TicketId, int? UserId, string? Message, int? CategoryId = null);
