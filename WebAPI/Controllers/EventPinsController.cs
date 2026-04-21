using System.Globalization;
using System.Net;
using System.Text;
using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using Services.Maps;
using WebAPI.Extensions;
using WebAPI.Models;
using WebAPI.Services.Quotas;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/event-pins")]
    public class EventPinsController : ApiControllerBase
    {
        private const int ResolveConfirmationThreshold = 3;
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;
        private readonly IUserActionQuotaService _quotaService;

        public EventPinsController(
            AppDbContext db,
            IAuthUserService authUser,
            IWebHostEnvironment env,
            IUserActionQuotaService quotaService)
        {
            _db = db;
            _authUser = authUser;
            _env = env;
            _quotaService = quotaService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status = null)
        {
            var normalizedStatus = NormalizeStatusFilter(status);
            var currentUserId = _authUser.Id;
            var query = ApplyStatusFilter(_db.EventPins.AsQueryable(), normalizedStatus);

            var pins = await query
                .Include(p => p.CreatedByUser)
                .Include(p => p.ResolvedByUser)
                .Select(p => new
                {
                    Pin = p,
                    Upvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Down),
                    ResolveConfirmationCount = _db.EventPinResolveConfirmations.Count(c => c.PinId == p.Id),
                    HasCurrentUserResolveConfirmation = currentUserId != null
                        && _db.EventPinResolveConfirmations.Any(c => c.PinId == p.Id && c.UserId == currentUserId.Value),
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

            var response = pins
                .Select(x => ToPinDto(
                    x.Pin,
                    x.Upvotes,
                    x.Downvotes,
                    x.MyVote,
                    resolveConfirmationCount: x.ResolveConfirmationCount,
                    hasCurrentUserResolveConfirmation: x.HasCurrentUserResolveConfirmation))
                .ToList();

            return ToApiValidationSuccess(response);
        }

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateEventPinForm dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Заглавието е задължително.");

            var category = NormalizeCategory(dto.Category);
            if (category == null)
                return ToApiValidationFail("Категорията на пина е задължителна.");

            if (dto.Photo != null && !dto.Photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return ToApiValidationFail("Към пин може да се качва само изображение.", 400);

            if (!IndoorMapGeometry.TryResolveZone(dto.Latitude, dto.Longitude, out var zone))
                return ToApiValidationFail("Пинът трябва да бъде поставен във валидна зона от картата.", 400);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Липсва удостоверен потребител.", 401);

            var quota = await _quotaService.CheckAsync(user.Id, UserActionQuotaType.EventPinCreate);
            if (!quota.Allowed)
                return ToQuotaFail(quota);

            var pin = new EventPin
            {
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Category = category,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                PhotoUrl = await SavePhotoAsync(dto.Photo),
                CreatedByUser = user,
                IsResolved = false,
                ResolvedAt = null,
                ResolvedByUserId = null,
                ArchivedAt = null
            };

            _db.EventPins.Add(pin);
            await _db.SaveChangesAsync();
            await _quotaService.RecordAsync(user.Id, UserActionQuotaType.EventPinCreate);

            return ToApiValidationSuccess(
                await BuildPinDtoAsync(pin, zone),
                "Пинът е създаден успешно.");
        }

        [Authorize]
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateEventPinForm dto)
        {
            var pin = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .Include(p => p.ResolvedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pin == null)
                return ToApiValidationFail("Пинът не е намерен.", 404);

            if (!CanManagePin(pin))
                return ToApiValidationFail("Нямаш права да редактираш този пин.", 403);

            if (string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Заглавието е задължително.");

            var category = NormalizeCategory(dto.Category);
            if (category == null)
                return ToApiValidationFail("Категорията на пина е задължителна.");

            if (dto.Photo != null && !dto.Photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return ToApiValidationFail("Към пин може да се качва само изображение.", 400);

            pin.Title = dto.Title.Trim();
            pin.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            pin.Category = category;

            if (dto.RemovePhoto)
            {
                DeleteLocalMedia(pin.PhotoUrl);
                pin.PhotoUrl = null;
            }

            if (dto.Photo != null)
            {
                DeleteLocalMedia(pin.PhotoUrl);
                pin.PhotoUrl = await SavePhotoAsync(dto.Photo);
            }

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(
                await BuildPinDtoAsync(pin),
                "Пинът е обновен.");
        }

        [Authorize]
        [HttpPost("{id:int}/resolve")]
        public async Task<IActionResult> Resolve(int id)
        {
            var pin = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .Include(p => p.ResolvedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pin == null)
                return ToApiValidationFail("Пинът не е намерен.", 404);

            if (pin.IsResolved)
                return ToApiValidationFail("Пинът вече е маркиран като решен.", 400);

            if (_authUser.Id == null)
                return ToApiValidationFail("Липсва удостоверен потребител.", 401);

            var currentUser = await _db.Users.FindAsync(_authUser.Id.Value);
            if (currentUser == null)
                return ToApiValidationFail("Потребителят не е намерен.", 401);

            var existingConfirmation = await _db.EventPinResolveConfirmations
                .FirstOrDefaultAsync(c => c.PinId == pin.Id && c.UserId == currentUser.Id);

            if (existingConfirmation != null)
                return ToApiValidationFail("Вече потвърди, че този пин е разрешен.", 400);

            _db.EventPinResolveConfirmations.Add(new EventPinResolveConfirmation
            {
                PinId = pin.Id,
                Pin = pin,
                UserId = currentUser.Id,
                User = currentUser
            });

            await _db.SaveChangesAsync();

            var confirmationCount = await _db.EventPinResolveConfirmations.CountAsync(c => c.PinId == pin.Id);
            if (confirmationCount >= ResolveConfirmationThreshold)
            {
                await ResolvePinInternalAsync(pin, currentUser);
                await _db.SaveChangesAsync();

                return ToApiValidationSuccess(
                    await BuildPinDtoAsync(pin),
                    $"Достигнат е прагът от {ResolveConfirmationThreshold} потвърждения и пинът е премахнат от активната карта.");
            }

            return ToApiValidationSuccess(
                await BuildPinDtoAsync(pin),
                $"Потвърждението е записано ({confirmationCount}/{ResolveConfirmationThreshold}).");
        }

        [Authorize]
        [HttpPost("{id:int}/unresolve")]
        public async Task<IActionResult> Unresolve(int id)
        {
            var pin = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .Include(p => p.ResolvedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pin == null)
                return ToApiValidationFail("Пинът не е намерен.", 404);

            if (!CanManagePin(pin))
                return ToApiValidationFail("Нямаш права да върнеш този пин като активен.", 403);

            if (!pin.IsResolved)
                return ToApiValidationFail("Пинът не е маркиран като решен.", 400);

            pin.IsResolved = false;
            pin.ResolvedAt = null;
            pin.ArchivedAt = null;
            pin.ResolvedByUserId = null;
            pin.ResolvedByUser = null;
            var confirmations = await _db.EventPinResolveConfirmations
                .Where(c => c.PinId == pin.Id)
                .ToListAsync();

            if (confirmations.Count > 0)
            {
                _db.EventPinResolveConfirmations.RemoveRange(confirmations);
            }

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(await BuildPinDtoAsync(pin), "Пинът отново е активен.");
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var pin = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pin == null)
                return ToApiValidationFail("Пинът не е намерен.", 404);

            if (!CanManagePin(pin))
                return ToApiValidationFail("Нямаш права да изтриеш този пин.", 403);

            var relatedReports = await _db.Reports
                .Where(r => r.TargetType == ReportTargetType.Pin && r.TargetId == id)
                .ToListAsync();

            if (relatedReports.Count > 0)
            {
                _db.Reports.RemoveRange(relatedReports);
            }

            DeleteLocalMedia(pin.PhotoUrl);
            _db.EventPins.Remove(pin);
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Пинът е изтрит успешно.");
        }

        [Authorize]
        [HttpPost("{id:int}/vote")]
        public async Task<IActionResult> Vote(int id, [FromBody] VoteRequestDto dto)
        {
            if (dto.Value != 1 && dto.Value != -1)
                return ToApiValidationFail("Гласът трябва да е 1 или -1.", 400);

            var pin = await _db.EventPins.FirstOrDefaultAsync(p => p.Id == id && !p.IsResolved);
            if (pin == null)
                return ToApiValidationFail("Пинът не е намерен.", 404);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Потребителят не е намерен.", 401);

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

            return ToApiValidationSuccess("Гласът е обновен.");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("reports/monthly")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] string? month = null)
        {
            var report = await BuildMonthlyReportAsync(month);
            return ToApiValidationSuccess(report);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("reports/monthly/export")]
        public async Task<IActionResult> ExportMonthlyReport([FromQuery] string? month = null)
        {
            var report = await BuildMonthlyReportAsync(month);
            var html = BuildWordCompatibleHtml(report);
            var bytes = Encoding.UTF8.GetBytes(html);

            return File(
                bytes,
                "application/msword",
                $"mg-akademik-kiril-popov-pin-report-{report.MonthKey}.doc");
        }

        private async Task<PinMonthlyReportDto> BuildMonthlyReportAsync(string? monthKey)
        {
            var (periodStartLocal, periodStart, periodEnd) = ParseMonthRange(monthKey);

            var rawPins = await _db.EventPins
                .Include(p => p.CreatedByUser)
                .Where(p => p.CreatedAt >= periodStart && p.CreatedAt < periodEnd)
                .Select(p => new
                {
                    Pin = p,
                    Upvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Down)
                })
                .ToListAsync();

            var decoratedPins = rawPins
                .Select(item =>
                {
                    IndoorMapGeometry.TryResolveZone(item.Pin.Latitude, item.Pin.Longitude, out var zone);
                    var layerLabel = zone?.LayerLabel ?? "Неизвестен слой";
                    var zoneLabel = zone?.ZoneLabel ?? "Неизвестна зона";
                    var zoneKind = zone?.ZoneKind ?? "zone";
                    var score = item.Upvotes - item.Downvotes;

                    return new
                    {
                        item.Pin,
                        item.Upvotes,
                        item.Downvotes,
                        Score = score,
                        LayerLabel = layerLabel,
                        ZoneLabel = zoneLabel,
                        ZoneKind = zoneKind
                    };
                })
                .ToList();

            var culture = CultureInfo.GetCultureInfo("bg-BG");
            var report = new PinMonthlyReportDto
            {
                MonthKey = periodStartLocal.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                MonthLabel = culture.TextInfo.ToTitleCase(periodStartLocal.ToString("MMMM yyyy", culture)),
                GeneratedAt = DateTime.UtcNow,
                TotalPins = decoratedPins.Count,
                PinsWithPhotos = decoratedPins.Count(item => !string.IsNullOrWhiteSpace(item.Pin.PhotoUrl)),
                ActiveZones = decoratedPins
                    .Select(item => $"{item.LayerLabel}::{item.ZoneLabel}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                Hotspots = decoratedPins
                    .GroupBy(item => new { item.LayerLabel, item.ZoneLabel, item.ZoneKind })
                    .Select(group => new PinHotspotDto
                    {
                        LayerLabel = group.Key.LayerLabel,
                        ZoneLabel = group.Key.ZoneLabel,
                        ZoneKind = group.Key.ZoneKind,
                        PinsCount = group.Count(),
                        TotalScore = group.Sum(item => item.Score),
                        HighestScore = group.Max(item => item.Score),
                        DominantCategory = group
                            .GroupBy(item => item.Pin.Category)
                            .OrderByDescending(categoryGroup => categoryGroup.Count())
                            .ThenByDescending(categoryGroup => categoryGroup.Sum(item => item.Score))
                            .Select(categoryGroup => categoryGroup.Key)
                            .FirstOrDefault() ?? "Без категория",
                        LatestPinAt = group.Max(item => item.Pin.CreatedAt)
                    })
                    .OrderByDescending(item => item.TotalScore)
                    .ThenByDescending(item => item.PinsCount)
                    .ThenBy(item => item.LayerLabel)
                    .Take(12)
                    .ToList(),
                Categories = decoratedPins
                    .GroupBy(item => item.Pin.Category)
                    .Select(group => new PinCategoryStatDto
                    {
                        Category = group.Key,
                        PinsCount = group.Count(),
                        TotalScore = group.Sum(item => item.Score)
                    })
                    .OrderByDescending(item => item.TotalScore)
                    .ThenByDescending(item => item.PinsCount)
                    .ThenBy(item => item.Category)
                    .ToList(),
                TopPins = decoratedPins
                    .OrderByDescending(item => item.Score)
                    .ThenByDescending(item => item.Pin.CreatedAt)
                    .Take(20)
                    .Select(item => new PinReportItemDto
                    {
                        Id = item.Pin.Id,
                        Title = item.Pin.Title,
                        Category = item.Pin.Category,
                        LayerLabel = item.LayerLabel,
                        ZoneLabel = item.ZoneLabel,
                        CreatedByUsername = item.Pin.CreatedByUser.Username,
                        Score = item.Score,
                        CreatedAt = item.Pin.CreatedAt
                    })
                    .ToList()
            };

            return report;
        }

        private static IQueryable<EventPin> ApplyStatusFilter(IQueryable<EventPin> query, string status) => status switch
        {
            "resolved" => query.Where(p => p.IsResolved),
            "all" => query,
            _ => query.Where(p => !p.IsResolved)
        };

        private static string NormalizeStatusFilter(string? status)
        {
            var normalized = status?.Trim().ToLowerInvariant();
            return normalized is "resolved" or "all" ? normalized : "active";
        }

        private static (DateTime LocalStart, DateTime UtcStart, DateTime UtcEnd) ParseMonthRange(string? monthKey)
        {
            DateTime localStart;

            if (!string.IsNullOrWhiteSpace(monthKey)
                && DateTime.TryParseExact(
                    $"{monthKey}-01",
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                localStart = new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            }
            else
            {
                var now = ToBulgariaTime(DateTime.UtcNow);
                localStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            }

            var nextLocalStart = localStart.AddMonths(1);
            return (
                localStart,
                TimeZoneInfo.ConvertTimeToUtc(localStart, BulgariaTimeZone),
                TimeZoneInfo.ConvertTimeToUtc(nextLocalStart, BulgariaTimeZone));
        }

        private async Task<EventPinDto> BuildPinDtoAsync(EventPin pin, ResolvedMapZone? resolvedZone = null)
        {
            var currentUserId = _authUser.Id;
            var upvotes = await _db.PinVotes.CountAsync(v => v.Pin.Id == pin.Id && v.Value == VoteValue.Up);
            var downvotes = await _db.PinVotes.CountAsync(v => v.Pin.Id == pin.Id && v.Value == VoteValue.Down);
            var myVote = currentUserId == null
                ? 0
                : await _db.PinVotes
                    .Where(v => v.Pin.Id == pin.Id && v.User.Id == currentUserId.Value)
                    .Select(v => (int?)v.Value)
                    .FirstOrDefaultAsync() ?? 0;
            var resolveConfirmationCount = await _db.EventPinResolveConfirmations.CountAsync(c => c.PinId == pin.Id);
            var hasCurrentUserResolveConfirmation = currentUserId != null
                && await _db.EventPinResolveConfirmations.AnyAsync(c => c.PinId == pin.Id && c.UserId == currentUserId.Value);

            return ToPinDto(
                pin,
                upvotes,
                downvotes,
                myVote,
                resolvedZone,
                resolveConfirmationCount,
                hasCurrentUserResolveConfirmation);
        }

        private EventPinDto ToPinDto(
            EventPin pin,
            int upvotes,
            int downvotes,
            int myVote,
            ResolvedMapZone? resolvedZone = null,
            int resolveConfirmationCount = 0,
            bool hasCurrentUserResolveConfirmation = false)
        {
            resolvedZone ??= IndoorMapGeometry.TryResolveZone(pin.Latitude, pin.Longitude, out var zone)
                ? zone
                : null;

            return new EventPinDto
            {
                Id = pin.Id,
                Title = pin.Title,
                Description = pin.Description,
                Category = pin.Category,
                Latitude = pin.Latitude,
                Longitude = pin.Longitude,
                PhotoUrl = pin.PhotoUrl,
                CreatedAt = pin.CreatedAt,
                CreatedByUserId = pin.CreatedByUser.Id,
                CreatedByUsername = pin.CreatedByUser.Username,
                IsResolved = pin.IsResolved,
                ResolvedAt = pin.ResolvedAt,
                ResolvedByUserId = pin.ResolvedByUserId,
                ResolvedByUsername = pin.ResolvedByUser?.Username,
                ArchivedAt = pin.ArchivedAt,
                ResolveConfirmationCount = resolveConfirmationCount,
                ResolveThreshold = ResolveConfirmationThreshold,
                HasCurrentUserResolveConfirmation = hasCurrentUserResolveConfirmation,
                LayerId = resolvedZone?.LayerId ?? "unknown",
                LayerLabel = resolvedZone?.LayerLabel ?? "Неизвестен слой",
                ZoneId = resolvedZone?.ZoneId ?? "unknown",
                ZoneLabel = resolvedZone?.ZoneLabel ?? "Неизвестна зона",
                ZoneKind = resolvedZone?.ZoneKind ?? "zone",
                Upvotes = upvotes,
                Downvotes = downvotes,
                Score = upvotes - downvotes,
                MyVote = myVote
            };
        }

        private Task ResolvePinInternalAsync(EventPin pin, User resolvedBy)
        {
            var resolvedAt = DateTime.UtcNow;
            pin.IsResolved = true;
            pin.ResolvedAt = resolvedAt;
            pin.ArchivedAt = resolvedAt;
            pin.ResolvedByUserId = resolvedBy.Id;
            pin.ResolvedByUser = resolvedBy;
            return Task.CompletedTask;
        }

        private bool CanManagePin(EventPin pin)
        {
            if (_authUser.Id == null)
            {
                return false;
            }

            return _authUser.Id == pin.CreatedByUser.Id
                || _authUser.Role == Role.Admin
                || _authUser.Role == Role.Teacher;
        }

        private async Task<string?> SavePhotoAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Към пин може да се качва само изображение.");

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
            return $"{baseUrl}/{relativePath.Replace("\\", "/")}/{fileName}";
        }

        private void DeleteLocalMedia(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            string relativePath;

            if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var absoluteUri))
            {
                relativePath = absoluteUri.AbsolutePath.TrimStart('/');
            }
            else
            {
                relativePath = mediaUrl.TrimStart('/', '\\');
            }

            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
            var rootPath = Path.GetFullPath(webRoot);

            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private static string? NormalizeCategory(string? rawCategory)
        {
            var trimmed = rawCategory?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private IActionResult ToQuotaFail(ActionQuotaCheckResult quota)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(quota.RetryAfter.TotalSeconds));
            Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse.Fail(quota.Message));
        }

        private static string BuildWordCompatibleHtml(PinMonthlyReportDto report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<html><head><meta charset=\"utf-8\" />");
            builder.AppendLine("<style>");
            builder.AppendLine("body{font-family:Calibri,Arial,sans-serif;color:#1f2937;padding:24px;} h1,h2{color:#991b1b;} table{border-collapse:collapse;width:100%;margin:16px 0;} th,td{border:1px solid #d1d5db;padding:8px;text-align:left;} th{background:#fee2e2;} .meta{margin-bottom:20px;color:#4b5563;} .pill-row{margin-top:12px;line-height:1.8;} .pill{display:inline-block;padding:4px 10px;border-radius:999px;background:#fee2e2;color:#991b1b;margin:0 8px 8px 0;}");
            builder.AppendLine("</style></head><body>");
            builder.AppendLine($"<h1>{Encode(report.SchoolName)}</h1>");
            builder.AppendLine($"<div class=\"meta\"><strong>Месечен отчет за пинове</strong> · {Encode(report.MonthLabel)} · Генериран на {Encode(FormatBulgariaTime(report.GeneratedAt))}</div>");
            builder.AppendLine($"<div class=\"pill-row\"><span class=\"pill\">Общо пинове: {report.TotalPins}</span> <span class=\"pill\">Пинове със снимка: {report.PinsWithPhotos}</span> <span class=\"pill\">Активни зони: {report.ActiveZones}</span></div>");

            builder.AppendLine("<h2>Най-проблемни места</h2><table><thead><tr><th>Слой</th><th>Зона</th><th>Категория</th><th>Брой пинове</th><th>Общ рейтинг</th><th>Последен пин</th></tr></thead><tbody>");
            foreach (var hotspot in report.Hotspots)
            {
                builder.AppendLine($"<tr><td>{Encode(hotspot.LayerLabel)}</td><td>{Encode(hotspot.ZoneLabel)}</td><td>{Encode(hotspot.DominantCategory)}</td><td>{hotspot.PinsCount}</td><td>{hotspot.TotalScore}</td><td>{Encode(FormatBulgariaTime(hotspot.LatestPinAt))}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("<h2>Категории</h2><table><thead><tr><th>Категория</th><th>Брой пинове</th><th>Общ рейтинг</th></tr></thead><tbody>");
            foreach (var category in report.Categories)
            {
                builder.AppendLine($"<tr><td>{Encode(category.Category)}</td><td>{category.PinsCount}</td><td>{category.TotalScore}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("<h2>Най-актуални пинове с висок рейтинг</h2><table><thead><tr><th>Заглавие</th><th>Категория</th><th>Локация</th><th>Автор</th><th>Рейтинг</th><th>Дата</th></tr></thead><tbody>");
            foreach (var item in report.TopPins)
            {
                builder.AppendLine($"<tr><td>{Encode(item.Title)}</td><td>{Encode(item.Category)}</td><td>{Encode($"{item.LayerLabel} · {item.ZoneLabel}")}</td><td>{Encode(item.CreatedByUsername)}</td><td>{item.Score}</td><td>{Encode(FormatBulgariaTime(item.CreatedAt))}</td></tr>");
            }

            builder.AppendLine("</tbody></table></body></html>");
            return builder.ToString();
        }

        private static readonly TimeZoneInfo BulgariaTimeZone = ResolveBulgariaTimeZone();

        private static TimeZoneInfo ResolveBulgariaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            }
        }

        private static DateTime ToBulgariaTime(DateTime value)
        {
            var utcValue = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcValue, BulgariaTimeZone);
        }

        private static string FormatBulgariaTime(DateTime value)
            => ToBulgariaTime(value).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("bg-BG"));

        private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
