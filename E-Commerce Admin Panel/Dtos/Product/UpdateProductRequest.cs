namespace E_Commerce_Admin_Panel.Dtos.Product
{
    public class UpdateProductRequest
    {
        public string? Name { get; set; }
        public string? SKU { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? Stock { get; set; }
        public bool? IsActive { get; set; }
        public long? CategoryId { get; set; }

        // <- renamed to "Tags" to match controller
        public List<ProductTagDto>? Tags { get; set; }
    }
}
