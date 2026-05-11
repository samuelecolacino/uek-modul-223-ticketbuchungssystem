using Microsoft.EntityFrameworkCore;
using TicketShop.Core.Entities;

namespace TicketShop.Infrastructure.Persistence;

public static class DbSeeder
{
    private const int TicketsPerCategory = 50;
    private const int TicketsPerAdminCategory = 10;

    //KI GENERIERTER SEEDER - FÜR TESTZWECKE
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

        await EnsureCategoryAsync(db, "VIP", price: 150.00m, isAdminOnly: false, TicketsPerCategory, ct);
        await EnsureCategoryAsync(db, "Standard", price: 80.00m, isAdminOnly: false, TicketsPerCategory, ct);
        await EnsureCategoryAsync(db, "Mitglieder Rabatt (Admin)", price: 40.00m, isAdminOnly: true, TicketsPerAdminCategory, ct);
    }

    private static async Task EnsureCategoryAsync(
        AppDbContext db,
        string name,
        decimal price,
        bool isAdminOnly,
        int ticketCount,
        CancellationToken ct)
    {
        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Name == name, ct);
        if (category is null)
        {
            category = new TicketCategory { Name = name, Price = price, IsAdminOnly = isAdminOnly };
            db.TicketCategories.Add(category);
            await db.SaveChangesAsync(ct);
        }

        var existingTickets = await db.Tickets.CountAsync(t => t.TicketCategoryId == category.Id, ct);
        if (existingTickets == 0)
        {
            for (var i = 0; i < ticketCount; i++)
            {
                db.Tickets.Add(new Ticket
                {
                    TicketCategoryId = category.Id,
                    IsSold = false
                });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
