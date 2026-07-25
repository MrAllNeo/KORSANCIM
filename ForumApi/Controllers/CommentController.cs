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

        // 1. Belirli bir konuya ait yorumları getirme (GET: /api/comments/topic/5)
        [HttpGet("topic/{topicId}")]
        public async Task<IActionResult> GetCommentsByTopic(int topicId)
        {
            var comments = await _context.Comments
                .Where(c => c.TopicId == topicId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(comments);
        }

        // 2. Yeni Yorum Yapma (POST: /api/comments)
        [HttpPost]
public async Task<IActionResult> CreateComment([FromBody] Comment comment)
{
    if (string.IsNullOrWhiteSpace(comment.Content))
    {
        return BadRequest(new { error = "Yorum içeriği boş olamaz." });
    }

    if (string.IsNullOrWhiteSpace(comment.AuthorUsername))
    {
        comment.AuthorUsername = "Anonim_Dev";
    }

    comment.CreatedAt = DateTime.UtcNow;

    _context.Comments.Add(comment);
    await _context.SaveChangesAsync();

    return Ok(new { success = true, message = "Yanıt başarıyla eklendi.", comment });
}
    }
}