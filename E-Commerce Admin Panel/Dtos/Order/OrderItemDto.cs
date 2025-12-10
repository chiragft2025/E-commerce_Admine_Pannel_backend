namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class OrderItemDto
    {
        public long Id { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public ProductBriefDto Product { get; set; } = new ProductBriefDto();
    }
}
