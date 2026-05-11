namespace TicketShop.Core.Entities;

public class TicketCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAdminOnly { get; set; }
}
