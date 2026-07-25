namespace ForumApi.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int TopicId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = "Anonim";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}