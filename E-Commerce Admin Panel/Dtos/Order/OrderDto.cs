namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class OrderDto
    {
        public long Id { get; set; }
        public DateTimeOffset PlacedAt { get; set; }
        public string Status { get; set; } = default!;
        public decimal TotalAmount { get; set; }
        public CustomerDetailDto Customer { get; set; } = new CustomerDetailDto();
        public IEnumerable<OrderItemDto> Items { get; set; } = Enumerable.Empty<OrderItemDto>();
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
