using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/event-pins")]
    public class EventPinsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;

        public EventPinsController(AppDbContext db, IAuthUserService authUser, IWebHostEnvironment env)
        {
            _db = db;
            _authUser = authUser;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var pins = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new EventPinDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    PhotoUrl = p.PhotoUrl,
                    CreatedAt = p.CreatedAt,
                    CreatedByUserId = p.CreatedByUser.Id,
                    CreatedByUsername = p.CreatedByUser.Username
                })
                .ToListAsync();

            return ToApiValidationSuccess(pins);
        }

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateEventPinForm dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Title is required.");

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Authenticated user not found.", 401);

            var pin = new EventPin
            {
                Title = dto.Title,
                Description = dto.Description,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                PhotoUrl = await SavePhotoAsync(dto.Photo),
                CreatedByUser = user
            };

            _db.EventPins.Add(pin);
            await _db.SaveChangesAsync();

            var response = new EventPinDto
            {
                Id = pin.Id,
                Title = pin.Title,
                Description = pin.Description,
                Latitude = pin.Latitude,
                Longitude = pin.Longitude,
                PhotoUrl = pin.PhotoUrl,
                CreatedAt = pin.CreatedAt,
                CreatedByUserId = user.Id,
                CreatedByUsername = user.Username
            };

            return ToApiValidationSuccess(response, "Pin created.");
        }

        private async Task<string?> SavePhotoAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            var userId = _authUser.Id?.ToString() ?? "unknown";
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativePath = Path.Combine("uploads", "pins", userId);
            var savePath = Path.Combine(webRoot, relativePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(savePath, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/{relativePath.Replace("\\\\", "/")}/{fileName}";
        }
    }
}
