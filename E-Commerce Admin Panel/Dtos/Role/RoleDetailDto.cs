using E_Commerce_Admin_Panel.Controllers;

namespace E_Commerce_Admin_Panel.Dtos.Role
{
    public class RoleDetailDto : RoleDto
    {
        public List<PermissionDto> Permissions { get; set; } = new();
    }
}
