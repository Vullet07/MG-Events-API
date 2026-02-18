using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;

        public ReportsController(AppDbContext db, IAuthUserService authUser)
        {
            _db = db;
            _authUser = authUser;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReportDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Reason))
                return ToApiValidationFail("Invalid report payload.");

            var reporter = await _db.Users.FindAsync(_authUser.Id);
            if (reporter == null)
                return ToApiValidationFail("User not found.", 401);

            var exists = dto.TargetType switch
            {
                ReportTargetType.Post => await _db.ForumPosts.AnyAsync(x => x.Id == dto.TargetId && !x.IsDeleted),
                ReportTargetType.Thread => await _db.ForumThreads.AnyAsync(x => x.Id == dto.TargetId),
                ReportTargetType.Pin => await _db.EventPins.AnyAsync(x => x.Id == dto.TargetId),
                ReportTargetType.User => await _db.Users.AnyAsync(x => x.Id == dto.TargetId),
                _ => false
            };

            if (!exists)
                return ToApiValidationFail("Reported target not found.", 404);

            var report = new Report
            {
                Reporter = reporter,
                TargetType = dto.TargetType,
                TargetId = dto.TargetId,
                Reason = dto.Reason.Trim(),
                Details = dto.Details?.Trim()
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Report submitted.");
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            if (!_authUser.Id.HasValue)
                return ToApiValidationFail("User not authenticated.", 401);

            var myReports = await _db.Reports
                .Include(r => r.ResolvedBy)
                .Where(r => r.Reporter.Id == _authUser.Id.Value)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReportDto
                {
                    Id = r.Id,
                    TargetType = r.TargetType,
                    TargetId = r.TargetId,
                    TargetLabel = r.TargetType == ReportTargetType.Thread
                        ? _db.ForumThreads
                            .Where(t => t.Id == r.TargetId)
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? "Unknown thread"
                        : r.TargetType == ReportTargetType.Post
                            ? _db.ForumPosts
                                .Where(p => p.Id == r.TargetId)
                                .Select(p => p.Title ?? p.Content)
                                .FirstOrDefault() ?? "Unknown post"
                            : r.TargetType == ReportTargetType.Pin
                                ? _db.EventPins
                                    .Where(p => p.Id == r.TargetId)
                                    .Select(p => p.Title)
                                    .FirstOrDefault() ?? "Unknown pin"
                                : _db.Users
                                    .Where(u => u.Id == r.TargetId)
                                    .Select(u => u.Username)
                                    .FirstOrDefault() ?? "Unknown user",
                    Reason = r.Reason,
                    Details = r.Details,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ReporterId = _authUser.Id.Value,
                    ReporterUsername = _authUser.Username ?? "",
                    ResolvedAt = r.ResolvedAt,
                    ResolvedByUserId = r.ResolvedBy != null ? r.ResolvedBy.Id : null
                })
                .ToListAsync();

            return ToApiValidationSuccess(myReports);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var reports = await _db.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ResolvedBy)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.CreatedAt)
                .Select(r => new ReportDto
                {
                    Id = r.Id,
                    TargetType = r.TargetType,
                    TargetId = r.TargetId,
                    TargetLabel = r.TargetType == ReportTargetType.Thread
                        ? _db.ForumThreads
                            .Where(t => t.Id == r.TargetId)
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? "Unknown thread"
                        : r.TargetType == ReportTargetType.Post
                            ? _db.ForumPosts
                                .Where(p => p.Id == r.TargetId)
                                .Select(p => p.Title ?? p.Content)
                                .FirstOrDefault() ?? "Unknown post"
                            : r.TargetType == ReportTargetType.Pin
                                ? _db.EventPins
                                    .Where(p => p.Id == r.TargetId)
                                    .Select(p => p.Title)
                                    .FirstOrDefault() ?? "Unknown pin"
                                : _db.Users
                                    .Where(u => u.Id == r.TargetId)
                                    .Select(u => u.Username)
                                    .FirstOrDefault() ?? "Unknown user",
                    Reason = r.Reason,
                    Details = r.Details,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ReporterId = r.Reporter.Id,
                    ReporterUsername = r.Reporter.Username,
                    ResolvedAt = r.ResolvedAt,
                    ResolvedByUserId = r.ResolvedBy != null ? r.ResolvedBy.Id : null
                })
                .ToListAsync();

            return ToApiValidationSuccess(reports);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateReportStatusDto dto)
        {
            var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null)
                return ToApiValidationFail("Report not found.", 404);

            report.Status = dto.Status;
            report.ResolvedAt = dto.Status == ReportStatus.Open ? null : DateTime.UtcNow;

            if (_authUser.Id.HasValue)
                report.ResolvedBy = await _db.Users.FindAsync(_authUser.Id.Value);

            await _db.SaveChangesAsync();
            return ToApiValidationSuccess("Report status updated.");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id:int}/delete-target")]
        public async Task<IActionResult> DeleteReportedTarget(int id)
        {
            var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null)
                return ToApiValidationFail("Report not found.", 404);

            string message;
            switch (report.TargetType)
            {
                case ReportTargetType.Post:
                    var post = await _db.ForumPosts.FirstOrDefaultAsync(p => p.Id == report.TargetId);
                    if (post == null)
                        return ToApiValidationFail("Reported post not found.", 404);
                    post.IsDeleted = true;
                    post.UpdatedAt = DateTime.UtcNow;
                    message = "Reported post deleted.";
                    break;

                case ReportTargetType.Thread:
                    var thread = await _db.ForumThreads.FirstOrDefaultAsync(t => t.Id == report.TargetId);
                    if (thread == null)
                        return ToApiValidationFail("Reported thread not found.", 404);
                    _db.ForumThreads.Remove(thread);
                    message = "Reported thread deleted.";
                    break;

                case ReportTargetType.Pin:
                    var pin = await _db.EventPins.FirstOrDefaultAsync(p => p.Id == report.TargetId);
                    if (pin == null)
                        return ToApiValidationFail("Reported pin not found.", 404);
                    _db.EventPins.Remove(pin);
                    message = "Reported pin deleted.";
                    break;

                case ReportTargetType.User:
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == report.TargetId);
                    if (user == null)
                        return ToApiValidationFail("Reported user not found.", 404);
                    if (user.Role == Role.Admin)
                        return ToApiValidationFail("Admin accounts cannot be deleted from reports.", 403);
                    user.IsDeleted = true;
                    user.DeletedAt = DateTime.UtcNow;
                    user.IsBanned = true;
                    user.BannedUntil = null;
                    message = "Reported user deleted.";
                    break;

                default:
                    return ToApiValidationFail("Unsupported report target.", 400);
            }

            report.Status = ReportStatus.Actioned;
            report.ResolvedAt = DateTime.UtcNow;
            if (_authUser.Id.HasValue)
                report.ResolvedBy = await _db.Users.FindAsync(_authUser.Id.Value);

            await _db.SaveChangesAsync();
            return ToApiValidationSuccess(message);
        }
    }
}
