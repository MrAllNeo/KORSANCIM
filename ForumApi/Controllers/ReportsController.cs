using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    // Herkese açık şikayet oluşturma ucu — moderasyon tarafı AdminController'da
    // (GET/PUT api/admin/reports/*).
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class CreateReportDto
        {
            [Required(ErrorMessage = "Hedef türü zorunludur.")]
            public string TargetType { get; set; } = string.Empty;

            [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir hedef belirtin.")]
            public int TargetId { get; set; }

            [Required(ErrorMessage = "Şikayet sebebi zorunludur.")]
            [StringLength(200, MinimumLength = 3, ErrorMessage = "Sebep 3-200 karakter olmalıdır.")]
            public string Reason { get; set; } = string.Empty;

            [StringLength(1000, ErrorMessage = "Not en fazla 1000 karakter olabilir.")]
            public string? Note { get; set; }
        }

        // POST: api/reports — giriş yapmış her kullanıcı şikayet edebilir.
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportDto dto)
        {
            if (!ReportTargetType.IsValid(dto.TargetType))
            {
                return BadRequest(new { error = $"Geçersiz hedef türü. Geçerli değerler: {string.Join(", ", ReportTargetType.All)}" });
            }

            var targetExists = dto.TargetType switch
            {
                ReportTargetType.Topic => await _context.Topics.AnyAsync(t => t.Id == dto.TargetId),
                ReportTargetType.Comment => await _context.Comments.AnyAsync(c => c.Id == dto.TargetId),
                ReportTargetType.User => await _context.Users.AnyAsync(u => u.Id == dto.TargetId),
                _ => false
            };

            if (!targetExists)
            {
                return NotFound(new { error = "Şikayet edilen içerik bulunamadı." });
            }

            var userId = CurrentUserId;

            // Aynı kişi aynı hedefi işlem bekleyen bir şikayeti varken tekrar
            // tekrar raporlayıp kuyruğu şişiremesin.
            var alreadyPending = await _context.Reports.AnyAsync(r =>
                r.TargetType == dto.TargetType &&
                r.TargetId == dto.TargetId &&
                r.ReporterUserId == userId &&
                (r.Status == ReportStatus.Pending || r.Status == ReportStatus.Reviewing));

            if (alreadyPending)
            {
                return BadRequest(new { error = "Bu içerik için zaten incelenmekte olan bir şikayetiniz var." });
            }

            var report = new Report
            {
                TargetType = dto.TargetType,
                TargetId = dto.TargetId,
                ReporterUserId = userId,
                Reason = dto.Reason,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Şikayetiniz alındı, incelenecek.", reportId = report.Id });
        }

        // GET: api/reports/mine — kendi şikayetlerimin durumu.
        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyReports()
        {
            var userId = CurrentUserId;

            var reports = await _context.Reports
                .Where(r => r.ReporterUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.TargetType,
                    r.TargetId,
                    r.Reason,
                    r.Status,
                    r.ResolutionNote,
                    r.CreatedAt,
                    r.ResolvedAt
                })
                .ToListAsync();

            return Ok(reports);
        }
    }
}
