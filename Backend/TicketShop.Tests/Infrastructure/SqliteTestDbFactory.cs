using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Tests.Infrastructure;

internal sealed class SqliteTestDbFactory : IDisposable
{
    private readonly string _dataSource = $"file:{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;
    private readonly List<SqliteConnection> _connections = new();

    public SqliteTestDbFactory()
    {
        _keepAlive = OpenConnection();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = BuildOptions();
        return new TestAppDbContext(options);
    }

    public AppDbContext CreateThrowingContext()
    {
        var options = BuildOptions();
        return new ThrowingOnSaveDbContext(options);
    }

    public void Seed(IEnumerable<TicketCategory>? categories = null, IEnumerable<Ticket>? tickets = null, IEnumerable<User>? users = null)
    {
        using var ctx = CreateContext();
        if (users is not null) ctx.Users.AddRange(users);
        if (categories is not null) ctx.TicketCategories.AddRange(categories);
        if (tickets is not null) ctx.Tickets.AddRange(tickets);
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var c in _connections)
        {
            c.Dispose();
        }
        _keepAlive.Dispose();
    }

    private DbContextOptions<AppDbContext> BuildOptions()
    {
        var connection = OpenConnection();
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"DataSource={_dataSource}");
        connection.Open();
        _connections.Add(connection);
        return connection;
    }
}
