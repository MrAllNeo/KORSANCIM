using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CommentsController(AppDbContext context)
        {
            _context = context;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.Name)!;

        public class CreateCommentDto
        {
            [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir konu belirtin.")]
            public int TopicId { get; set; }

            [Required(ErrorMessage = "Yorum içeriği boş olamaz.")]
            [StringLength(5000, MinimumLength = 1, ErrorMessage = "Yorum en fazla 5000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;
        }

        // GET: api/comments/topic/5
        [HttpGet("topic/{topicId}")]
        public async Task<IActionResult> GetCommentsByTopic(int topicId)
        {
            var comments = await _context.Comments
                .Where(c => c.TopicId == topicId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(comments);
        }

        // POST: api/comments
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentDto dto)
        {
            var topicExists = await _context.Topics.AnyAsync(t => t.Id == dto.TopicId);
            if (!topicExists)
            {
                return NotFound(new { error = "Konu bulunamadı." });
            }

            var comment = new Comment
            {
                TopicId = dto.TopicId,
                Content = dto.Content,
                AuthorUsername = CurrentUsername,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(comment);
        }

        // POST: api/comments/5/like
        [Authorize]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            var username = CurrentUsername;

            var existingLike = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == id && l.Username == username);

            bool isLiked;

            if (existingLike != null)
            {
                _context.CommentLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.CommentLikes.Add(new CommentLike { CommentId = id, Username = username });
                isLiked = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index ihlali: eşzamanlı ikinci beğeni isteği.
                _context.ChangeTracker.Clear();
                var current = await _context.CommentLikes.CountAsync(l => l.CommentId == id);
                return Ok(new { likes = current, isLiked = true });
            }

            comment.LikeCount = await _context.CommentLikes.CountAsync(l => l.CommentId == id);
            await _context.SaveChangesAsync();

            return Ok(new { likes = comment.LikeCount, isLiked });
        }
    }
}
