using ForumApi.Data;
using ForumApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // Kayıt Modeli
        public class RegisterDto
        {
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // Giriş Modeli
        public class LoginDto
        {
            public string UsernameOrEmail { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // 1. Kullanıcı Kaydı (POST: /api/auth/register)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { error = "Kullanıcı adı ve şifre zorunludur." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email);
            if (userExists)
            {
                return BadRequest(new { error = "Bu kullanıcı adı veya e-posta adresi zaten kullanılıyor." });
            }

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = dto.Password, // İleride hashleme mantığı geliştirilebilir
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Kayıt başarılı! Şimdi giriş yapabilirsiniz.", username = user.Username });
        }

        // 2. Kullanıcı Girişi (POST: /api/auth/login)
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                (u.Username == dto.UsernameOrEmail || u.Email == dto.UsernameOrEmail) && 
                u.PasswordHash == dto.Password);

            if (user == null)
            {
                return BadRequest(new { error = "Kullanıcı adı/e-posta veya şifre hatalı!" });
            }

            return Ok(new { success = true, username = user.Username, email = user.Email, userId = user.Id });
        }
    }
}