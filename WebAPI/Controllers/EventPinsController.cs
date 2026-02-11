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
            var currentUserId = _authUser.Id;
            var pins = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .Select(p => new
                {
                    Pin = p,
                    Upvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Down),
                    MyVote = currentUserId == null
                        ? 0
                        : _db.PinVotes
                            .Where(v => v.Pin.Id == p.Id && v.User.Id == currentUserId.Value)
                            .Select(v => (int?)v.Value)
                            .FirstOrDefault() ?? 0
                })
                .OrderByDescending(x => x.Upvotes - x.Downvotes)
                .ThenByDescending(x => x.Pin.CreatedAt)
                .ToListAsync();

            var response = pins.Select(x => new EventPinDto
            {
                Id = x.Pin.Id,
                Title = x.Pin.Title,
                Description = x.Pin.Description,
                Latitude = x.Pin.Latitude,
                Longitude = x.Pin.Longitude,
                PhotoUrl = x.Pin.PhotoUrl,
                CreatedAt = x.Pin.CreatedAt,
                CreatedByUserId = x.Pin.CreatedByUser.Id,
                CreatedByUsername = x.Pin.CreatedByUser.Username,
                Upvotes = x.Upvotes,
                Downvotes = x.Downvotes,
                Score = x.Upvotes - x.Downvotes,
                MyVote = x.MyVote
            }).ToList();

            return ToApiValidationSuccess(response);
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
                CreatedByUsername = user.Username,
                Upvotes = 0,
                Downvotes = 0,
                Score = 0,
                MyVote = 0
            };

            return ToApiValidationSuccess(response, "Pin created.");
        }

        [Authorize]
        [HttpPost("{id:int}/vote")]
        public async Task<IActionResult> Vote(int id, [FromBody] VoteRequestDto dto)
        {
            if (dto.Value != 1 && dto.Value != -1)
                return ToApiValidationFail("Vote must be 1 or -1.", 400);

            var pin = await _db.EventPins.FirstOrDefaultAsync(p => p.Id == id);
            if (pin == null)
                return ToApiValidationFail("Pin not found.", 404);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("User not found.", 401);

            var existing = await _db.PinVotes
                .FirstOrDefaultAsync(v => v.User.Id == user.Id && v.Pin.Id == pin.Id);

            if (existing == null)
            {
                _db.PinVotes.Add(new PinVote
                {
                    User = user,
                    Pin = pin,
                    Value = dto.Value == 1 ? VoteValue.Up : VoteValue.Down
                });
            }
            else if ((int)existing.Value == dto.Value)
            {
                _db.PinVotes.Remove(existing);
            }
            else
            {
                existing.Value = dto.Value == 1 ? VoteValue.Up : VoteValue.Down;
            }

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Vote updated.");
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
