using FluentValidation;

namespace TicketShop.Application.Tickets;

public class BuyTicketDtoValidator : AbstractValidator<BuyTicketDto>
{
    public BuyTicketDtoValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
    }
}
