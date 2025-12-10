namespace E_Commerce_Admin_Panel.Dtos.User
{
    public class UserDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = default!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<RoleBriefDto> Roles { get; set; } = Enumerable.Empty<RoleBriefDto>();
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
