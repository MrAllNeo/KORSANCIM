using System;
using System.Collections.Generic;

namespace ForumApi.Models
{
    public class Badge
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // lucide ikon anahtarı, opsiyonel.
        public string? Icon { get; set; }

        public string ColorTheme { get; set; } = BadgeThemes.Plain;

        // Kayar ışık + degrade isim efekti (bkz. css/app.css .tier-shine).
        public bool Shine { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<User> Users { get; set; } = new();
    }
}
