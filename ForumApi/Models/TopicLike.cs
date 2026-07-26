namespace ForumApi.Models
{
    public class TopicLike
    {
        public int Id { get; set; }

        public int TopicId { get; set; }
        public Topic? Topic { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
