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
        public string? BannerUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public bool HideEmail { get; set; } = true;
        public bool ShowActivity { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}