using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.PasswordResetService
{
    public class SmtpSettings
    {
        public string Host { get; set; } = null!;
        public int Port { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string FromEmail { get; set; } = null!;
        public string FromName { get; set; } = null!;
    }
}
