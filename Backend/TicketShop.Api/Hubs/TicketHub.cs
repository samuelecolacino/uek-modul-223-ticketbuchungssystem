using Microsoft.AspNetCore.SignalR;

namespace TicketShop.Api.Hubs;

public class TicketHub : Hub
{
    public const string Path = "/hubs/tickets";
}
