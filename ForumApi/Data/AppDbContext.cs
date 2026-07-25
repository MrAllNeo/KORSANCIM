using Microsoft.EntityFrameworkCore;
using ForumApi.Models;

namespace ForumApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<Comment> Comments => Set<Comment>();
    }
}