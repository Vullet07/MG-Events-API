using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Services.PasswordResetService.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<SmtpSettings> options,
            ILogger<EmailService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var message = new MailMessage
            {
                From = new MailAddress(
                    _settings.FromEmail,
                    _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(to);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(
                    _settings.Username,
                    _settings.Password),
                EnableSsl = true
            };

            try
            {
                await client.SendMailAsync(message);

                _logger.LogInformation(
                    "Email sent to {Email} with subject {Subject}",
                    to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Email}",
                    to);

                throw; // Let global exception middleware handle it
            }
        }
    }
}
