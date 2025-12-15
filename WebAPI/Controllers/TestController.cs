using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuthUserService;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IAuthUserService _authUser;

        public TestController(IAuthUserService authUser)
        {
            _authUser = authUser;
        }

        // ---------------- Public endpoint ----------------
        [AllowAnonymous]
        [HttpGet("public")]
        public IActionResult Public()
        {
            return Ok("Anyone can access this endpoint.");
        }

        // ---------------- Authenticated endpoint ----------------
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!_authUser.IsAuthenticated)
                return Unauthorized();

            return Ok(new
            {
                Id = _authUser.Id,
                Username = _authUser.Username,
                Role = _authUser.Role
            });
        }
    }
}
