using Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.JwtService
{
    public interface ITokenService
    {
        TokenResult GenerateToken(string userId, string role, string userName);
    }
}
