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
    // Moderatör: kullanıcı banlama + içerik silme. Admin: bunlara ek olarak rol atama.
    // Kimse Admin rolündeki bir hesabı banlayamaz — kilitlenme riskine karşı.
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Moderator)]
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

            if (target.Role == Roles.Admin)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin rolündeki bir kullanıcı banlanamaz." });
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

        // PUT: api/admin/users/5/role — yalnızca Admin.
        [Authorize(Roles = Roles.Admin)]
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto dto)
        {
            if (!Roles.IsValid(dto.Role))
            {
                return BadRequest(new { error = $"Geçersiz rol. Geçerli değerler: {string.Join(", ", Roles.All)}" });
            }

            var target = await _context.Users.FindAsync(id);
            if (target == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (target.Id == CurrentUserId)
            {
                return BadRequest(new { error = "Kendi rolünüzü değiştiremezsiniz." });
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
    }
}
