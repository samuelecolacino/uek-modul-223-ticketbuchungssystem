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
                new TicketCategory { Id = 2, Name = "Standard", Price = 80.00m },
                new TicketCategory { Id = 3, Name = "Mitglieder Rabatt (Admin)", Price = 40.00m, IsAdminOnly = true }
            },
            tickets: new[]
            {
                new Ticket { Id = 1, TicketCategoryId = 1 },
                new Ticket { Id = 2, TicketCategoryId = 1 },
                new Ticket { Id = 3, TicketCategoryId = 2, IsSold = true, UserId = null },
                new Ticket { Id = 4, TicketCategoryId = 3 }
            },
            users: new[]
            {
                new User { Id = 1, Username = "user", PasswordHash = "x", Role = "User" }
            });
    }

    [Fact]
    public async Task GetAvailableGrouped_NonAdmin_HidesAdminOnlyCategories()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.GetAvailableGroupedAsync(includeAdminOnly: false);

        result.Should().HaveCount(2);
        result.Select(c => c.CategoryId).Should().BeEquivalentTo(new[] { 1, 2 });

        var vip = result.Single(c => c.CategoryId == 1);
        vip.Name.Should().Be("VIP");
        vip.AvailableCount.Should().Be(2);
        vip.TicketIds.Should().BeEquivalentTo(new[] { 1, 2 });
        vip.IsAdminOnly.Should().BeFalse();

        var standard = result.Single(c => c.CategoryId == 2);
        standard.AvailableCount.Should().Be(0);
        standard.TicketIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableGrouped_Admin_IncludesAdminOnlyCategories()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.GetAvailableGroupedAsync(includeAdminOnly: true);

        result.Should().HaveCount(3);
        var admin = result.Single(c => c.CategoryId == 3);
        admin.Name.Should().Be("Mitglieder Rabatt (Admin)");
        admin.IsAdminOnly.Should().BeTrue();
        admin.AvailableCount.Should().Be(1);
    }

    [Fact]
    public async Task BuyAsync_AvailableTicket_MarksItSoldAndReturnsSuccess()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 1, userId: 1, isAdmin: false);

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

        var result = await service.BuyAsync(ticketId: 3, userId: 1, isAdmin: false);

        result.Status.Should().Be(TicketPurchaseStatus.NotFoundOrAlreadySold);
    }

    [Fact]
    public async Task BuyAsync_NonExistentTicket_ReturnsNotFoundOrAlreadySold()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 9999, userId: 1, isAdmin: false);

        result.Status.Should().Be(TicketPurchaseStatus.NotFoundOrAlreadySold);
    }

    [Fact]
    public async Task BuyAsync_AdminOnlyTicket_AsNormalUser_ReturnsForbidden()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 4, userId: 1, isAdmin: false);

        result.Status.Should().Be(TicketPurchaseStatus.Forbidden);

        using var verifyCtx = factory.CreateContext();
        var ticket = await verifyCtx.Tickets.SingleAsync(t => t.Id == 4);
        ticket.IsSold.Should().BeFalse();
    }

    [Fact]
    public async Task BuyAsync_AdminOnlyTicket_AsAdmin_Succeeds()
    {
        using var factory = new SqliteTestDbFactory();
        SeedBaseFixture(factory);

        using var ctx = factory.CreateContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 4, userId: 1, isAdmin: true);

        result.Status.Should().Be(TicketPurchaseStatus.Success);
    }
}
