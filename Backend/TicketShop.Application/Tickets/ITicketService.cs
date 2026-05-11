namespace TicketShop.Application.Tickets;

public interface ITicketService
{
    Task<IReadOnlyList<AvailableCategoryDto>> GetAvailableGroupedAsync(bool includeAdminOnly, CancellationToken ct = default);
    Task<TicketPurchaseResult> BuyAsync(int ticketId, int userId, bool isAdmin, CancellationToken ct = default);
}

public enum TicketPurchaseStatus
{
    Success,
    NotFoundOrAlreadySold,
    ConcurrencyConflict,
    Forbidden
}

public record TicketPurchaseResult(TicketPurchaseStatus Status, int TicketId, int? UserId, string? Message, int? CategoryId = null);
