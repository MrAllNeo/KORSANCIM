using System;

namespace ForumApi.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int TopicId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = string.Empty;

        // YENİ: Beğeni Sayısı
        public int LikeCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}