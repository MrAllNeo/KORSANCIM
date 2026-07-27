using ForumApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ForumApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Topic> Topics { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }

        // BEĞENİ TABLOLARI
        public DbSet<TopicLike> TopicLikes { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Badge> Badges { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            base.ConfigureConventions(builder);

            // SQLite tarihleri Kind=Unspecified olarak geri veriyor; bu da JSON'a
            // zaman dilimi işareti olmadan yazılmasına ve tarayıcının yerel saat
            // sanmasına yol açıyordu. Okurken UTC olarak işaretliyoruz.
            builder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();
            builder.Properties<DateTime?>()
                .HaveConversion<NullableUtcDateTimeConverter>();
        }

        private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public UtcDateTimeConverter() : base(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            { }
        }

        private sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
        {
            public NullableUtcDateTimeConverter() : base(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
            { }
        }

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
                .HasIndex(l => new { l.TopicId, l.UserId })
                .IsUnique();

            modelBuilder.Entity<CommentLike>()
                .HasIndex(l => new { l.CommentId, l.UserId })
                .IsUnique();

            // ── İlişkiler ────────────────────────────────────────
            // Kullanıcı silinirse ürettiği içerik de gider; konu silinirse
            // yanıtları ve beğenileri gider. Artık veritabanı garanti ediyor,
            // uygulama katmanının elle toplamasına gerek yok.
            modelBuilder.Entity<Topic>()
                .HasOne(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Kategori silmek o kategorideki tüm konuları uçurmasın —
            // bilerek Restrict: önce konular taşınmalı.
            modelBuilder.Entity<Topic>()
                .HasOne(t => t.Category).WithMany(c => c.Topics)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Topic).WithMany(t => t.Comments)
                .HasForeignKey(c => c.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User).WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicLike>()
                .HasOne(l => l.Topic).WithMany(t => t.Likes)
                .HasForeignKey(l => l.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicLike>()
                .HasOne(l => l.User).WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommentLike>()
                .HasOne(l => l.Comment).WithMany(c => c.Likes)
                .HasForeignKey(l => l.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommentLike>()
                .HasOne(l => l.User).WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Şikayet eden silinirse şikayet de gider (Cascade, diğer içerik
            // ilişkileriyle tutarlı); işlemi yapan admin silinirse şikayet
            // kaydı kalır, yalnızca "kim işledi" bilgisi boşa düşer (SetNull).
            modelBuilder.Entity<Report>()
                .HasOne(r => r.Reporter).WithMany()
                .HasForeignKey(r => r.ReporterUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.HandledBy).WithMany()
                .HasForeignKey(r => r.HandledByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Report>()
                .HasIndex(r => new { r.Status, r.CreatedAt });

            // Rozet silinirse kullanıcılar rozetsiz kalır, banlanmaz (SetNull).
            modelBuilder.Entity<User>()
                .HasOne(u => u.Badge).WithMany(b => b.Users)
                .HasForeignKey(u => u.BadgeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Kategoriler — slug ve ad benzersiz, başlangıç verisi migration ile gelir.
            modelBuilder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Yazılım & Kodlama", Slug = "yazilim-kodlama", Icon = "code", DisplayOrder = 1, Description = "C++, C#, Python, web ve kodlama dünyası" },
                new Category { Id = 2, Name = "Donanım & Sistem", Slug = "donanim-sistem", Icon = "cpu", DisplayOrder = 2, Description = "Bilgisayar toplama, işletim sistemi, ağ ve sunucu" },
                new Category { Id = 3, Name = "Geyik & Sohbet", Slug = "geyik-sohbet", Icon = "coffee", DisplayOrder = 3, Description = "Serbest kürsü — teknoloji dışı her şey" }
            );
        }
    }
}
