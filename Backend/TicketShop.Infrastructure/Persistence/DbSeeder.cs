using Microsoft.EntityFrameworkCore;
using TicketShop.Core.Entities;

namespace TicketShop.Infrastructure.Persistence;

public static class DbSeeder
{
    private const int TicketsPerCategory = 50;

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                    Role = "Admin"
                },
                new User
                {
                    Username = "user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("user"),
                    Role = "User"
                });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.TicketCategories.AnyAsync(ct))
        {
            db.TicketCategories.AddRange(
                new TicketCategory { Name = "VIP", Price = 150.00m },
                new TicketCategory { Name = "Standard", Price = 80.00m });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Tickets.AnyAsync(ct))
        {
            var categories = await db.TicketCategories.ToListAsync(ct);
            foreach (var category in categories)
            {
                for (var i = 0; i < TicketsPerCategory; i++)
                {
                    db.Tickets.Add(new Ticket
                    {
                        TicketCategoryId = category.Id,
                        IsSold = false
                    });
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
