using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using Services.JwtService;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAuthUserService _authUserService;
        private readonly AppDbContext _db;

        public AuthController(ITokenService tokenService, IPasswordHasher<User> passwordHasher, IAuthUserService authUserService, AppDbContext db)
        {
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _authUserService = authUserService;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null)
                return BadRequest("Missing login data.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null)
                return Unauthorized("Invalid username or password.");

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

            if (verifyResult == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid username or password.");

            var token = _tokenService.GenerateToken(user.Id.ToString(), user.Role.ToString(), user.Username);

            return Ok(new { token });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return Conflict("Username already exists.");

            var user = new User
            {
                Username = dto.Username,
                Role = Role.Student,
                PhotoUrl = dto.PhotoUrl,
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            });
        }
       
        [Authorize]
        [HttpGet("me")]
        public ActionResult<UserDto> Me()
        {
            if (!_authUserService.IsAuthenticated || _authUserService.Id == null)
                return Unauthorized();

            return Ok(new UserDto
            {
                Id = _authUserService.Id.Value,
                Username = _authUserService.Username!,
                Role = _authUserService.Role ?? Role.Student,
                PhotoUrl = null
            });
        }
    }
}
