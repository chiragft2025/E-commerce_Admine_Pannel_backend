namespace E_Commerce_Admin_Panel.Dtos.Customer
{
    public class CustomerDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTimeOffset? LastModifiedAt { get; set; }
    }
}
