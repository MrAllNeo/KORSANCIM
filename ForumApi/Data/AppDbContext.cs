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

        // BEĞENİ TABLOLARI
        public DbSet<TopicLike> TopicLikes { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kullanıcı adı ve e-posta benzersiz olmalı.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Bir kullanıcı aynı konuyu/yorumu yalnızca bir kez beğenebilir.
            // Eşzamanlı isteklerde sayacın bozulmasını veritabanı seviyesinde engeller.
            modelBuilder.Entity<TopicLike>()
                .HasIndex(l => new { l.TopicId, l.Username })
                .IsUnique();

            modelBuilder.Entity<CommentLike>()
                .HasIndex(l => new { l.CommentId, l.Username })
                .IsUnique();
        }
    }
}
