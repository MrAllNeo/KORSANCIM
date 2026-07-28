using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
    public class UsersController : ControllerBase
    {
        // Profilde gösterilecek en fazla kayıt sayısı — sayfalama gelene kadar
        // tüm geçmişin tek istekte dönmesini engeller.
        private const int ActivityLimit = 50;

        private readonly AppDbContext _context;
        private readonly FileUploadService _uploads;

        public UsersController(AppDbContext context, FileUploadService uploads)
        {
            _context = context;
            _uploads = uploads;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class UpdateProfileDto
        {
            [StringLength(500, ErrorMessage = "Biyografi en fazla 500 karakter olabilir.")]
            public string? Bio { get; set; }

            [Url(ErrorMessage = "GitHub adresi geçerli bir URL olmalıdır.")]
            [StringLength(200)]
            public string? GithubUrl { get; set; }

            [Url(ErrorMessage = "Web sitesi geçerli bir URL olmalıdır.")]
            [StringLength(200)]
            public string? WebsiteUrl { get; set; }

            public IFormFile? AvatarFile { get; set; }
            public IFormFile? BannerFile { get; set; }
        }

        // GET: api/users/profile/CREATOR
        [HttpGet("profile/{username}")]
        public async Task<IActionResult> GetProfile(string username)
        {
            var user = await _context.Users
                .Include(u => u.Badge)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null)
            {
                return NotFound(new { error = "Böyle bir kullanıcı bulunamadı." });
            }

            // Ziyaretçi kendisi mi? Öyleyse ShowActivity=false olsa bile kendi
            // konu/yorumlarını görebilmeli — gizlilik yalnızca BAŞKALARINA karşı.
            var viewerUsername = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.Name) : null;
            var isSelf = viewerUsername != null && viewerUsername.Equals(user.Username, StringComparison.OrdinalIgnoreCase);
            var showActivity = user.ShowActivity || isSelf;

            // İçerik artık kullanıcı adına değil, kullanıcı kaydına bağlı.
            var userTopics = !showActivity ? new() : await _context.Topics
                .Include(t => t.Category)
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(ActivityLimit)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Content,
                    t.CategoryId,
                    CategoryName = t.Category!.Name,
                    t.LikeCount,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            var userComments = !showActivity ? new() : await _context.Comments
                .Include(c => c.Topic)
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .Take(ActivityLimit)
                .Select(c => new
                {
                    c.Id,
                    c.TopicId,
                    TopicTitle = c.Topic!.Title,
                    c.Content,
                    c.LikeCount,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync();

            // E-posta bilerek dönülmüyor — profil herkese açık bir uç nokta.
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Bio,
                user.AvatarUrl,
                user.BannerUrl,
                user.GithubUrl,
                user.WebsiteUrl,
                Badge = BadgeSummary.From(user.Badge),
                user.CreatedAt,
                ShowActivity = isSelf ? user.ShowActivity : (bool?)null,
                ActivityHidden = !showActivity,
                Topics = userTopics,
                Comments = userComments
            });
        }

        // PUT: api/users/profile — yalnızca kendi profilini günceller.
        [Authorize]
        [EnableRateLimiting("write")]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);

            if (user == null)
            {
                // Token geçerli ama kullanıcı silinmiş — yeni kayıt oluşturmuyoruz.
                return NotFound(new { error = "Kullanıcı bulunamadı." });
            }

            // Dosyaları önce doğrula, sonra hiçbirini yarım bırakmadan kaydet.
            if (dto.AvatarFile != null && !_uploads.IsValid(dto.AvatarFile, out var avatarError))
            {
                return BadRequest(new { error = avatarError });
            }

            if (dto.BannerFile != null && !_uploads.IsValid(dto.BannerFile, out var bannerError))
            {
                return BadRequest(new { error = bannerError });
            }

            user.Bio = dto.Bio;
            user.GithubUrl = dto.GithubUrl;
            user.WebsiteUrl = dto.WebsiteUrl;

            if (dto.AvatarFile != null)
            {
                user.AvatarUrl = await _uploads.SaveAsync(dto.AvatarFile, "avatar");
            }

            if (dto.BannerFile != null)
            {
                user.BannerUrl = await _uploads.SaveAsync(dto.BannerFile, "banner");
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profil başarıyla güncellendi.",
                avatarUrl = user.AvatarUrl,
                bannerUrl = user.BannerUrl
            });
        }
    }
}
