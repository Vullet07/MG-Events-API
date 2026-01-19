using Data;
using Data.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Services.AuthUserService
{
    public class AuthUserService : IAuthUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;

        private User? _cachedUser;

        public AuthUserService(
            IHttpContextAccessor httpContextAccessor,
            AppDbContext db)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        private ClaimsPrincipal? Principal =>
            _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated =>
            Principal?.Identity?.IsAuthenticated ?? false;

        private int? UserId
        {
            get
            {
                var idClaim = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(idClaim, out var id) ? id : null;
            }
        }

        private User? User
        {
            get
            {
                if (!IsAuthenticated || UserId == null)
                    return null;

                if (_cachedUser != null)
                    return _cachedUser;

                _cachedUser = _db.Users
                    .FirstOrDefault(u => u.Id == UserId && !u.IsDeleted);

                return _cachedUser;
            }
        }

        public int? Id => User?.Id;

        public string? Username => User?.Username;

        public Role? Role => User?.Role;

        public bool IsBanned =>
            User?.IsBanned == true &&
            (User.BannedUntil == null || User.BannedUntil > DateTime.UtcNow);

        public DateTime? BannedUntil => User?.BannedUntil;
    }
}
