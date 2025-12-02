namespace E_Commerce_Admin_Panel.Dtos.Customer
{
    public class CreateCustomerRequest
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }
}
