using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // Güncelleme Modeli
        public class UpdateProfileDto
        {
            public string Username { get; set; } = string.Empty;
            public string? Bio { get; set; }
            public string? GithubUrl { get; set; }
            public string? WebsiteUrl { get; set; }
            public bool HideEmail { get; set; }
            public bool ShowActivity { get; set; }
        }

        // 1. Kullanıcının Public Profil Bilgilerini ve İçeriklerini Getir (GET: /api/users/profile/CREATOR)
        [HttpGet("profile/{username}")]
        public async Task<IActionResult> GetUserProfile(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            
            // Eğer kullanıcı henüz kayıtlı değilse bile içeriklerini esnekçe listele
            if (user == null)
            {
                var anonTopics = await _context.Topics
                    .Where(t => t.AuthorUsername.ToLower() == username.ToLower())
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var anonComments = await _context.Comments
                    .Where(c => c.AuthorUsername.ToLower() == username.ToLower())
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return Ok(new
                {
                    Username = username,
                    Email = "",
                    Bio = "DevForum topluluk üyesi.",
                    GithubUrl = "",
                    WebsiteUrl = "",
                    HideEmail = true,
                    ShowActivity = true,
                    CreatedAt = DateTime.UtcNow,
                    Topics = anonTopics,
                    Comments = anonComments
                });
            }

            var userTopics = await _context.Topics
                .Where(t => t.AuthorUsername.ToLower() == username.ToLower())
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var userComments = await _context.Comments
                .Where(c => c.AuthorUsername.ToLower() == username.ToLower())
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                Email = user.HideEmail ? null : user.Email,
                user.Bio,
                user.GithubUrl,
                user.WebsiteUrl,
                user.HideEmail,
                user.ShowActivity,
                user.CreatedAt,
                Topics = userTopics,
                Comments = userComments
            });
        }

        // 2. Profil Ayarlarını Güncelle (PUT: /api/users/profile)
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            user.Bio = dto.Bio;
            user.GithubUrl = dto.GithubUrl;
            user.WebsiteUrl = dto.WebsiteUrl;
            user.HideEmail = dto.HideEmail;
            user.ShowActivity = dto.ShowActivity;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Profil başarıyla güncellendi!" });
        }
    }
}