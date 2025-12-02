using System.ComponentModel.DataAnnotations;

namespace E_Commerce_Admin_Panel.Dtos.Auth
{
    public class LoginRequest
    {
        [Required]
        public string UserName { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }
}
