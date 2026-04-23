using System.Globalization;
using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;
using WebAPI.Services.Accounts;
using WebAPI.Services.Quotas;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;
        private readonly IUserLifecycleService _userLifecycleService;
        private readonly IUserActionQuotaService _quotaService;

        public ReportsController(
            AppDbContext db,
            IAuthUserService authUser,
            IUserLifecycleService userLifecycleService,
            IUserActionQuotaService quotaService)
        {
            _db = db;
            _authUser = authUser;
            _userLifecycleService = userLifecycleService;
            _quotaService = quotaService;
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

            if (dto.TargetType == ReportTargetType.Post)
            {
                var alreadyReported = await _db.Reports.AnyAsync(report =>
                    report.TargetType == ReportTargetType.Post &&
                    report.TargetId == dto.TargetId &&
                    EF.Property<int>(report, "ReporterId") == reporter.Id);

                if (alreadyReported)
                    return ToApiValidationFail("Вече си подал сигнал за тази публикация.", 409);
            }

            var quota = await _quotaService.CheckAsync(reporter.Id, UserActionQuotaType.ReportCreate);
            if (!quota.Allowed)
                return ToQuotaFail(quota);

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
            await _quotaService.RecordAsync(reporter.Id, UserActionQuotaType.ReportCreate);

            return ToApiValidationSuccess("Report submitted.");
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            if (!_authUser.Id.HasValue)
                return ToApiValidationFail("User not authenticated.", 401);

            var reports = await _db.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ResolvedBy)
                .Where(r => r.Reporter.Id == _authUser.Id.Value)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return ToApiValidationSuccess(await MapReportsAsync(reports));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? sort = null)
        {
            var query = _db.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ResolvedBy)
                .AsQueryable();

            query = (sort ?? "status").Trim().ToLowerInvariant() switch
            {
                "newest" => query.OrderByDescending(r => r.CreatedAt),
                "oldest" => query.OrderBy(r => r.CreatedAt),
                "target" => query.OrderBy(r => r.TargetType).ThenByDescending(r => r.CreatedAt),
                "reporter" => query.OrderBy(r => r.Reporter.Username).ThenByDescending(r => r.CreatedAt),
                "status" or _ => query.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt)
            };

            var reports = await query
                .ToListAsync();

            return ToApiValidationSuccess(await MapReportsAsync(reports));
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

            if (dto.Status == ReportStatus.Open)
            {
                report.ResolvedBy = null;
            }
            else if (_authUser.Id.HasValue)
            {
                report.ResolvedBy = await _db.Users.FindAsync(_authUser.Id.Value);
            }

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
                    var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == report.TargetId);
                    if (user == null)
                        return ToApiValidationFail("Reported user not found.", 404);
                    if (user.Role == Role.Admin)
                        return ToApiValidationFail("Admin accounts cannot be deleted from reports.", 403);

                    await _userLifecycleService.DeleteUserWithContentAsync(user.Id);
                    message = "Reported user deleted.";
                    break;

                default:
                    return ToApiValidationFail("Unsupported report target.", 400);
            }

            await MarkReportsForTargetAsync(report.TargetType, report.TargetId, ReportStatus.Actioned);
            return ToApiValidationSuccess(message);
        }

        private async Task MarkReportsForTargetAsync(ReportTargetType targetType, int targetId, ReportStatus nextStatus)
        {
            var relatedReports = await _db.Reports
                .Where(r => r.TargetType == targetType && r.TargetId == targetId)
                .ToListAsync();

            User? actingUser = null;
            if (_authUser.Id.HasValue)
            {
                actingUser = await _db.Users.FindAsync(_authUser.Id.Value);
            }

            foreach (var report in relatedReports)
            {
                report.Status = nextStatus;
                report.ResolvedAt = nextStatus == ReportStatus.Open ? null : DateTime.UtcNow;
                report.ResolvedBy = nextStatus == ReportStatus.Open ? null : actingUser;
            }

            await _db.SaveChangesAsync();
        }

        private async Task<List<ReportDto>> MapReportsAsync(List<Report> reports)
        {
            var threadIds = reports
                .Where(r => r.TargetType == ReportTargetType.Thread)
                .Select(r => r.TargetId)
                .Distinct()
                .ToList();

            var postIds = reports
                .Where(r => r.TargetType == ReportTargetType.Post)
                .Select(r => r.TargetId)
                .Distinct()
                .ToList();

            var pinIds = reports
                .Where(r => r.TargetType == ReportTargetType.Pin)
                .Select(r => r.TargetId)
                .Distinct()
                .ToList();

            var userIds = reports
                .Where(r => r.TargetType == ReportTargetType.User)
                .Select(r => r.TargetId)
                .Distinct()
                .ToList();

            var threads = threadIds.Count == 0
                ? new Dictionary<int, (string Title, bool Exists)>()
                : await _db.ForumThreads
                    .Where(t => threadIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.Title })
                    .ToDictionaryAsync(
                        t => t.Id,
                        t => (t.Title, true));

            var posts = postIds.Count == 0
                ? new Dictionary<int, (string Label, int ThreadId, string ThreadTitle, bool Exists)>()
                : await _db.ForumPosts
                    .Where(p => postIds.Contains(p.Id))
                    .Select(p => new
                    {
                        p.Id,
                        Label = p.Title ?? p.Content,
                        ThreadId = EF.Property<int>(p, "ThreadId"),
                        ThreadTitle = p.Thread.Title
                    })
                    .ToDictionaryAsync(
                        p => p.Id,
                        p => (p.Label, p.ThreadId, p.ThreadTitle, true));

            var pins = pinIds.Count == 0
                ? new Dictionary<int, (string Title, bool Exists)>()
                : await _db.EventPins
                    .Where(p => pinIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Title })
                    .ToDictionaryAsync(
                        p => p.Id,
                        p => (p.Title, true));

            var users = userIds.Count == 0
                ? new Dictionary<int, (string Username, Role Role, bool Exists)>()
                : await _db.Users
                    .IgnoreQueryFilters()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.Username, u.Role })
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => (u.Username, u.Role, true));

            var mapped = new List<ReportDto>(reports.Count);
            foreach (var report in reports)
            {
                var dto = new ReportDto
                {
                    Id = report.Id,
                    TargetType = report.TargetType,
                    TargetId = report.TargetId,
                    Reason = report.Reason,
                    Details = report.Details,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt,
                    ReporterId = report.Reporter?.Id ?? 0,
                    ReporterUsername = report.Reporter?.Username ?? "Unknown reporter",
                    ResolvedAt = report.ResolvedAt,
                    ResolvedByUserId = report.ResolvedBy?.Id
                };

                switch (report.TargetType)
                {
                    case ReportTargetType.Thread:
                        if (threads.TryGetValue(report.TargetId, out var threadInfo))
                        {
                            dto.TargetExists = threadInfo.Item2;
                            dto.TargetLabel = threadInfo.Item1;
                            dto.PreviewPath = $"/threads/{report.TargetId}";
                        }
                        else
                        {
                            dto.TargetExists = false;
                            dto.TargetLabel = "Deleted thread";
                        }
                        break;

                    case ReportTargetType.Post:
                        if (posts.TryGetValue(report.TargetId, out var postInfo))
                        {
                            dto.TargetExists = postInfo.Item4;
                            dto.TargetLabel = postInfo.Item1;
                            dto.ContextLabel = $"Thread: {postInfo.Item3}";
                            dto.PreviewPath = $"/threads/{postInfo.Item2}?postId={report.TargetId}";
                        }
                        else
                        {
                            dto.TargetExists = false;
                            dto.TargetLabel = "Deleted post";
                        }
                        break;

                    case ReportTargetType.Pin:
                        if (pins.TryGetValue(report.TargetId, out var pinInfo))
                        {
                            dto.TargetExists = pinInfo.Item2;
                            dto.TargetLabel = pinInfo.Item1;
                            dto.PreviewPath = $"/map?pinId={report.TargetId}";
                        }
                        else
                        {
                            dto.TargetExists = false;
                            dto.TargetLabel = "Deleted marker";
                        }
                        break;

                    case ReportTargetType.User:
                        if (users.TryGetValue(report.TargetId, out var userInfo))
                        {
                            dto.TargetExists = userInfo.Item3;
                            dto.TargetLabel = userInfo.Item1;
                            dto.ContextLabel = $"Role: {userInfo.Item2}";
                            dto.PreviewPath = $"/users/{report.TargetId}";
                        }
                        else
                        {
                            dto.TargetExists = false;
                            dto.TargetLabel = "Deleted user";
                        }
                        break;

                    default:
                        dto.TargetExists = false;
                        dto.TargetLabel = "Unknown target";
                        break;
                }

                mapped.Add(dto);
            }

            return mapped;
        }

        private IActionResult ToQuotaFail(ActionQuotaCheckResult quota)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(quota.RetryAfter.TotalSeconds));
            Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse.Fail(quota.Message));
        }
    }
}
