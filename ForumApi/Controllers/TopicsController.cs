using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopicsController : ControllerBase
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 50;

        private readonly AppDbContext _context;
        private readonly FileUploadService _uploads;

        public TopicsController(AppDbContext context, FileUploadService uploads)
        {
            _context = context;
            _uploads = uploads;
        }

        // Kimlik yalnızca doğrulanmış token'dan gelir; istemcinin gönderdiği
        // hiçbir alana güvenilmez.
        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class CreateTopicDto
        {
            [Required(ErrorMessage = "Başlık zorunludur.")]
            [StringLength(200, MinimumLength = 3, ErrorMessage = "Başlık 3-200 karakter olmalıdır.")]
            public string Title { get; set; } = string.Empty;

            [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçin.")]
            public int CategoryId { get; set; }

            [Required(ErrorMessage = "İçerik zorunludur.")]
            [StringLength(20000, MinimumLength = 1, ErrorMessage = "İçerik en fazla 20000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;

            public bool IsLegalTermsAccepted { get; set; }

            public List<IFormFile>? Files { get; set; }
        }

        public class UpdateTopicDto
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

        // Yazar ve kategori bilgisi ilişkiden okunur; artık kopya string yok.
        // likedByMe, girişli kullanıcının beğenip beğenmediğini taşır; null ise
        // istek anonim demektir.
        private static object ToDto(Topic t, bool? likedByMe = null) => new
        {
            t.Id,
            t.Title,
            t.Content,
            t.CategoryId,
            CategoryName = t.Category?.Name,
            CategorySlug = t.Category?.Slug,
            t.IsLegalTermsAccepted,
            t.UserId,
            AuthorUsername = t.User?.Username ?? "(silinmiş kullanıcı)",
            AuthorAvatarUrl = t.User?.AvatarUrl,
            Badge = BadgeSummary.From(t.User?.Badge),
            t.LikeCount,
            CommentCount = t.Comments?.Count ?? 0,
            t.CreatedAt,
            t.UpdatedAt,
            LikedByMe = likedByMe,
            FileUrls = ParseFileUrls(t.FileUrlsJson)
        };

        // Girişli kullanıcının bu sayfadaki hangi konuları beğendiğini TEK
        // sorguda getirir; eskiden arayüz konu başına ayrı istek atıyordu.
        private async Task<HashSet<int>> LikedTopicIdsAsync(IEnumerable<int> topicIds)
        {
            if (User.Identity?.IsAuthenticated != true) return new HashSet<int>();

            var userId = CurrentUserId;
            var ids = topicIds.ToList();

            return (await _context.TopicLikes
                .Where(l => l.UserId == userId && ids.Contains(l.TopicId))
                .Select(l => l.TopicId)
                .ToListAsync()).ToHashSet();
        }

        private static List<string> ParseFileUrls(string? json) =>
            string.IsNullOrEmpty(json)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        // GET: api/topics?categoryId=1&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetTopics(
            [FromQuery] int categoryId = 0,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _context.Topics
                .Include(t => t.User).ThenInclude(u => u!.Badge)
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .AsQueryable();

            if (categoryId > 0)
            {
                query = query.Where(t => t.CategoryId == categoryId);
            }

            var total = await query.CountAsync();

            var topics = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var liked = await LikedTopicIdsAsync(topics.Select(t => t.Id));
            var isAuthenticated = User.Identity?.IsAuthenticated == true;

            return Ok(new
            {
                items = topics.Select(t => ToDto(t, isAuthenticated ? liked.Contains(t.Id) : (bool?)null)),
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                hasMore = page * pageSize < total
            });
        }

        // GET: api/topics/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTopic(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.User).ThenInclude(u => u!.Badge)
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var liked = await LikedTopicIdsAsync(new[] { topic.Id });
            var isAuthenticated = User.Identity?.IsAuthenticated == true;

            return Ok(ToDto(topic, isAuthenticated ? liked.Contains(topic.Id) : (bool?)null));
        }

        // POST: api/topics
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPost]
        public async Task<IActionResult> CreateTopic([FromForm] CreateTopicDto dto)
        {
            var isVerified = await _context.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => u.IsEmailVerified)
                .FirstOrDefaultAsync();

            if (!isVerified)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Konu açabilmek için e-posta adresini doğrulamalısın." });
            }

            if (!dto.IsLegalTermsAccepted)
            {
                return BadRequest(new { error = "Yasal şartları kabul etmelisiniz." });
            }

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            {
                return BadRequest(new { error = "Böyle bir kategori yok." });
            }

            var fileUrls = new List<string>();

            if (dto.Files != null && dto.Files.Count > 0)
            {
                if (dto.Files.Count > FileUploadService.MaxFilesPerTopic)
                {
                    return BadRequest(new { error = $"En fazla {FileUploadService.MaxFilesPerTopic} dosya yükleyebilirsiniz." });
                }

                // Tüm dosyaları önce doğrula; biri geçersizse hiçbiri kaydedilmesin.
                foreach (var file in dto.Files)
                {
                    if (!_uploads.IsValid(file, out var error))
                    {
                        return BadRequest(new { error });
                    }
                }

                foreach (var file in dto.Files)
                {
                    fileUrls.Add(await _uploads.SaveAsync(file, "topic"));
                }
            }

            var topic = new Topic
            {
                Title = dto.Title,
                CategoryId = dto.CategoryId,
                Content = dto.Content,
                IsLegalTermsAccepted = dto.IsLegalTermsAccepted,
                UserId = CurrentUserId,
                FileUrlsJson = fileUrls.Count > 0 ? JsonSerializer.Serialize(fileUrls) : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            await _context.Entry(topic).Reference(t => t.User).LoadAsync();
            await _context.Entry(topic).Reference(t => t.Category).LoadAsync();
            if (topic.User != null)
            {
                await _context.Entry(topic.User).Reference(u => u.Badge).LoadAsync();
            }

            return Ok(ToDto(topic));
        }

        // PUT: api/topics/5 — yalnızca konunun sahibi düzenleyebilir.
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTopic(int id, [FromBody] UpdateTopicDto dto)
        {
            var topic = await _context.Topics
                .Include(t => t.User).ThenInclude(u => u!.Badge)
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            if (topic.UserId != CurrentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu konu size ait değil." });
            }

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            {
                return BadRequest(new { error = "Böyle bir kategori yok." });
            }

            topic.Title = dto.Title;
            topic.CategoryId = dto.CategoryId;
            topic.Content = dto.Content;
            topic.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _context.Entry(topic).Reference(t => t.Category).LoadAsync();

            return Ok(ToDto(topic));
        }

        // DELETE: api/topics/5
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            if (topic.UserId != CurrentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu konu size ait değil." });
            }

            // Yorumlar ve beğeniler artık veritabanı cascade'i ile gidiyor;
            // yalnızca diskteki dosyaları elle temizlemek kalıyor.
            var fileUrls = ParseFileUrls(topic.FileUrlsJson);

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            _uploads.DeleteAll(fileUrls);

            return Ok(new { message = "Konu silindi." });
        }

        // POST: api/topics/5/like
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var userId = CurrentUserId;

            var existingLike = await _context.TopicLikes
                .FirstOrDefaultAsync(l => l.TopicId == id && l.UserId == userId);

            bool isLiked;

            if (existingLike != null)
            {
                _context.TopicLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.TopicLikes.Add(new TopicLike { TopicId = id, UserId = userId });
                isLiked = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index ihlali: aynı anda gelen ikinci beğeni isteği.
                _context.ChangeTracker.Clear();
                var current = await _context.TopicLikes.CountAsync(l => l.TopicId == id);
                return Ok(new { likes = current, isLiked = true });
            }

            // Sayaç beğeni tablosundan türetiliyor — oku-artır'a göre yarış
            // koşullarına dayanıklı.
            topic.LikeCount = await _context.TopicLikes.CountAsync(l => l.TopicId == id);
            await _context.SaveChangesAsync();

            return Ok(new { likes = topic.LikeCount, isLiked });
        }

        // GET: api/topics/5/like — mevcut kullanıcı bu konuyu beğenmiş mi?
        [Authorize]
        [HttpGet("{id}/like")]
        public async Task<IActionResult> GetLikeState(int id)
        {
            var userId = CurrentUserId;
            var isLiked = await _context.TopicLikes.AnyAsync(l => l.TopicId == id && l.UserId == userId);
            return Ok(new { isLiked });
        }
    }
}
