using System.Text;
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

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
