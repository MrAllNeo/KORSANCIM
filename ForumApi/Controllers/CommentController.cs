using ForumApi.Data;
using ForumApi.Models;
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
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] Comment comment)
        {
            if (string.IsNullOrWhiteSpace(comment.Content))
            {
                return BadRequest(new { error = "Yorum içeriği boş olamaz." });
            }

            comment.CreatedAt = DateTime.UtcNow;
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(comment);
        }

        // POST: api/comments/5/like
        // POST: api/comments/5/like
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeComment(int id, [FromBody] LikeRequestDto dto)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            var username = string.IsNullOrWhiteSpace(dto.Username) ? "Anonim" : dto.Username;

            var existingLike = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == id && l.Username.ToLower() == username.ToLower());

            bool isLiked;

            if (existingLike != null)
            {
                _context.CommentLikes.Remove(existingLike);
                comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
                isLiked = false;
            }
            else
            {
                _context.CommentLikes.Add(new CommentLike { CommentId = id, Username = username });
                comment.LikeCount += 1;
                isLiked = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { likes = comment.LikeCount, isLiked });
        }

        public class LikeRequestDto
        {
            public string Username { get; set; } = string.Empty;
        }
    }
}