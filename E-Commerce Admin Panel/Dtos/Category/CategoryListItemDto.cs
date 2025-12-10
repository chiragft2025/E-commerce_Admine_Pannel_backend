namespace E_Commerce_Admin_Panel.Dtos.Category
{
    public class CategoryListItemDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
