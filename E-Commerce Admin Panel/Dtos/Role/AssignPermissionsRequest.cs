namespace E_Commerce_Admin_Panel.Dtos.Role
{
    public class AssignPermissionsRequest
    {
        public List<long> PermissionIds { get; set; } = new();
    }
}
