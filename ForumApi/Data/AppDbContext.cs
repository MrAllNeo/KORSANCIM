using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Topic> Topics { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<User> Users { get; set; }

        // YENİ BEĞENİ TABLOLARI
        public DbSet<TopicLike> TopicLikes { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }
    }
}