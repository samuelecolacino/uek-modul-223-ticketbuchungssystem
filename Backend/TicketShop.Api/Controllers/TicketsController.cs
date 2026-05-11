using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketShop.Application.Tickets;

namespace TicketShop.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IValidator<BuyTicketDto> _buyValidator;

    public TicketsController(ITicketService ticketService, IValidator<BuyTicketDto> buyValidator)
    {
        _ticketService = ticketService;
        _buyValidator = buyValidator;
    }

    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<AvailableCategoryDto>>> GetAvailable(CancellationToken ct)
    {
        var categories = await _ticketService.GetAvailableGroupedAsync(ct);
        return Ok(categories);
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Buy(BuyTicketDto request, CancellationToken ct)
    {
        var validation = await _buyValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Ungültiges Token: Benutzer-ID fehlt." });
        }

        var result = await _ticketService.BuyAsync(request.TicketId, userId, ct);

        return result.Status switch
        {
            TicketPurchaseStatus.Success => Ok(new { ticketId = result.TicketId, userId = result.UserId }),
            TicketPurchaseStatus.NotFoundOrAlreadySold => NotFound(new { message = "Ticket nicht verfügbar oder bereits verkauft." }),
            TicketPurchaseStatus.ConcurrencyConflict => Conflict(new { message = result.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
