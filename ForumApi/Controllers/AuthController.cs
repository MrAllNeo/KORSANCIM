using System.ComponentModel.DataAnnotations;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
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
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext context, IPasswordHasher<User> passwordHasher, TokenService tokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
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
            // Model doğrulaması [ApiController] tarafından otomatik yapılıyor;
            // hata formatı Program.cs'te { error } olarak ayarlandı.
            var userExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()
                            || u.Email.ToLower() == dto.Email.ToLower());

            if (userExists)
            {
                return BadRequest(new { error = "Bu kullanıcı adı veya e-posta adresi zaten kullanılıyor." });
            }

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Kayıt başarılı! Şimdi giriş yapabilirsiniz.",
                username = user.Username
            });
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
                expiresInHours = TokenService.ExpiryHours,
                username = user.Username,
                email = user.Email,
                userId = user.Id,
                role = user.Role,
                avatarUrl = user.AvatarUrl,
                badge = BadgeSummary.From(user.Badge)
            });
        }
    }
}
