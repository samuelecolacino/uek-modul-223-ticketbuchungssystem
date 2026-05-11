using BCrypt.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Tests.Infrastructure;

internal sealed class TicketShopApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataSource = $"file:loadtest-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection? _keepAlive;

    public string LoadTestUsername { get; } = "loaduser";
    public string LoadTestPassword { get; } = "loadpass";
    public int SeededTicketCount { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            _keepAlive ??= OpenConnection();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(OpenConnection());
                options.AddInterceptors(new RowVersionInterceptor());
            });
        });
    }

    public void SeedLoadTestData(int ticketCount)
    {
        SeededTicketCount = ticketCount;

        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Database.EnsureCreated();

        if (!ctx.Users.Any(u => u.Username == LoadTestUsername))
        {
            ctx.Users.Add(new User
            {
                Username = LoadTestUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(LoadTestPassword),
                Role = "User"
            });
        }

        if (!ctx.TicketCategories.Any())
        {
            ctx.TicketCategories.Add(new TicketCategory { Id = 1, Name = "Load", Price = 1m });
        }

        ctx.SaveChanges();

        if (!ctx.Tickets.Any())
        {
            var tickets = Enumerable.Range(1, ticketCount)
                .Select(_ => new Ticket { TicketCategoryId = 1 })
                .ToList();
            ctx.Tickets.AddRange(tickets);
            ctx.SaveChanges();
        }
    }

    public int CountSoldTickets()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return ctx.Tickets.Count(t => t.IsSold);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive?.Dispose();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"DataSource={_dataSource}");
        connection.Open();
        return connection;
    }
}
