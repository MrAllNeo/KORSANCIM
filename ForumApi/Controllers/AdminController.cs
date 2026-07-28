using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("admin")]
    public class AdminController : ControllerBase
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly AppDbContext _context;
        private readonly FileUploadService _uploads;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AdminController(AppDbContext context, FileUploadService uploads, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _uploads = uploads;
            _passwordHasher = passwordHasher;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? "(bilinmiyor)";

        // Denetim kaydına tek satır ekler — her mutasyonun sonunda çağrılır.
        private async Task LogAction(string action, string? targetType = null, int? targetId = null, string? details = null)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                ActorUserId = CurrentUserId,
                ActorUsername = CurrentUsername,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public class BanUserDto
        {
            [StringLength(300, ErrorMessage = "Sebep en fazla 300 karakter olabilir.")]
            public string? Reason { get; set; }
        }

        public class ChangeRoleDto
        {
            [Required(ErrorMessage = "Rol zorunludur.")]
            public string Role { get; set; } = string.Empty;

            // Rol atama en yüksek etkili işlem (yetki yükseltme); Owner'ın
            // kendi şifresiyle yeniden onay vermesi gerekir.
            [Required(ErrorMessage = "Onay için şifreniz gereklidir.")]
            public string CurrentPassword { get; set; } = string.Empty;
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

        public class BadgeDto
        {
            [Required(ErrorMessage = "Rozet adı zorunludur.")]
            [StringLength(40, MinimumLength = 2, ErrorMessage = "Rozet adı 2-40 karakter olmalıdır.")]
            public string Name { get; set; } = string.Empty;

            [StringLength(30, ErrorMessage = "İkon adı en fazla 30 karakter olabilir.")]
            public string? Icon { get; set; }

            [Required(ErrorMessage = "Renk teması zorunludur.")]
            public string ColorTheme { get; set; } = string.Empty;

            public bool Shine { get; set; } = false;
        }

        public class AssignBadgeDto
        {
            // null = rozeti kaldır.
            public int? BadgeId { get; set; }
        }

        public class CategoryDto
        {
            [Required(ErrorMessage = "Kategori adı zorunludur.")]
            [StringLength(60, MinimumLength = 2, ErrorMessage = "Kategori adı 2-60 karakter olmalıdır.")]
            public string Name { get; set; } = string.Empty;

            [Required(ErrorMessage = "Açıklama zorunludur.")]
            [StringLength(200, ErrorMessage = "Açıklama en fazla 200 karakter olabilir.")]
            public string Description { get; set; } = string.Empty;

            [StringLength(30, ErrorMessage = "İkon adı en fazla 30 karakter olabilir.")]
            public string Icon { get; set; } = "hash";

            public int DisplayOrder { get; set; } = 0;
        }

        public class SiteSettingsDto
        {
            public bool RegistrationOpen { get; set; } = true;
            public bool MaintenanceMode { get; set; } = false;

            [StringLength(500, ErrorMessage = "Duyuru en fazla 500 karakter olabilir.")]
            public string? AnnouncementText { get; set; }
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

            // Son 14 günün günlük büyüme grafiği. Sağlayıcı-bağımsız olması için
            // tarihler belleğe çekilip C# tarafında günlere bölünüyor.
            var since14d = DateTime.UtcNow.Date.AddDays(-13);

            var userDates = await _context.Users.Where(u => u.CreatedAt >= since14d).Select(u => u.CreatedAt).ToListAsync();
            var topicDates = await _context.Topics.Where(t => t.CreatedAt >= since14d).Select(t => t.CreatedAt).ToListAsync();
            var commentDates = await _context.Comments.Where(c => c.CreatedAt >= since14d).Select(c => c.CreatedAt).ToListAsync();

            var days = Enumerable.Range(0, 14).Select(i => since14d.AddDays(i)).ToList();

            var growth = days.Select(day => new
            {
                date = day.ToString("yyyy-MM-dd"),
                newUsers = userDates.Count(d => d.Date == day),
                newTopics = topicDates.Count(d => d.Date == day),
                newComments = commentDates.Count(d => d.Date == day)
            }).ToList();

            // En aktif kullanıcılar — konu + yorum toplamına göre.
            var topicCounts = await _context.Topics.GroupBy(t => t.UserId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var commentCounts = await _context.Comments.GroupBy(c => c.UserId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

            var activityByUser = new Dictionary<int, int>();
            foreach (var t in topicCounts) activityByUser[t.Key] = activityByUser.GetValueOrDefault(t.Key) + t.Count;
            foreach (var c in commentCounts) activityByUser[c.Key] = activityByUser.GetValueOrDefault(c.Key) + c.Count;

            var topUserIds = activityByUser.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key).ToList();
            var topUserNames = await _context.Users.Where(u => topUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

            var topActiveUsers = topUserIds
                .Where(id => topUserNames.ContainsKey(id))
                .Select(id => new { userId = id, username = topUserNames[id], activityCount = activityByUser[id] })
                .ToList();

            // En çok şikayet edilen kullanıcılar (yalnızca hedef türü User olanlar).
            var reportedUserCounts = await _context.Reports
                .Where(r => r.TargetType == ReportTargetType.User)
                .GroupBy(r => r.TargetId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            var reportedUserIds = reportedUserCounts.Select(r => r.UserId).ToList();
            var reportedUserNames = await _context.Users.Where(u => reportedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

            var topReportedUsers = reportedUserCounts
                .Where(r => reportedUserNames.ContainsKey(r.UserId))
                .Select(r => new { userId = r.UserId, username = reportedUserNames[r.UserId], reportCount = r.Count })
                .ToList();

            // Karma aktivite akışı: son konular/yorumlar/üyelikler/şikayetler tek
            // zaman çizgisinde birleştirilir (her tablo ayrı, gerçek bir join yok).
            var recentTopics = await _context.Topics.Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt).Take(8)
                .Select(t => new { Type = "topic", Text = (string?)t.Title, Username = (string?)t.User!.Username, t.CreatedAt })
                .ToListAsync();

            var recentComments = await _context.Comments.Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt).Take(8)
                .Select(c => new { Type = "comment", Text = (string?)c.Content, Username = (string?)c.User!.Username, c.CreatedAt })
                .ToListAsync();

            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt).Take(8)
                .Select(u => new { Type = "signup", Text = (string?)null, Username = (string?)u.Username, u.CreatedAt })
                .ToListAsync();

            var recentReports = await _context.Reports
                .OrderByDescending(r => r.CreatedAt).Take(8)
                .Select(r => new { Type = "report", Text = (string?)r.Reason, Username = (string?)null, r.CreatedAt })
                .ToListAsync();

            var activityFeed = recentTopics
                .Concat(recentComments)
                .Concat(recentUsers)
                .Concat(recentReports)
                .OrderByDescending(a => a.CreatedAt)
                .Take(15)
                .Select(a => new
                {
                    a.Type,
                    text = a.Text != null && a.Text.Length > 80 ? a.Text[..80] + "…" : a.Text,
                    a.Username,
                    a.CreatedAt
                })
                .ToList();

            return Ok(new
            {
                totalUsers,
                bannedUsers,
                totalTopics,
                totalComments,
                pendingReports,
                newUsersLast24h,
                newTopicsLast24h,
                topicsByCategory,
                growth,
                topActiveUsers,
                topReportedUsers,
                activityFeed
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
                    Badge = u.Badge == null ? null : new { u.Badge.Id, u.Badge.Name, u.Badge.Icon, u.Badge.ColorTheme, u.Badge.Shine },
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
            await LogAction(AuditActions.BanUser, "User", target.Id, $"{target.Username}: {dto.Reason}");

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
            await LogAction(AuditActions.UnbanUser, "User", target.Id, target.Username);

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

            var actingOwner = await _context.Users.FindAsync(CurrentUserId);
            if (actingOwner == null ||
                _passwordHasher.VerifyHashedPassword(actingOwner, actingOwner.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Şifre doğrulanamadı." });
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

            var previousRole = target.Role;
            target.Role = dto.Role;
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.ChangeRole, "User", target.Id, $"{target.Username}: {previousRole} -> {dto.Role}");

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

            var topicTitle = topic.Title;

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            _uploads.DeleteAll(fileUrls);
            await LogAction(AuditActions.DeleteTopic, "Topic", id, topicTitle);

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
            await LogAction(AuditActions.EditTopic, "Topic", id, dto.Title);

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
            await LogAction(AuditActions.DeleteComment, "Comment", id);

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
            await LogAction(AuditActions.EditComment, "Comment", id);

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
            await LogAction(AuditActions.UpdateReportStatus, "Report", id, dto.Status);

            return Ok(new { message = "Şikayet güncellendi.", reportId = report.Id, status = report.Status });
        }

        // ── Rozetler ─────────────────────────────────────────────
        // Tanımlama/silme Owner'a özel — site kimliğine ait yeni bir "ünvan"
        // yaratmak, mevcut kullanıcılara rozet atamaktan (Admin+) daha büyük
        // bir karardır. Atama AssignBadge ile Admin+'a açık.

        // GET: api/admin/badges
        [HttpGet("badges")]
        public async Task<IActionResult> GetBadges()
        {
            var badges = await _context.Badges
                .OrderBy(b => b.Id)
                .Select(b => new { b.Id, b.Name, b.Icon, b.ColorTheme, b.Shine, UserCount = b.Users.Count })
                .ToListAsync();

            return Ok(badges);
        }

        // POST: api/admin/badges
        [Authorize(Roles = Roles.Owner)]
        [HttpPost("badges")]
        public async Task<IActionResult> CreateBadge([FromBody] BadgeDto dto)
        {
            if (!BadgeThemes.IsValid(dto.ColorTheme))
            {
                return BadRequest(new { error = $"Geçersiz tema. Geçerli değerler: {string.Join(", ", BadgeThemes.All)}" });
            }

            var badge = new Badge
            {
                Name = dto.Name.Trim(),
                Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim(),
                ColorTheme = dto.ColorTheme,
                Shine = dto.Shine
            };

            _context.Badges.Add(badge);
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.CreateBadge, "Badge", badge.Id, badge.Name);

            return Ok(new { message = "Rozet oluşturuldu.", badgeId = badge.Id });
        }

        // PUT: api/admin/badges/5
        [Authorize(Roles = Roles.Owner)]
        [HttpPut("badges/{id}")]
        public async Task<IActionResult> UpdateBadge(int id, [FromBody] BadgeDto dto)
        {
            if (!BadgeThemes.IsValid(dto.ColorTheme))
            {
                return BadRequest(new { error = $"Geçersiz tema. Geçerli değerler: {string.Join(", ", BadgeThemes.All)}" });
            }

            var badge = await _context.Badges.FindAsync(id);
            if (badge == null) return NotFound(new { error = "Rozet bulunamadı." });

            badge.Name = dto.Name.Trim();
            badge.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim();
            badge.ColorTheme = dto.ColorTheme;
            badge.Shine = dto.Shine;

            await _context.SaveChangesAsync();
            await LogAction(AuditActions.UpdateBadge, "Badge", badge.Id, badge.Name);

            return Ok(new { message = "Rozet güncellendi." });
        }

        // DELETE: api/admin/badges/5 — kullanıcılar rozetsiz kalır (SetNull FK).
        [Authorize(Roles = Roles.Owner)]
        [HttpDelete("badges/{id}")]
        public async Task<IActionResult> DeleteBadge(int id)
        {
            var badge = await _context.Badges.FindAsync(id);
            if (badge == null) return NotFound(new { error = "Rozet bulunamadı." });

            var badgeName = badge.Name;

            _context.Badges.Remove(badge);
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.DeleteBadge, "Badge", id, badgeName);

            return Ok(new { message = "Rozet silindi." });
        }

        // PUT: api/admin/users/5/badge — rozet atama/kaldırma. Admin+ yetkisinde
        // (tag atama, planlanan izin matrisinde Admin seviyesi).
        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpPut("users/{id}/badge")]
        public async Task<IActionResult> AssignBadge(int id, [FromBody] AssignBadgeDto dto)
        {
            var target = await _context.Users.FindAsync(id);
            if (target == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (dto.BadgeId.HasValue && !await _context.Badges.AnyAsync(b => b.Id == dto.BadgeId.Value))
            {
                return BadRequest(new { error = "Böyle bir rozet yok." });
            }

            target.BadgeId = dto.BadgeId;
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.AssignBadge, "User", target.Id, $"{target.Username}: badgeId={dto.BadgeId?.ToString() ?? "null"}");

            return Ok(new { message = "Rozet güncellendi.", userId = target.Id, badgeId = target.BadgeId });
        }

        // ── Kategoriler ──────────────────────────────────────────
        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            var slug = Slugify(dto.Name);

            if (await _context.Categories.AnyAsync(c => c.Slug == slug || c.Name == dto.Name))
            {
                return BadRequest(new { error = "Bu isimde veya slug'da bir kategori zaten var." });
            }

            var category = new Category
            {
                Name = dto.Name.Trim(),
                Slug = slug,
                Description = dto.Description.Trim(),
                Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "hash" : dto.Icon.Trim(),
                DisplayOrder = dto.DisplayOrder
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.CreateCategory, "Category", category.Id, category.Name);

            return Ok(new { message = "Kategori oluşturuldu.", categoryId = category.Id, slug = category.Slug });
        }

        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto dto)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { error = "Kategori bulunamadı." });

            var slug = Slugify(dto.Name);

            if (await _context.Categories.AnyAsync(c => c.Id != id && (c.Slug == slug || c.Name == dto.Name)))
            {
                return BadRequest(new { error = "Bu isimde veya slug'da başka bir kategori zaten var." });
            }

            category.Name = dto.Name.Trim();
            category.Slug = slug;
            category.Description = dto.Description.Trim();
            category.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "hash" : dto.Icon.Trim();
            category.DisplayOrder = dto.DisplayOrder;

            await _context.SaveChangesAsync();
            await LogAction(AuditActions.UpdateCategory, "Category", category.Id, category.Name);

            return Ok(new { message = "Kategori güncellendi." });
        }

        // DELETE: api/admin/categories/5 — içinde konu varsa reddedilir, önce
        // konuların taşınması/silinmesi gerekir (Restrict FK ile de tutarlı).
        [Authorize(Roles = Roles.AdminAndAbove)]
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { error = "Kategori bulunamadı." });

            var topicCount = await _context.Topics.CountAsync(t => t.CategoryId == id);
            if (topicCount > 0)
            {
                return BadRequest(new { error = $"Bu kategoride {topicCount} konu var. Önce konuları başka bir kategoriye taşıyın." });
            }

            var categoryName = category.Name;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            await LogAction(AuditActions.DeleteCategory, "Category", id, categoryName);

            return Ok(new { message = "Kategori silindi." });
        }

        // ── Denetim Kaydı ──────────────────────────────────────────
        // Yalnızca okuma — kayıtlar hiçbir uçtan düzenlenemez/silinemez.
        // Owner'a özel: "kim izliyor" sorusunun cevabını yalnızca en üst
        // yetkili görmeli, aksi halde bir Admin kendi izini gizleyebilir.
        [Authorize(Roles = Roles.Owner)]
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] string? action, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.ActorUsername,
                    a.Action,
                    a.TargetType,
                    a.TargetId,
                    a.Details,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items = logs, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }

        // ── Site Ayarları ────────────────────────────────────────────
        // Okuma herkese açık (GET /api/settings, SettingsController), yazma
        // yalnızca Owner — kayıt aç/kapa ve bakım modu site geneli etki eder.
        [Authorize(Roles = Roles.Owner)]
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] SiteSettingsDto dto)
        {
            var settings = await _context.SiteSettings.FindAsync(1);
            if (settings == null)
            {
                settings = new SiteSettings { Id = 1 };
                _context.SiteSettings.Add(settings);
            }

            settings.RegistrationOpen = dto.RegistrationOpen;
            settings.MaintenanceMode = dto.MaintenanceMode;
            settings.AnnouncementText = string.IsNullOrWhiteSpace(dto.AnnouncementText) ? null : dto.AnnouncementText.Trim();

            await _context.SaveChangesAsync();
            await LogAction(AuditActions.UpdateSettings, null, null,
                $"registrationOpen={settings.RegistrationOpen}, maintenanceMode={settings.MaintenanceMode}");

            return Ok(new { message = "Ayarlar güncellendi." });
        }

        private static string Slugify(string input)
        {
            var lower = input.Trim().ToLowerInvariant()
                .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u')
                .Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');

            var chars = lower.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            var slug = new string(chars);

            while (slug.Contains("--")) slug = slug.Replace("--", "-");

            return slug.Trim('-');
        }
    }
}
