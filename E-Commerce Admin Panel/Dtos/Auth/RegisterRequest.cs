using System.ComponentModel.DataAnnotations;

namespace E_Commerce_Admin_Panel.Dtos.Auth
{
    public class RegisterRequest
    {
        [Required, MinLength(3)]
        public string UserName { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required, MinLength(6)]
        public string Password { get; set; } = null!;
    }
}
