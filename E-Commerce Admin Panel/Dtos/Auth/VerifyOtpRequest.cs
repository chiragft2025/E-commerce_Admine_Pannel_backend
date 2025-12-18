using System.ComponentModel.DataAnnotations;

namespace E_Commerce_Admin_Panel.Dtos.Auth
{
    public class VerifyOtpRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Otp { get; set; } = null!;
    }
}
