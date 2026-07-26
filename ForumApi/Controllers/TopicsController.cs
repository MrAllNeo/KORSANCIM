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
    [ApiController]
    [Route("api/[controller]")]
    public class TopicsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FileUploadService _uploads;

        public TopicsController(AppDbContext context, FileUploadService uploads)
        {
            _context = context;
            _uploads = uploads;
        }

        // Kimlik yalnızca doğrulanmış token'dan gelir; istemcinin gönderdiği
        // hiçbir kullanıcı adı alanına güvenilmez.
        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name)!;

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

        // GET: api/topics
        [HttpGet]
        public async Task<IActionResult> GetTopics([FromQuery] int categoryId = 0)
        {
            var query = _context.Topics.AsQueryable();

            if (categoryId > 0)
            {
                query = query.Where(t => t.CategoryId == categoryId);
            }

            var topics = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var result = topics.Select(t => new
            {
                t.Id,
                t.Title,
                t.CategoryId,
                t.Content,
                t.IsLegalTermsAccepted,
                t.AuthorUsername,
                t.LikeCount,
                t.CreatedAt,
                FileUrls = string.IsNullOrEmpty(t.FileUrlsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(t.FileUrlsJson)
            });

            return Ok(result);
        }

        // GET: api/topics/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var result = new
            {
                topic.Id,
                topic.Title,
                topic.CategoryId,
                topic.Content,
                topic.IsLegalTermsAccepted,
                topic.AuthorUsername,
                topic.LikeCount,
                topic.CreatedAt,
                FileUrls = string.IsNullOrEmpty(topic.FileUrlsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(topic.FileUrlsJson)
            };

            return Ok(result);
        }

        // POST: api/topics
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateTopic([FromForm] CreateTopicDto dto)
        {
            if (!dto.IsLegalTermsAccepted)
            {
                return BadRequest(new { error = "Yasal şartları kabul etmelisiniz." });
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
                AuthorUsername = CurrentUsername,
                FileUrlsJson = fileUrls.Count > 0 ? JsonSerializer.Serialize(fileUrls) : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            return Ok(topic);
        }

        // POST: api/topics/5/like
        [Authorize]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var username = CurrentUsername;

            var existingLike = await _context.TopicLikes
                .FirstOrDefaultAsync(l => l.TopicId == id && l.Username == username);

            bool isLiked;

            if (existingLike != null)
            {
                // Zaten beğenmişse beğeniyi geri al (Unlike)
                _context.TopicLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                // Beğenmemişse yeni beğeni ekle
                _context.TopicLikes.Add(new TopicLike { TopicId = id, Username = username });
                isLiked = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index ihlali: aynı anda gelen ikinci beğeni isteği.
                // İlk istek zaten kaydı yazmış durumda, sayacı yeniden hesaplayıp dönüyoruz.
                _context.ChangeTracker.Clear();
                var current = await _context.TopicLikes.CountAsync(l => l.TopicId == id);
                return Ok(new { likes = current, isLiked = true });
            }

            // Sayacı beğeni tablosundan türetiyoruz — okuyup-artırmaya göre
            // yarış koşullarına karşı dayanıklı.
            topic.LikeCount = await _context.TopicLikes.CountAsync(l => l.TopicId == id);
            await _context.SaveChangesAsync();

            return Ok(new { likes = topic.LikeCount, isLiked });
        }

        // GET: api/topics/5/like — mevcut kullanıcı bu konuyu beğenmiş mi?
        [Authorize]
        [HttpGet("{id}/like")]
        public async Task<IActionResult> GetLikeState(int id)
        {
            var username = CurrentUsername;
            var isLiked = await _context.TopicLikes.AnyAsync(l => l.TopicId == id && l.Username == username);
            return Ok(new { isLiked });
        }
    }
}
