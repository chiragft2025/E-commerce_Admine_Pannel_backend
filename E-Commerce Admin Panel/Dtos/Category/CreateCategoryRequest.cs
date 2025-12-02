namespace E_Commerce_Admin_Panel.Dtos.Category
{
    public class CreateCategoryRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
    }
}
