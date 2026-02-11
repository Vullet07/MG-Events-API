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

        [Authorize(Roles = "Admin,Teacher")]
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

        [Authorize(Roles = "Admin,Teacher")]
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
    }
}
