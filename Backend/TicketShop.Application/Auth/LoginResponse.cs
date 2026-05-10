namespace TicketShop.Application.Auth;

public record LoginResponse(string Token, string Username, string Role);
