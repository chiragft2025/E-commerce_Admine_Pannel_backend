namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class OrderListItemDto
    {
        public long Id { get; set; }
        public DateTimeOffset PlacedAt { get; set; }
        public string Status { get; set; } = default!;
        public decimal TotalAmount { get; set; }
        public CustomerBriefDto Customer { get; set; } = new CustomerBriefDto();
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
