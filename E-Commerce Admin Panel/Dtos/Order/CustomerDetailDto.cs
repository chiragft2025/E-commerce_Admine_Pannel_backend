namespace E_Commerce_Admin_Panel.Dtos.Order
{
    public class CustomerDetailDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = default!;
        public string? Email { get; set; }
    }
}
