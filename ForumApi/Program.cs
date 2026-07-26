using System.Text;
using System.Threading.RateLimiting;
using ForumApi.Data;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// SQLite Veritabanı Servisi
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=forum.db"));

// CORS Politikası — arayüz aynı origin'den servis ediliyor, ancak API'yi
// ayrı bir istemciden çağırabilmek için açık bırakıldı. Kimlik doğrulama
// Authorization header'ı ile yapıldığı için cookie tabanlı CSRF riski yok.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Şifre hash'leme (PBKDF2-HMAC-SHA256, ASP.NET Core varsayılanı)
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// JWT imzalama anahtarı — Jwt__Key ortam değişkeninden okunur.
var jwtKey = TokenService.ResolveKey(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    LoggerFactory.Create(c => c.AddConsole()).CreateLogger("Startup"));

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ForumApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ForumApi";

builder.Services.AddSingleton(new TokenService(jwtKey, jwtIssuer, jwtAudience));
builder.Services.AddScoped<FileUploadService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ────────────────────────────────────────────
// Bölümleme IP bazlı. Ters vekil (nginx vb.) arkasına konursa
// ForwardedHeaders eklenmeli, yoksa tüm istekler tek IP'den görünür.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Genel tavan — normal gezinmeyi engellemeyecek kadar geniş.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1)
        }));

    // Giriş/kayıt — kaba kuvvet denemesini yavaşlatır.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(5)
        }));

    // Yazma işlemleri — konu/yorum/beğeni seli engellenir.
    options.AddPolicy("write", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1)
        }));

    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Çok fazla istek gönderdiniz. Lütfen biraz bekleyip tekrar deneyin."
        }, token);
    };

    // Giriş yapmış kullanıcıyı kendi adıyla, anonimi IP ile bölümle.
    static string ClientKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? "user:" + context.User.Identity.Name
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor");
});

builder.Services.AddControllers();

// Model doğrulama hataları da arayüzün beklediği { error: "..." } şeklinde dönsün
// (varsayılan RFC 9110 ProblemDetails formatı yerine).
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Geçersiz istek.";

        return new BadRequestObjectResult(new { error = message });
    };
});

var app = builder.Build();

// Veritabanı şemasını migration'lardan kur/güncelle.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Yüklenen dosyalar wwwroot/uploads altında; UseStaticFiles onları zaten
// /uploads yolundan servis ediyor. nosniff, tarayıcının içerik türünü
// tahmin edip görseli script/HTML gibi çalıştırmasını engeller.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.UseDefaultFiles(); // kök URL -> wwwroot/index.html
app.UseStaticFiles();  // wwwroot altındaki html/css/js ve /uploads

// Bilinmeyen sayfa isteklerinde 404.html göster. /api/ altındaki yollar
// hariç — onlar JSON hata dönmeli, HTML değil.
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var path = context.HttpContext.Request.Path;

    if (response.StatusCode == StatusCodes.Status404NotFound &&
        !path.StartsWithSegments("/api"))
    {
        response.ContentType = "text/html; charset=utf-8";
        await response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "404.html"));
    }
});

app.UseCors("AllowAll");
app.UseAuthentication();

// Statik dosyalardan sonra: sayfa/görsel istekleri sayaca girmesin,
// kimlik doğrulamadan sonra: bölümleme kullanıcı adını görebilsin.
app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers();

app.Run();
