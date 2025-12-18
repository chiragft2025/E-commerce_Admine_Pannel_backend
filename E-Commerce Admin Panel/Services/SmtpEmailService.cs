using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace E_Commerce_Admin_Panel.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var smtpSection = _config.GetSection("Smtp");

            var host = smtpSection["Host"];
            var port = int.Parse(smtpSection["Port"]!);
            var username = smtpSection["Username"];
            var password = smtpSection["Password"];
            var from = smtpSection["From"];
            var enableSsl = bool.Parse(smtpSection["EnableSsl"]!);

            var message = new MailMessage
            {
                From = new MailAddress(from!),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl
            };

            // Send async (important for scalability)
            await client.SendMailAsync(message);
        }
    }
}
