using System.ComponentModel.DataAnnotations;

namespace E_Commerce_Admin_Panel.Dtos.User
{
    public class CreateUserRequest
    {
        [Required]
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool IsActive { get; set; } = true;

        // optional role ids to assign on create
        public List<long>? RoleIds { get; set; }
    }
}
