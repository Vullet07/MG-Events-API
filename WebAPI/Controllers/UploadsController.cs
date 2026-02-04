using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuthUserService;

namespace WebAPI.Controllers
{
    [Route("api/uploads")]
    [ApiController]
    [Authorize]
    public class UploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IAuthUserService _authUser;

        public UploadsController(IWebHostEnvironment env, IAuthUserService authUser)
        {
            _env = env;
            _authUser = authUser;
        }

        [HttpPost("profile-photo")]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = _authUser.Id?.ToString() ?? "unknown";
            var relativePath = Path.Combine("uploads", "users", userId);
            var savePath = Path.Combine(_env.WebRootPath ?? "wwwroot", relativePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(savePath, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/{relativePath.Replace("\\\\", "/")}/{fileName}";
            return Ok(new { url });
        }

        [HttpPost("forum-post-photo")]
        public async Task<IActionResult> UploadForumPostPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = _authUser.Id?.ToString() ?? "unknown";
            var relativePath = Path.Combine("uploads", "posts", userId);
            var savePath = Path.Combine(_env.WebRootPath ?? "wwwroot", relativePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(savePath, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/{relativePath.Replace("\\\\", "/")}/{fileName}";
            return Ok(new { url });
        }
    }
}
