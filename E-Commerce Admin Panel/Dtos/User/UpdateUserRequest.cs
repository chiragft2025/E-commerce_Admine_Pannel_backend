namespace E_Commerce_Admin_Panel.Dtos.User
{
    public class UpdateUserRequest
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; } // if supplied, password will be reset
        public bool? IsActive { get; set; }

    }
}
