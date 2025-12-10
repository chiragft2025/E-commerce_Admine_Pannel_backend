namespace E_Commerce_Admin_Panel.Dtos.User
{
    public class UserListItemDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = default!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    }
}
