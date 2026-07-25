using System;
using System.Collections.Generic;

namespace ForumApi.Models
{
    public class Topic
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? FileUrlsJson { get; set; } // Yüklenen dosyaların/videoların yolları (Opsiyonel)
        public bool IsLegalTermsAccepted { get; set; } = false; // Yasal Sorumluluk Onayı (ZORUNLU)
        public string AuthorUsername { get; set; } = "Anonim";
        
        // YENİ: Beğeni Sayısı
        public int LikeCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<Comment> Comments { get; set; } = new();
    }
}