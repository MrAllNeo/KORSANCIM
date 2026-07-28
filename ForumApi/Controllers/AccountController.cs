using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    // Kullanıcının kendi hesap güvenliği ayarları — kullanıcı adı, e-posta,
    // şifre değişikliği ve hesap silme. Profil özelleştirme (bio/avatar/banner)
    // UsersController'da kalıyor; burası kimlik/güvenlik tarafı.
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private const int VerificationTokenExpiryHours = 24;

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        private readonly FileUploadService _uploads;
        private readonly IWebHostEnvironment _env;

        public AccountController(AppDbContext context, IPasswordHasher<User> passwordHasher, TokenService tokenService,
            EmailService emailService, FileUploadService uploads, IWebHostEnvironment env)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _emailService = emailService;
            _uploads = uploads;
            _env = env;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string GenerateVerificationToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        private bool VerifyPassword(User user, string currentPassword) =>
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) != PasswordVerificationResult.Failed;

        public class ChangeUsernameDto
        {
            [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
            [StringLength(32, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-32 karakter olmalıdır.")]
            [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam, _ . - içerebilir.")]
            public string NewUsername { get; set; } = string.Empty;

            [Required(ErrorMessage = "Onay için şifreniz gereklidir.")]
            public string CurrentPassword { get; set; } = string.Empty;
        }

        public class ChangeEmailDto
        {
            [Required(ErrorMessage = "E-posta zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
            public string NewEmail { get; set; } = string.Empty;

            [Required(ErrorMessage = "Onay için şifreniz gereklidir.")]
            public string CurrentPassword { get; set; } = string.Empty;
        }

        public class ChangePasswordDto
        {
            [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Yeni şifre zorunludur.")]
            [StringLength(128, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
            public string NewPassword { get; set; } = string.Empty;
        }

        public class DeleteAccountDto
        {
            [Required(ErrorMessage = "Onay için şifreniz gereklidir.")]
            public string CurrentPassword { get; set; } = string.Empty;
        }

        public class UpdatePrivacyDto
        {
            public bool ShowActivity { get; set; } = true;
        }

        // PUT: api/account/username — JWT'de kullanıcı adı claim'i olduğu için
        // başarılı değişiklikte yeni bir token dönülür; istemci onu kaydetmezse
        // eski token'la yapılan "bana ait mi" kontrolleri eski adı görmeye devam eder.
        [EnableRateLimiting("auth")]
        [HttpPut("username")]
        public async Task<IActionResult> ChangeUsername([FromBody] ChangeUsernameDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (!VerifyPassword(user, dto.CurrentPassword))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Şifre doğrulanamadı." });
            }

            var exists = await _context.Users.AnyAsync(u => u.Id != user.Id && u.Username.ToLower() == dto.NewUsername.ToLower());
            if (exists)
            {
                return BadRequest(new { error = "Bu kullanıcı adı zaten kullanılıyor." });
            }

            user.Username = dto.NewUsername;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Kullanıcı adın güncellendi.",
                username = user.Username,
                token = _tokenService.CreateToken(user),
                expiresInHours = TokenService.ExpiryHoursFor(user.Role)
            });
        }

        // PUT: api/account/email — yeni adres doğrulanana kadar konu/yorum
        // yazma kilitlenir (IsEmailVerified=false), tıpkı yeni kayıt gibi.
        [EnableRateLimiting("auth")]
        [HttpPut("email")]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (!VerifyPassword(user, dto.CurrentPassword))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Şifre doğrulanamadı." });
            }

            var exists = await _context.Users.AnyAsync(u => u.Id != user.Id && u.Email.ToLower() == dto.NewEmail.ToLower());
            if (exists)
            {
                return BadRequest(new { error = "Bu e-posta adresi zaten kullanılıyor." });
            }

            // Kayıttaki Testing-otomatik-doğrulama bypass'ı yalnızca ilk kayıt
            // için var (bkz. AuthController.Register) — e-posta değişikliği
            // yeni bir adresi doğrulamak zorunda, ortamdan bağımsız.
            user.Email = dto.NewEmail;
            user.IsEmailVerified = false;
            user.EmailVerificationToken = GenerateVerificationToken();
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);

            await _context.SaveChangesAsync();

            if (!_env.IsEnvironment("Testing"))
            {
                var link = $"{Request.Scheme}://{Request.Host}/verify-email.html?token={user.EmailVerificationToken}";
                await _emailService.SendVerificationEmailAsync(user.Email, user.Username, link);
            }

            return Ok(new
            {
                message = "E-posta adresin güncellendi. Yeni adresine gönderilen bağlantıyla doğrulaman gerekiyor.",
                email = user.Email,
                emailVerified = user.IsEmailVerified
            });
        }

        // PUT: api/account/password
        [EnableRateLimiting("auth")]
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (!VerifyPassword(user, dto.CurrentPassword))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Mevcut şifre hatalı." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Şifren güncellendi." });
        }

        // PUT: api/account/privacy — hassas değil, şifre istemiyor.
        [EnableRateLimiting("write")]
        [HttpPut("privacy")]
        public async Task<IActionResult> UpdatePrivacy([FromBody] UpdatePrivacyDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            user.ShowActivity = dto.ShowActivity;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gizlilik ayarları güncellendi.", showActivity = user.ShowActivity });
        }

        // DELETE: api/account — geri alınamaz. Kullanıcı silinince konuları ve
        // yorumları da veritabanı cascade'i ile gider (bkz. AppDbContext); burada
        // yalnızca diskteki dosyaları (avatar/banner/konu ekleri) elle temizliyoruz.
        [EnableRateLimiting("auth")]
        [HttpDelete]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (!VerifyPassword(user, dto.CurrentPassword))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Şifre doğrulanamadı." });
            }

            var fileUrls = new List<string>();
            if (!string.IsNullOrEmpty(user.AvatarUrl)) fileUrls.Add(user.AvatarUrl);
            if (!string.IsNullOrEmpty(user.BannerUrl)) fileUrls.Add(user.BannerUrl);

            var topicFileJsons = await _context.Topics
                .Where(t => t.UserId == user.Id && t.FileUrlsJson != null)
                .Select(t => t.FileUrlsJson!)
                .ToListAsync();

            foreach (var json in topicFileJsons)
            {
                var urls = JsonSerializer.Deserialize<List<string>>(json);
                if (urls != null) fileUrls.AddRange(urls);
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _uploads.DeleteAll(fileUrls);

            return Ok(new { message = "Hesabın ve tüm içeriğin kalıcı olarak silindi." });
        }
    }
}
