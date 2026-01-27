using Services.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class LoginDto
    {
        public required string Identifier { get; set; }
        public required string Password { get; set; }
    }
}
