using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    // İzin matrisi (bkz. Roles.cs):
    // Moderator : kullanıcı banlama, içerik silme, şikayet işleme.
    // Admin     : Moderator + başkasının içeriğini düzenleme, şikayet işleme.
    // Owner     : Admin + rol atama/azletme (tek yetkili — Admin bile rol
    //             değiştiremez, bu adminler arası yetki çakışmasını önler).
    // Kimse Admin veya Owner rolündeki bir hesabı banlayamaz — kilitlenme
    // riskine karşı. Bir Admin'in yetkisi kötüye kullanılırsa çözüm önce
    // Owner'ın onu User/Moderator'a indirmesi, SONRA banlanmasıdır.
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = Roles.ModAndAbove)]
    public class AdminController : ControllerBase
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly AppDbContext _context;
        private readonly FileUploadService _uploads;

        public AdminController(AppDbContext context, FileUploadService uploads)
        {
            _context = context;
            _uploads = uploads;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class BanUserDto
        {
            [StringLength(300, ErrorMessage = "Sebep en fazla 300 karakter olabilir.")]
            public string? Reason { get; set; }
        }

        public class ChangeRoleDto
        {
            [Required(ErrorMessage = "Rol zorunludur.")]
            public string Role { get; set; } = string.Empty;
        }

        public class ModerateEditTopicDto
        {
            [Required(ErrorMessage = "Başlık zorunludur.")]
            [StringLength(200, MinimumLength = 3, ErrorMessage = "Başlık 3-200 karakter olmalıdır.")]
            public string Title { get; set; } = string.Empty;

            [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçin.")]
            public int CategoryId { get; set; }

            [Required(ErrorMessage = "İçerik zorunludur.")]
            [StringLength(20000, MinimumLength = 1, ErrorMessage = "İçerik en fazla 20000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;
        }

        public class ModerateEditCommentDto
        {
            [Required(ErrorMessage = "Yorum içeriği boş olamaz.")]
            [StringLength(5000, MinimumLength = 1, ErrorMessage = "Yorum en fazla 5000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;
        }

        public class UpdateReportStatusDto
        {
            [Required(ErrorMessage = "Durum zorunludur.")]
            public string Status { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Çözüm notu en fazla 500 karakter olabilir.")]
            public string? ResolutionNote { get; set; }
        }

        // GET: api/admin/stats — panel dashboard'u için toplu sayımlar.
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var since24h = DateTime.UtcNow.AddHours(-24);

            var totalUsers = await _context.Users.CountAsync();
            var bannedUsers = await _context.Users.CountAsync(u => u.IsBanned);
            var totalTopics = await _context.Topics.CountAsync();
            var totalComments = await _context.Comments.CountAsync();
            var pendingReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Pending);
            var newUsersLast24h = await _context.Users.CountAsync(u => u.CreatedAt >= since24h);
            var newTopicsLast24h = await _context.Topics.CountAsync(t => t.CreatedAt >= since24h);

            var topicsByCategory = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Name, Count = c.Topics.Count })
                .ToListAsync();

            return Ok(new
            {
                totalUsers,
                bannedUsers,
                totalTopics,
                totalComments,
                pendingReports,
                newUsersLast24h,
                newTopicsLast24h,
                topicsByCategory
            });
        }

        // GET: api/admin/users?search=&page=&pageSize=
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
            }

            var total = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsBanned,
                    u.BanReason,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items = users, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }

        // POST: api/admin/users/5/ban
        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] BanUserDto dto)
        {
            var target = await _context.Users.FindAsync(id);
            if (target == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (target.Id == CurrentUserId)
            {
                return BadRequest(new { error = "Kendinizi banlayamazsınız." });
            }

            if (target.Role == Roles.Admin || target.Role == Roles.Owner)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin veya Owner rolündeki bir kullanıcı banlanamaz." });
            }

            target.IsBanned = true;
            target.BanReason = dto.Reason;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı banlandı.", userId = target.Id });
        }

        // POST: api/admin/users/5/unban
        [HttpPost("users/{id}/unban")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            var target = await _context.Users.FindAsync(id);
            if (target == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            target.IsBanned = false;
            target.BanReason = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ban kaldırıldı.", userId = target.Id });
        }

        // PUT: api/admin/users/5/role — yalnızca Owner. Admin dahil kimse rol
        // atayamaz; bu, adminlerin birbirini terfi/azletmesini imkânsız kılar.
        [Authorize(Roles = Roles.Owner)]
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto dto)
        {
            if (!Roles.IsAssignable(dto.Role))
            {
                return BadRequest(new { error = $"Geçersiz rol. Atanabilir roller: {string.Join(", ", Roles.Assignable)}" });
            }

            var target = await _context.Users.FindAsync(id);
            if (target == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (target.Id == CurrentUserId)
            {
                return BadRequest(new { error = "Kendi rolünüzü değiştiremezsiniz." });
            }

            if (target.Role == Roles.Owner)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Owner rolündeki bir kullanıcının rolü bu uçtan değiştirilemez." });
            }

            target.Role = dto.Role;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Rol güncellendi.", userId = target.Id, role = target.Role });
        }

        // DELETE: api/admin/topics/5 — sahiplikten bağımsız moderasyon silmesi.
        [HttpDelete("topics/{id}")]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var fileUrls = string.IsNullOrEmpty(topic.FileUrlsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(topic.FileUrlsJson) ?? new List<string>();

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            _uploads.DeleteAll(fileUrls);

            return Ok(new { message = "Konu moderasyon tarafından silindi." });
        }

        // PUT: api/admin/topics/5 — sahiplikten bağımsız moderasyon düzenlemesi.
        // Moderator bunu yapamaz; içerik değiştirmek silmekten daha hassas.
        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpPut("topics/{id}")]
        public async Task<IActionResult> EditTopic(int id, [FromBody] ModerateEditTopicDto dto)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            {
                return BadRequest(new { error = "Böyle bir kategori yok." });
            }

            topic.Title = dto.Title;
            topic.CategoryId = dto.CategoryId;
            topic.Content = dto.Content;
            topic.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Konu moderasyon tarafından düzenlendi." });
        }

        // DELETE: api/admin/comments/5 — sahiplikten bağımsız moderasyon silmesi.
        [HttpDelete("comments/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum moderasyon tarafından silindi." });
        }

        // PUT: api/admin/comments/5 — sahiplikten bağımsız moderasyon düzenlemesi.
        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpPut("comments/{id}")]
        public async Task<IActionResult> EditComment(int id, [FromBody] ModerateEditCommentDto dto)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            comment.Content = dto.Content;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum moderasyon tarafından düzenlendi." });
        }

        // GET: api/admin/reports?status=&page=&pageSize=
        // Bekleyenler önce, sonra en yeni. Hedef önizlemesi polymorphic olduğu
        // için (Topic/Comment/User) tek bir join yerine türe göre ayrı toplu
        // sorgularla çözülüyor.
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize)
        {
            if (!string.IsNullOrWhiteSpace(status) && !ReportStatus.IsValid(status))
            {
                return BadRequest(new { error = $"Geçersiz durum. Geçerli değerler: {string.Join(", ", ReportStatus.All)}" });
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.HandledBy)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var total = await query.CountAsync();

            var reports = await query
                .OrderBy(r => r.Status == ReportStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var topicIds = reports.Where(r => r.TargetType == ReportTargetType.Topic).Select(r => r.TargetId).ToList();
            var commentIds = reports.Where(r => r.TargetType == ReportTargetType.Comment).Select(r => r.TargetId).ToList();
            var userIds = reports.Where(r => r.TargetType == ReportTargetType.User).Select(r => r.TargetId).ToList();

            var topicTitles = await _context.Topics.Where(t => topicIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title);
            var commentBodies = await _context.Comments.Where(c => commentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Content);
            var usernames = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

            string TargetPreview(Report r)
            {
                var found = r.TargetType switch
                {
                    ReportTargetType.Topic => topicTitles.GetValueOrDefault(r.TargetId),
                    ReportTargetType.Comment => commentBodies.GetValueOrDefault(r.TargetId),
                    ReportTargetType.User => usernames.GetValueOrDefault(r.TargetId),
                    _ => null
                };

                if (found == null) return "(içerik silinmiş)";
                return found.Length > 120 ? found[..120] + "…" : found;
            }

            return Ok(new
            {
                items = reports.Select(r => new
                {
                    r.Id,
                    r.TargetType,
                    r.TargetId,
                    r.Reason,
                    r.Note,
                    r.Status,
                    r.ResolutionNote,
                    r.CreatedAt,
                    r.ResolvedAt,
                    ReporterUsername = r.Reporter?.Username ?? "(silinmiş kullanıcı)",
                    HandledByUsername = r.HandledBy?.Username,
                    TargetPreview = TargetPreview(r)
                }),
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }

        // PUT: api/admin/reports/5/status — Pending→Reviewing→Resolved/Dismissed
        // hepsi tek uçtan yönetiliyor; terminal durumlarda ResolvedAt basılıyor.
        [HttpPut("reports/{id}/status")]
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateReportStatusDto dto)
        {
            if (!ReportStatus.IsValid(dto.Status))
            {
                return BadRequest(new { error = $"Geçersiz durum. Geçerli değerler: {string.Join(", ", ReportStatus.All)}" });
            }

            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound(new { error = "Şikayet bulunamadı." });

            report.Status = dto.Status;
            report.ResolutionNote = dto.ResolutionNote;
            report.HandledByUserId = CurrentUserId;
            report.ResolvedAt = ReportStatus.IsTerminal(dto.Status) ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Şikayet güncellendi.", reportId = report.Id, status = report.Status });
        }
    }
}
