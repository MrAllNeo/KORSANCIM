using ForumApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private const int MinQueryLength = 2;
        private const int DefaultLimit = 10;
        private const int MaxLimit = 50;

        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/search?q=linux&limit=10
        // Konu, yorum ve kullanıcılarda tek seferde arar.
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = DefaultLimit)
        {
            var term = (q ?? string.Empty).Trim();

            if (term.Length < MinQueryLength)
            {
                return BadRequest(new { error = $"Arama terimi en az {MinQueryLength} karakter olmalıdır." });
            }

            limit = Math.Clamp(limit, 1, MaxLimit);

            // SQLite'ın lower() fonksiyonu yalnızca ASCII harfleri katlar; bu yüzden
            // "İ/ı" gibi Türkçe harflerde büyük/küçük eşleşmesi tam değildir.
            var needle = term.ToLower();

            var topics = await _context.Topics
                .Include(t => t.User)
                .Include(t => t.Category)
                .Where(t => t.Title.ToLower().Contains(needle) || t.Content.ToLower().Contains(needle))
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Content,
                    t.CategoryId,
                    CategoryName = t.Category!.Name,
                    AuthorUsername = t.User!.Username,
                    t.LikeCount,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            var comments = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.Topic)
                .Where(c => c.Content.ToLower().Contains(needle))
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .Select(c => new
                {
                    c.Id,
                    c.TopicId,
                    TopicTitle = c.Topic!.Title,
                    c.Content,
                    AuthorUsername = c.User!.Username,
                    c.LikeCount,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync();

            // E-posta bilerek dönülmüyor — arama herkese açık.
            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(needle)
                         || (u.Bio != null && u.Bio.ToLower().Contains(needle)))
                .OrderBy(u => u.Username)
                .Take(limit)
                .Select(u => new
                {
                    u.Username,
                    u.Bio,
                    u.AvatarUrl,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                query = term,
                topics,
                comments,
                users,
                totals = new
                {
                    topics = topics.Count,
                    comments = comments.Count,
                    users = users.Count
                }
            });
        }
    }
}
