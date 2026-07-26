using System;
using System.Collections.Generic;

namespace ForumApi.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;

        public int LikeCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Düzenlendiyse son düzenleme zamanı; hiç düzenlenmediyse null.
        public DateTime? UpdatedAt { get; set; }

        // ── İlişkiler ────────────────────────────────────────────
        public int TopicId { get; set; }
        public Topic? Topic { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public List<CommentLike> Likes { get; set; } = new();
    }
}
