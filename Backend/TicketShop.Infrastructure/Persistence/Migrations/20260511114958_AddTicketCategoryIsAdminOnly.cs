using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCategoryIsAdminOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminOnly",
                table: "TicketCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdminOnly",
                table: "TicketCategories");
        }
    }
}
