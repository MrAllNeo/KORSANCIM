using System;

namespace ForumApi.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsTermsAccepted { get; set; } = false; // Anonimlik & Kural Onayı
        
        // Yeni Profil & Özelleştirme Alanları
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public bool HideEmail { get; set; } = true;
        public bool ShowActivity { get; set; } = true;

        // Yetkilendirme — bkz. Roles.cs. Varsayılan: sıradan kullanıcı.
        public string Role { get; set; } = Roles.User;

        public bool IsBanned { get; set; } = false;
        public string? BanReason { get; set; }

        // Yönetim panelinden atanan kozmetik ünvan rozeti — opsiyonel.
        public int? BadgeId { get; set; }
        public Badge? Badge { get; set; }

        // E-posta doğrulama — doğrulanana kadar konu/yorum yazılamaz (bkz.
        // TopicsController/CommentController). Token tek kullanımlık, süresi
        // dolunca ResendVerification ile yenilenir.
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}