namespace E_Commerce_Admin_Panel.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
    }
}
