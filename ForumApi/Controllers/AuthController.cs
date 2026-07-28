using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
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
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const int VerificationTokenExpiryHours = 24;

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public AuthController(AppDbContext context, IPasswordHasher<User> passwordHasher, TokenService tokenService,
            EmailService emailService, IWebHostEnvironment env)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _emailService = emailService;
            _env = env;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string GenerateVerificationToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        private async Task SendVerificationEmailAsync(User user)
        {
            var link = $"{Request.Scheme}://{Request.Host}/verify-email.html?token={user.EmailVerificationToken}";
            await _emailService.SendVerificationEmailAsync(user.Email, user.Username, link);
        }

        // Kayıt Modeli
        public class RegisterDto
        {
            [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
            [StringLength(32, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-32 karakter olmalıdır.")]
            [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam, _ . - içerebilir.")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "E-posta zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Şifre zorunludur.")]
            [StringLength(128, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
            public string Password { get; set; } = string.Empty;
        }

        // Giriş Modeli
        public class LoginDto
        {
            [Required]
            public string UsernameOrEmail { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;
        }

        // 1. Kullanıcı Kaydı (POST: /api/auth/register)
        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var settings = await _context.SiteSettings.FindAsync(1);
            if (settings?.RegistrationOpen == false)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Kayıtlar şu anda kapalı." });
            }

            // Model doğrulaması [ApiController] tarafından otomatik yapılıyor;
            // hata formatı Program.cs'te { error } olarak ayarlandı.
            var userExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()
                            || u.Email.ToLower() == dto.Email.ToLower());

            if (userExists)
            {
                return BadRequest(new { error = "Bu kullanıcı adı veya e-posta adresi zaten kullanılıyor." });
            }

            // Test paketinde onlarca test kaydolduğu anda konu/yorum yazabilmeyi
            // varsayıyor; e-posta gönderimini/doğrulama akışını test etmek
            // isteyen tek yer ForumApi.Tests/EmailVerificationTests.cs, orada
            // doğrulanmamış durum doğrudan veritabanına yazılarak kuruluyor.
            var isTesting = _env.IsEnvironment("Testing");

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = isTesting,
                EmailVerificationToken = isTesting ? null : GenerateVerificationToken(),
                EmailVerificationTokenExpiresAt = isTesting ? null : DateTime.UtcNow.AddHours(VerificationTokenExpiryHours)
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (!isTesting)
            {
                await SendVerificationEmailAsync(user);
            }

            return Ok(new
            {
                success = true,
                message = "Kayıt başarılı! E-posta adresine gönderilen bağlantıyla doğrulama yaptıktan sonra konu/yorum yazabilirsin.",
                username = user.Username
            });
        }

        public class VerifyEmailDto
        {
            [Required(ErrorMessage = "Token zorunludur.")]
            public string Token { get; set; } = string.Empty;
        }

        // POST: api/auth/verify-email
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == dto.Token);

            if (user == null || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { error = "Doğrulama bağlantısı geçersiz veya süresi dolmuş." });
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "E-posta adresin doğrulandı." });
        }

        // POST: api/auth/resend-verification — girişli kullanıcı kendi
        // doğrulamasını yeniler (eskisi süresi dolmuş veya e-posta kaybolmuşsa).
        [Authorize]
        [EnableRateLimiting("auth")]
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification()
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound(new { error = "Kullanıcı bulunamadı." });

            if (user.IsEmailVerified)
            {
                return BadRequest(new { error = "E-posta adresin zaten doğrulanmış." });
            }

            user.EmailVerificationToken = GenerateVerificationToken();
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);
            await _context.SaveChangesAsync();

            await SendVerificationEmailAsync(user);

            return Ok(new { message = "Doğrulama e-postası tekrar gönderildi." });
        }

        // 2. Kullanıcı Girişi (POST: /api/auth/login)
        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.Include(u => u.Badge).FirstOrDefaultAsync(u =>
                u.Username.ToLower() == dto.UsernameOrEmail.ToLower() ||
                u.Email.ToLower() == dto.UsernameOrEmail.ToLower());

            // Kullanıcı adı ile şifre hatasını ayırt etmiyoruz — kullanıcı adı
            // sayımını (enumeration) engellemek için tek ve aynı mesajı dönüyoruz.
            const string invalidCredentials = "Kullanıcı adı/e-posta veya şifre hatalı!";

            if (user == null)
            {
                return BadRequest(new { error = invalidCredentials });
            }

            PasswordVerificationResult result;
            try
            {
                result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            }
            catch (FormatException)
            {
                // Kayıtlı değer geçerli bir hash formatında değil (ör. eski düz
                // metin kayıtlar). Girişi reddet, 500 döndürme.
                return BadRequest(new { error = invalidCredentials });
            }

            if (result == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { error = invalidCredentials });
            }

            if (user.IsBanned)
            {
                var reason = string.IsNullOrWhiteSpace(user.BanReason)
                    ? "Hesabınız askıya alınmış."
                    : $"Hesabınız askıya alınmış: {user.BanReason}";
                return StatusCode(StatusCodes.Status403Forbidden, new { error = reason });
            }

            // Hash parametreleri eskiyse (iterasyon sayısı vb.) sessizce yenile.
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                success = true,
                token = _tokenService.CreateToken(user),
                expiresInHours = TokenService.ExpiryHoursFor(user.Role),
                username = user.Username,
                email = user.Email,
                userId = user.Id,
                role = user.Role,
                avatarUrl = user.AvatarUrl,
                badge = BadgeSummary.From(user.Badge),
                emailVerified = user.IsEmailVerified
            });
        }
    }
}
