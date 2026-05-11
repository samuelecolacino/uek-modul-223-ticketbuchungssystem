using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TicketShop.Application.Tickets;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Tickets;
using TicketShop.Tests.Infrastructure;

namespace TicketShop.Tests;

public class TicketServiceTests
{
    private static void SeedBaseFixture(SqliteTestDbFactory factory)
    {
        factory.Seed(
            categories: new[]
            {
                new TicketCategory { Id = 1, Name = "VIP", Price = 150.00m },
                new TicketCategory { Id = 2, Name = "Standard", Price = 80.00m }
            },
            tickets: new[]
            {
                new Ticket { Id = 1, TicketCategoryId = 1 },
                new Ticket { Id = 2, TicketCategoryId = 1 },
                new Ticket { Id = 3, TicketCategoryId = 2, IsSold = true, UserId = null }
            },
            users: new[]
            {
                new User { Id = 1, Username = "user", PasswordHash = "x", Role = "User" }
            });
    }

    [Fact]
    public async Task GetAvailableGrouped_ReturnsOnlyUnsoldTicketsGroupedByCategory()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.GetAvailableGroupedAsync();

        result.Should().HaveCount(1);
        var vip = result.Single();
        vip.CategoryId.Should().Be(1);
        vip.Name.Should().Be("VIP");
        vip.AvailableCount.Should().Be(2);
        vip.TicketIds.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task BuyAsync_AvailableTicket_MarksItSoldAndReturnsSuccess()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 1, userId: 1);

        result.Status.Should().Be(TicketPurchaseStatus.Success);
        result.TicketId.Should().Be(1);
        result.UserId.Should().Be(1);

        using var verifyCtx = factory.CreateContext();
        var ticket = await verifyCtx.Tickets.SingleAsync(t => t.Id == 1);
        ticket.IsSold.Should().BeTrue();
        ticket.UserId.Should().Be(1);
    }

    [Fact]
    public async Task BuyAsync_AlreadySoldTicket_ReturnsNotFoundOrAlreadySold()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 3, userId: 1);

        result.Status.Should().Be(TicketPurchaseStatus.NotFoundOrAlreadySold);
    }

    [Fact]
    public async Task BuyAsync_NonExistentTicket_ReturnsNotFoundOrAlreadySold()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 9999, userId: 1);

        result.Status.Should().Be(TicketPurchaseStatus.NotFoundOrAlreadySold);
    }
}
