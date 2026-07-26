using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ForumApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace ForumApi.Services
{
    public class TokenService
    {
        public const int ExpiryHours = 12;

        private readonly SymmetricSecurityKey _key;
        private readonly string _issuer;
        private readonly string _audience;

        public TokenService(SymmetricSecurityKey key, string issuer, string audience)
        {
            _key = key;
            _issuer = issuer;
            _audience = audience;
        }

        // Anahtarı konfigürasyondan okur. Development'ta anahtar yoksa geçici bir
        // anahtar üretir (sunucu yeniden başlayınca tokenlar geçersiz olur).
        // Production'da anahtar zorunludur, yoksa uygulama başlamaz.
        public static SymmetricSecurityKey ResolveKey(IConfiguration config, bool isDevelopment, ILogger logger)
        {
            var configured = config["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(configured))
            {
                if (!isDevelopment)
                {
                    throw new InvalidOperationException(
                        "JWT imzalama anahtarı tanımlı değil. Jwt__Key ortam değişkenini " +
                        "en az 32 karakterlik rastgele bir değerle ayarlayın.");
                }

                logger.LogWarning(
                    "Jwt:Key tanımlı değil — geliştirme için geçici anahtar üretildi. " +
                    "Sunucu yeniden başladığında tüm oturumlar düşecek.");

                var generated = RandomNumberGenerator.GetBytes(32);
                return new SymmetricSecurityKey(generated);
            }

            var bytes = Encoding.UTF8.GetBytes(configured);
            if (bytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Key en az 32 bayt (karakter) olmalıdır; HMAC-SHA256 bunu gerektirir.");
            }

            return new SymmetricSecurityKey(bytes);
        }

        public string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(ExpiryHours),
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
