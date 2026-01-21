using Services.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class ResetPasswordDto
    {
        public required string Token { get; set; } = null!;
        [PasswordPolicy]
        public required string NewPassword { get; set; } = null!;
    }
}
