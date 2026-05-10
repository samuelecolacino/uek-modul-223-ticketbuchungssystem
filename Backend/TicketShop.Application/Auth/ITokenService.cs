using TicketShop.Core.Entities;

namespace TicketShop.Application.Auth;

public interface ITokenService
{
    string CreateToken(User user);
}
