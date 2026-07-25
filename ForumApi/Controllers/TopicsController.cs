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

        // 1. Tüm Konuları veya Kategoriye Göre Listeleme (GET: /api/topics?categoryId=1)
        [HttpGet]
        public async Task<IActionResult> GetTopics([FromQuery] int? categoryId = null)
        {
            var query = _context.Topics.AsQueryable();

            // Eğer bir kategori filtrelemesi geldiyse SQL tarafında süzüyoruz
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(t => t.CategoryId == categoryId.Value);
            }

            var rawTopics = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var topics = rawTopics.Select(t => new
            {
                t.Id,
                t.Title,
                t.CategoryId,
                t.Content,
                t.AuthorUsername,
                t.CreatedAt,
                FileUrls = !string.IsNullOrEmpty(t.FileUrlsJson) 
                    ? JsonSerializer.Deserialize<List<string>>(t.FileUrlsJson, (JsonSerializerOptions?)null) 
                    : new List<string>()
            });

            return Ok(topics);
        }

        // 2. Tek Bir Konuyu Detayıyla Getirme
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTopicById(int id)
        {
            var topic = await _context.Topics.FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound(new { error = "Konu bulunamadı." });

            var result = new
            {
                topic.Id,
                topic.Title,
                topic.CategoryId,
                topic.Content,
                topic.AuthorUsername,
                topic.CreatedAt,
                FileUrls = !string.IsNullOrEmpty(topic.FileUrlsJson) 
                    ? JsonSerializer.Deserialize<List<string>>(topic.FileUrlsJson, (JsonSerializerOptions?)null) 
                    : new List<string>()
            };

            return Ok(result);
        }

        // 3. Yeni Konu Açma & Dosya/Klasör Yükleme
        [HttpPost]
public async Task<IActionResult> CreateTopic(
    [FromForm] string title,
    [FromForm] int categoryId,
    [FromForm] string content,
    [FromForm] bool termsAccepted,
    [FromForm] string? authorUsername,
    [FromForm] List<IFormFile>? files)
{
    if (!termsAccepted)
    {
        return BadRequest(new { error = "Topluluk kurallarını ve yasal sorumluluğu kabul etmelisiniz." });
    }

    var savedFileUrls = new List<string>();

    if (files != null && files.Count > 0)
    {
        var uploadsFolder = Path.Combine(_env.ContentRootPath, "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                savedFileUrls.Add($"/uploads/{uniqueFileName}");
            }
        }
    }

    var topic = new Topic
    {
        Title = title,
        CategoryId = categoryId,
        Content = content,
        IsLegalTermsAccepted = termsAccepted,
        AuthorUsername = !string.IsNullOrWhiteSpace(authorUsername) ? authorUsername : "Anonim_Dev",
        FileUrlsJson = savedFileUrls.Count > 0 ? JsonSerializer.Serialize(savedFileUrls) : null,
        CreatedAt = DateTime.UtcNow
    };

    _context.Topics.Add(topic);
    await _context.SaveChangesAsync();

    return Ok(new { success = true, topicId = topic.Id, message = "Konu başarıyla yayınlandı." });
}
    }
}