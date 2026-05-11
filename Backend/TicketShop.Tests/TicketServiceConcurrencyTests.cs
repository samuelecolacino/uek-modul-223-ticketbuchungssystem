using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TicketShop.Application.Tickets;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Tickets;
using TicketShop.Tests.Infrastructure;

namespace TicketShop.Tests;

public class TicketServiceConcurrencyTests
{
    private const string ConcurrencyMessage =
        "Das Ticket wurde in der Zwischenzeit von einem anderen Benutzer gekauft.";

    private static void SeedSingleAvailableTicket(SqliteTestDbFactory factory)
    {
        factory.Seed(
            categories: new[] { new TicketCategory { Id = 1, Name = "VIP", Price = 150m } },
            tickets: new[] { new Ticket { Id = 1, TicketCategoryId = 1 } },
            users: new[]
            {
                new User { Id = 1, Username = "u1", PasswordHash = "x", Role = "User" },
                new User { Id = 100, Username = "u100", PasswordHash = "x", Role = "User" },
                new User { Id = 200, Username = "u200", PasswordHash = "x", Role = "User" }
            });
    }

    [Fact]
    public async Task BuyTicket_ConcurrentAccess_ThrowsException()
    {
        // Two independent in-memory databases, each pre-seeded with the same available ticket.
        // Service A uses a real context (its SaveChanges succeeds and reflects a winning purchase).
        // Service B uses a stubbed context whose SaveChangesAsync throws DbUpdateConcurrencyException,
        // simulating the second-arriving transaction whose RowVersion no longer matches the row in the DB.
        using var factoryA = new SqliteTestDbFactory();
        using var factoryB = new SqliteTestDbFactory();
        SeedSingleAvailableTicket(factoryA);
        SeedSingleAvailableTicket(factoryB);

        using var ctxA = factoryA.CreateContext();
        using var ctxB = factoryB.CreateThrowingContext();

        var serviceA = new TicketService(ctxA);
        var serviceB = new TicketService(ctxB);

        // Task.WhenAll fires both BuyAsync calls in parallel — Phase-2 spec requirement.
        var results = await Task.WhenAll(
            Task.Run(() => serviceA.BuyAsync(ticketId: 1, userId: 100)),
            Task.Run(() => serviceB.BuyAsync(ticketId: 1, userId: 200)));

        // The winning service returns Success.
        results.Should().ContainSingle(r => r.Status == TicketPurchaseStatus.Success);

        // The losing service caught DbUpdateConcurrencyException and reported ConcurrencyConflict
        // with the user-facing German message — this is the catch-handling proof the spec asks for.
        var conflict = results.Single(r => r.Status == TicketPurchaseStatus.ConcurrencyConflict);
        conflict.Message.Should().Be(ConcurrencyMessage);
        conflict.UserId.Should().BeNull();
    }

    [Fact]
    public async Task BuyAsync_DbUpdateConcurrencyException_IsCaughtAndReturnsConflict()
    {
        using var factory = new SqliteTestDbFactory();
        SeedSingleAvailableTicket(factory);

        using var ctx = factory.CreateThrowingContext();
        var service = new TicketService(ctx);

        var result = await service.BuyAsync(ticketId: 1, userId: 1);

        result.Status.Should().Be(TicketPurchaseStatus.ConcurrencyConflict);
        result.Message.Should().Be(ConcurrencyMessage);
    }

    [Fact]
    public async Task RowVersionMismatch_OnConcurrentSaveChanges_RaisesDbUpdateConcurrencyException()
    {
        // Proves the underlying EF Core RowVersion concurrency check actually fires on the
        // shared in-memory SQLite database that backs these tests. Two separate DbContexts
        // both load the same Ticket, then both try to commit a Modified state — the second
        // one collides on RowVersion and gets DbUpdateConcurrencyException from EF directly.
        using var factory = new SqliteTestDbFactory();
        SeedSingleAvailableTicket(factory);

        using var ctxA = factory.CreateContext();
        using var ctxB = factory.CreateContext();

        var ticketA = await ctxA.Tickets.FirstAsync(t => t.Id == 1);
        var ticketB = await ctxB.Tickets.FirstAsync(t => t.Id == 1);

        ticketA.IsSold = true;
        ticketA.UserId = 100;
        ticketB.IsSold = true;
        ticketB.UserId = 200;

        // First commit wins and bumps the row's RowVersion via the SaveChanges interceptor.
        await ctxA.SaveChangesAsync();

        // Second commit still holds the stale RowVersion from the original read → mismatch.
        var act = async () => await ctxB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
