using System.ComponentModel.DataAnnotations;

namespace TicketShop.Core.Entities;

public class Ticket
{
    public int Id { get; set; }
    public int TicketCategoryId { get; set; }
    public TicketCategory Category { get; set; } = null!;
    public bool IsSold { get; set; } = false;
    public int? UserId { get; set; }
    public User? User { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
