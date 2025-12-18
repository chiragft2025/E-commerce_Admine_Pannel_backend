using System.ComponentModel.DataAnnotations;

namespace E_Commerce_Admin_Panel.Dtos.Auth
{
    public class ResetPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Otp { get; set; } = null!;

        [Required, MinLength(6)]
        public string NewPassword { get; set; } = null!;
    }
}
