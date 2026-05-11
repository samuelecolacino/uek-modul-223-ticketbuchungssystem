using Microsoft.EntityFrameworkCore;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Persistence;

namespace TicketShop.Tests.Infrastructure;

internal class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>(b =>
        {
            b.Property(t => t.RowVersion).ValueGeneratedNever();
        });
    }
}
