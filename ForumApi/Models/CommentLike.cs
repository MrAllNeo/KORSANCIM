namespace ForumApi.Models
{
    public class CommentLike
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public string Username { get; set; } = string.Empty;
    }
}