namespace E_Commerce_Admin_Panel.Dtos.Role
{
    public class UpdateRoleRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
