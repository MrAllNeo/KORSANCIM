namespace ForumApi.Models
{
    public class TopicLike
    {
        public int Id { get; set; }
        public int TopicId { get; set; }
        public string Username { get; set; } = string.Empty;
    }
}