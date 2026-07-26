using System;
using System.Collections.Generic;

namespace ForumApi.Models
{
    public class Topic
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public string? FileUrlsJson { get; set; } // Yüklenen dosyaların yolları (opsiyonel)
        public bool IsLegalTermsAccepted { get; set; } = false; // Yasal Sorumluluk Onayı (ZORUNLU)

        public int LikeCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Düzenlendiyse son düzenleme zamanı; hiç düzenlenmediyse null.
        public DateTime? UpdatedAt { get; set; }

        // ── İlişkiler ────────────────────────────────────────────
        // Yazar artık string değil, gerçek bir kayda bağlı: kullanıcı adını
        // değiştirse bile içerik sahipsiz kalmaz.
        public int UserId { get; set; }
        public User? User { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public List<Comment> Comments { get; set; } = new();
        public List<TopicLike> Likes { get; set; } = new();
    }
}
