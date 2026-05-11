using Microsoft.EntityFrameworkCore;
using TicketShop.Core.Entities;

namespace TicketShop.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Username).IsUnique();
            b.Property(u => u.Username).IsRequired().HasMaxLength(64);
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.Role).IsRequired().HasMaxLength(32);
        });

        modelBuilder.Entity<TicketCategory>(b =>
        {
            b.Property(c => c.Name).IsRequired().HasMaxLength(64);
            b.Property(c => c.Price).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Ticket>(b =>
        {
            b.HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey(t => t.TicketCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Property(t => t.RowVersion).IsRowVersion();

            if (Database.ProviderName?.EndsWith(".Sqlite", StringComparison.Ordinal) == true)
            {
                b.Property(t => t.RowVersion).ValueGeneratedNever();
            }
        });
    }
}
