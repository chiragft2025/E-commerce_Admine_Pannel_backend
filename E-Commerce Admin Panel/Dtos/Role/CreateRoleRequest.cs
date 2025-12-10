namespace E_Commerce_Admin_Panel.Dtos.Role
{
    public class CreateRoleRequest
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
