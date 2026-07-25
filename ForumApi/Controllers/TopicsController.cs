using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopicsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TopicsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public class CreateTopicDto
        {
            public string Title { get; set; } = string.Empty;
            public int CategoryId { get; set; }
            public string Content { get; set; } = string.Empty;
            public bool IsLegalTermsAccepted { get; set; }
            public string AuthorUsername { get; set; } = "Anonim";
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
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in dto.Files)
                {
                    if (file.Length > 0)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        fileUrls.Add("/uploads/" + uniqueFileName);
                    }
                }
            }

            var topic = new Topic
            {
                Title = dto.Title,
                CategoryId = dto.CategoryId,
                Content = dto.Content,
                IsLegalTermsAccepted = dto.IsLegalTermsAccepted,
                AuthorUsername = string.IsNullOrWhiteSpace(dto.AuthorUsername) ? "Anonim" : dto.AuthorUsername,
                FileUrlsJson = fileUrls.Count > 0 ? JsonSerializer.Serialize(fileUrls) : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            return Ok(topic);
        }

        // POST: api/topics/5/like
        // POST: api/topics/5/like
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeTopic(int id, [FromBody] LikeRequestDto dto)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var username = string.IsNullOrWhiteSpace(dto.Username) ? "Anonim" : dto.Username;

            // Kullanıcı bu konuyu daha önce beğenmiş mi?
            var existingLike = await _context.TopicLikes
                .FirstOrDefaultAsync(l => l.TopicId == id && l.Username.ToLower() == username.ToLower());

            bool isLiked;

            if (existingLike != null)
            {
                // Zaten beğenmişse beğeniyi geri al (Unlike)
                _context.TopicLikes.Remove(existingLike);
                topic.LikeCount = Math.Max(0, topic.LikeCount - 1);
                isLiked = false;
            }
            else
            {
                // Beğenmemişse yeni beğeni ekle
                _context.TopicLikes.Add(new TopicLike { TopicId = id, Username = username });
                topic.LikeCount += 1;
                isLiked = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { likes = topic.LikeCount, isLiked });
        }

        public class LikeRequestDto
        {
            public string Username { get; set; } = string.Empty;
        }
    }
}