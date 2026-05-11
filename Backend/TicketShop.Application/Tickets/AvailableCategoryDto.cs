namespace TicketShop.Application.Tickets;

public record AvailableCategoryDto(int CategoryId, string Name, decimal Price, int AvailableCount, IReadOnlyList<int> TicketIds, bool IsAdminOnly);
