namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class CreateOrderRequest
    {
        public long CustomerId { get; set; }
        public string? ShippingAddress { get; set; }
        public List<OrderItemDto>? Items { get; set; }
    }
}
