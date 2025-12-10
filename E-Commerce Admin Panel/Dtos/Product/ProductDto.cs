namespace E_Commerce_Admin_Panel.Dtos.Product
{
    public class ProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string? SKU { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public CategoryBriefDto? Category { get; set; }
        public IEnumerable<TagDto> Tags { get; set; } = Enumerable.Empty<TagDto>();
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTimeOffset? LastModifiedAt { get; set; }
    }
}
