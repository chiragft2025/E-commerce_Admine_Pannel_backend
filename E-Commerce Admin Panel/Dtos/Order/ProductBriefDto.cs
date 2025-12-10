namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class ProductBriefDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string? SKU { get; set; }
    }
}
