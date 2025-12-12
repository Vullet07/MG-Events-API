using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AuthUserService
{
    public interface IAuthUserService
    {
        int? Id { get; }
        string? Username { get; }
        Role? Role { get; }
        bool IsAuthenticated { get; }
    }
}
