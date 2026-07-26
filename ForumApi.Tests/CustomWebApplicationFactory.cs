using ForumApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForumApi.Tests
{
    // Her test sınıfı için izole, bellek-içi bir SQLite veritabanı kurar.
    // Program.cs zaten Startup'ta Database.Migrate() çağırdığından şema ve
    // seed verisi (kategoriler) otomatik olarak bu bağlantı üzerinde oluşur.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public CustomWebApplicationFactory()
        {
            _connection.Open();

            // Program.cs, builder.Build() çağrılmadan ÖNCE Jwt:Key'i senkron
            // olarak okuyor. WebApplicationFactory'nin ConfigureAppConfiguration
            // kancası ise ancak Build() sırasında devreye giriyor — yani o ana
            // kadar çok geç kalıyor. Ortam değişkeni ise CreateBuilder(args)
            // içindeki AddEnvironmentVariables() ile daha en baştan yüklendiği
            // için bu satıra zamanında yetişiyor.
            Environment.SetEnvironmentVariable("Jwt__Key", "test-only-signing-key-32-characters-minimum!!");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "ForumApi.Tests");
            Environment.SetEnvironmentVariable("Jwt__Audience", "ForumApi.Tests");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing" ortamı Program.cs'te rate limiting'i devre dışı bırakır —
            // aksi halde onlarca test hesabı aynı TestServer "IP"sini paylaşıp
            // birbirini 429'a düşürür.
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }
}
