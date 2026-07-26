using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class CreateCommentDto
        {
            [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir konu belirtin.")]
            public int TopicId { get; set; }

            [Required(ErrorMessage = "Yorum içeriği boş olamaz.")]
            [StringLength(5000, MinimumLength = 1, ErrorMessage = "Yorum en fazla 5000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;
        }

        public class UpdateCommentDto
        {
            [Required(ErrorMessage = "Yorum içeriği boş olamaz.")]
            [StringLength(5000, MinimumLength = 1, ErrorMessage = "Yorum en fazla 5000 karakter olabilir.")]
            public string Content { get; set; } = string.Empty;
        }

        private static object ToDto(Comment c) => new
        {
            c.Id,
            c.TopicId,
            c.Content,
            c.UserId,
            AuthorUsername = c.User?.Username ?? "(silinmiş kullanıcı)",
            AuthorAvatarUrl = c.User?.AvatarUrl,
            c.LikeCount,
            c.CreatedAt,
            c.UpdatedAt
        };

        // GET: api/comments/topic/5
        [HttpGet("topic/{topicId}")]
        public async Task<IActionResult> GetCommentsByTopic(int topicId)
        {
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TopicId == topicId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(comments.Select(ToDto));
        }

        // POST: api/comments
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentDto dto)
        {
            if (!await _context.Topics.AnyAsync(t => t.Id == dto.TopicId))
            {
                return NotFound(new { error = "Konu bulunamadı." });
            }

            var comment = new Comment
            {
                TopicId = dto.TopicId,
                Content = dto.Content,
                UserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            await _context.Entry(comment).Reference(c => c.User).LoadAsync();

            return Ok(ToDto(comment));
        }

        // PUT: api/comments/5 — yalnızca yorumun sahibi düzenleyebilir.
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentDto dto)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            if (comment.UserId != CurrentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu yorum size ait değil." });
            }

            comment.Content = dto.Content;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ToDto(comment));
        }

        // DELETE: api/comments/5 — beğenileri veritabanı cascade'i ile gider.
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            if (comment.UserId != CurrentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu yorum size ait değil." });
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum silindi." });
        }

        // POST: api/comments/5/like
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikeComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { error = "Yorum bulunamadı." });

            var userId = CurrentUserId;

            var existingLike = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == id && l.UserId == userId);

            bool isLiked;

            if (existingLike != null)
            {
                _context.CommentLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.CommentLikes.Add(new CommentLike { CommentId = id, UserId = userId });
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

        // GET: api/comments/topic/5/likes — bu konudaki hangi yorumları beğendim?
        [Authorize]
        [HttpGet("topic/{topicId}/likes")]
        public async Task<IActionResult> GetLikedComments(int topicId)
        {
            var userId = CurrentUserId;

            var likedIds = await _context.CommentLikes
                .Where(l => l.UserId == userId && l.Comment!.TopicId == topicId)
                .Select(l => l.CommentId)
                .ToListAsync();

            return Ok(new { likedCommentIds = likedIds });
        }
    }
}
