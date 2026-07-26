# KORSANCIM

Anonim topluluk forumu. ASP.NET Core 8 Web API + SQLite, statik HTML/Tailwind arayüz.

## Yapı

```
ForumApi/
├── Controllers/     Auth, Topics, Comments, Users endpoint'leri
├── Models/          Topic, Comment, User, TopicLike, CommentLike
├── Data/            AppDbContext (EF Core)
├── Migrations/      EF Core migration'ları
└── wwwroot/         Arayüz (index, auth, create-topic, topic-detail, profile)
    └── uploads/     Kullanıcı yüklemeleri (git'e dahil değil)
```

## Çalıştırma

```bash
dotnet run --project ForumApi
```

Varsayılan adres: http://localhost:5085 — kök URL `wwwroot/index.html`'i açar.

### JWT imzalama anahtarı

Token'lar `Jwt:Key` ile imzalanır. Ortam değişkeni olarak verilir:

```bash
Jwt__Key='en-az-32-karakterlik-rastgele-bir-deger' dotnet run --project ForumApi
```

Development'ta tanımlanmazsa her başlatmada geçici bir anahtar üretilir (uyarı
loglanır, sunucu yeniden başlayınca oturumlar düşer). **Production'da tanımlı
değilse uygulama başlamaz.**

## API

| Metot | Yol | Açıklama |
|---|---|---|
🔒 = `Authorization: Bearer <token>` gerektirir. Bu uçlarda yazar/kullanıcı
kimliği **token'dan** okunur; istemcinin gönderdiği kullanıcı adı dikkate alınmaz.

| Metot | Yol | Açıklama |
|---|---|---|
| POST | `/api/auth/register` | Kayıt |
| POST | `/api/auth/login` | Giriş — token döner (12 saat geçerli) |
| GET | `/api/topics?categoryId=` | Konu listesi (kategoriye göre filtreli) |
| GET | `/api/topics/{id}` | Konu detayı |
| POST | `/api/topics` 🔒 | Konu oluştur (multipart, görsel ekli) |
| POST | `/api/topics/{id}/like` 🔒 | Konu beğen / beğeniyi geri al |
| GET | `/api/topics/{id}/like` 🔒 | Bu konuyu beğenmiş miyim? |
| GET | `/api/comments/topic/{topicId}` | Konunun yorumları |
| POST | `/api/comments` 🔒 | Yorum yaz |
| POST | `/api/comments/{id}/like` 🔒 | Yorum beğen / beğeniyi geri al |
| GET | `/api/users/profile/{username}` | Profil + son 50 konu ve yorum |
| PUT | `/api/users/profile` 🔒 | Kendi profilini güncelle (multipart) |
| GET | `/api/search?q=&limit=` | Konu, yanıt ve kullanıcılarda arama |

Arama terimi en az 2 karakter olmalı, `limit` en fazla 50. Yanıt gövdesi
`{ query, topics, comments, users, totals }` şeklinde gruplanmış döner.
Arayüzde `index.html?q=aranan` ile doğrudan sonuç sayfasına bağlanılabilir.

Dosya yükleme: yalnızca JPG/PNG/GIF/WEBP, dosya başına 5 MB, konu başına en
fazla 5 dosya. Dosyalar rastgele adla kaydedilir, kullanıcının verdiği ad
kullanılmaz.

## Veritabanı

SQLite (`ForumApi/forum.db`). Şema migration'lardan kurulur — uygulama açılışta
`Database.Migrate()` çağırır. Model değişikliğinden sonra:

```bash
dotnet ef migrations add DegisiklikAdi --project ForumApi
```

## Güvenlik Notları

- Şifreler ASP.NET Core `PasswordHasher` ile (PBKDF2-HMAC-SHA256) saklanır.
- Korumalı uçlar JWT bearer token ister; kimlik token'dan okunur, istemcinin
  gönderdiği kullanıcı adına güvenilmez.
- Yüklemeler tür/boyut whitelist'inden geçer, rastgele adla kaydedilir. `.svg`
  bilerek dışarıda — içine script gömülebiliyor.
- Tüm yanıtlarda `X-Content-Type-Options: nosniff`.
- Arayüz kullanıcı içeriğini `escapeHtml()` (bkz. `wwwroot/js/app.js`) ile
  kaçırarak basar; `innerHTML`'e ham veri gömülmemeli.
- `(TopicId, Username)` ve `(CommentId, Username)` üzerinde unique index var;
  beğeni sayacı `COUNT` ile türetilir.

Henüz yapılmadı: rate limiting, e-posta doğrulama, rol/moderasyon sistemi,
token iptali (logout sunucuda değil yalnızca istemcide).

## Geçmiş

Projenin ilk sürümü C++ (Crow + SQLite) ile yazılmıştı; JWT, PBKDF2 şifre
hash'leme, rate limiter ve rol bazlı yetkilendirme içeriyordu. .NET'e geçişle
birlikte kaldırıldı — kod `ee67107` öncesi commit'lerde mevcuttur.
