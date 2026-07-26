# KORSANCIM

Anonim topluluk forumu. ASP.NET Core 8 Web API + SQLite, statik HTML/Tailwind arayüz.

## Yapı

```
ForumApi/
├── Controllers/     Auth, Topics, Comments, Users endpoint'leri
├── Models/          Topic, Comment, User, Category, TopicLike, CommentLike
├── Data/            AppDbContext (EF Core)
├── Migrations/      EF Core migration'ları
└── wwwroot/         Arayüz (index, auth, create-topic, topic-detail, profile)
    ├── css/app.css  Tasarım sistemi (renk, kart, buton, rozet)
    ├── js/theme.js   Tailwind renk token'ları
    ├── js/app.js     Ortak yardımcılar: header, oturum, XSS kaçışı, rozetler
    └── uploads/      Kullanıcı yüklemeleri (git'e dahil değil)
```

Arayüz "sakin derinlik" temasını kullanır: düz koyu yüzeyler, ayrımı kenarlık
taşır, tek vurgu rengi. Gradyan ve parlama yalnızca ünvan rozetlerinde.

## Çalıştırma

```bash
dotnet run --project ForumApi
```

Geliştirme sırasında sunucuyu güvenilir biçimde yeniden başlatmak için:

```bash
scripts/dev-server.sh restart   # start | stop | restart
```

(Süreç `dotnet` değil `ForumApi` adlı apphost olarak göründüğü için ada göre
`pkill` yanıltıcı olabiliyor; script portu yoklayarak doğruluyor.)

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

🔒 = `Authorization: Bearer <token>` gerektirir. Bu uçlarda yazar/kullanıcı
kimliği **token'dan** okunur; istemcinin gönderdiği kullanıcı adı dikkate alınmaz.
👤 = yalnızca içeriğin sahibi çağırabilir, başkası `403` alır.

Konu listesi varsayılan 20, en fazla 50 kayıt döner; yanıt
`{ items, page, pageSize, total, totalPages, hasMore }` şeklindedir.

| Metot | Yol | Açıklama |
|---|---|---|
| POST | `/api/auth/register` | Kayıt |
| POST | `/api/auth/login` | Giriş — token döner (12 saat geçerli) |
| GET | `/api/topics?categoryId=&page=&pageSize=` | Sayfalı konu listesi |
| GET | `/api/topics/{id}` | Konu detayı |
| POST | `/api/topics` 🔒 | Konu oluştur (multipart, görsel ekli) |
| PUT | `/api/topics/{id}` 🔒👤 | Konuyu düzenle |
| DELETE | `/api/topics/{id}` 🔒👤 | Konuyu, yanıtlarını, beğenilerini ve eklerini sil |
| POST | `/api/topics/{id}/like` 🔒 | Konu beğen / beğeniyi geri al |
| GET | `/api/topics/{id}/like` 🔒 | Bu konuyu beğenmiş miyim? |
| GET | `/api/comments/topic/{topicId}` | Konunun yorumları |
| POST | `/api/comments` 🔒 | Yorum yaz |
| PUT | `/api/comments/{id}` 🔒👤 | Yorumu düzenle |
| DELETE | `/api/comments/{id}` 🔒👤 | Yorumu ve beğenilerini sil |
| POST | `/api/comments/{id}/like` 🔒 | Yorum beğen / beğeniyi geri al |
| GET | `/api/users/profile/{username}` | Profil + son 50 konu ve yorum |
| PUT | `/api/users/profile` 🔒 | Kendi profilini güncelle (multipart) |
| GET | `/api/search?q=&limit=` | Konu, yanıt ve kullanıcılarda arama |
| GET | `/api/categories` | Kategori listesi + kategori başına konu sayısı |
| GET | `/api/comments/topic/{id}/likes` 🔒 | Bu konuda beğendiğim yanıtların id'leri |

Arama terimi en az 2 karakter olmalı, `limit` en fazla 50. Yanıt gövdesi
`{ query, topics, comments, users, totals }` şeklinde gruplanmış döner.
Arayüzde `index.html?q=aranan` ile doğrudan sonuç sayfasına bağlanılabilir.

Dosya yükleme: yalnızca JPG/PNG/GIF/WEBP, dosya başına 5 MB, konu başına en
fazla 5 dosya. Dosyalar rastgele adla kaydedilir, kullanıcının verdiği ad
kullanılmaz.

## Veritabanı

SQLite (`ForumApi/forum.db`). Şema migration'lardan kurulur — uygulama açılışta
`Database.Migrate()` çağırır.

İçerik artık kullanıcı adı string'i yerine gerçek foreign key'lerle bağlı:

```
Topics.UserId       -> Users.Id       ON DELETE CASCADE
Topics.CategoryId   -> Categories.Id  ON DELETE RESTRICT
Comments.UserId     -> Users.Id       ON DELETE CASCADE
Comments.TopicId    -> Topics.Id      ON DELETE CASCADE
TopicLikes/CommentLikes -> ilgili kayıt ve kullanıcı, ON DELETE CASCADE
```

Kategori silmek Restrict — o kategorideki konular kazara uçmasın diye önce
taşınmaları gerekir. Model değişikliğinden sonra:

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
- `(TopicId, UserId)` ve `(CommentId, UserId)` üzerinde unique index var;
  beğeni sayacı `COUNT` ile türetilir.
- Yazar/beğenen kimliği veritabanı seviyesinde foreign key ile bağlı; kullanıcı
  adı değişse veya kullanıcı silinse bile yetim kayıt kalmaz.
- Rate limiting IP (anonim) veya kullanıcı adı (girişli) bazında bölümlenir:
  genel 120 istek/dk, giriş-kayıt 8 istek/5dk, yazma işlemleri 30 istek/dk.
  Aşıldığında `429` + `Retry-After` döner. Statik dosyalar sayaca dahil değildir.
  **Ters vekil arkasına konursa** `ForwardedHeaders` eklenmeli, yoksa tüm
  istekler tek IP'den geliyormuş gibi görünür.

Henüz yapılmadı: e-posta doğrulama, rol/moderasyon sistemi, token iptali
(logout sunucuda değil yalnızca istemcide), otomatik test.

## Geçmiş

Projenin ilk sürümü C++ (Crow + SQLite) ile yazılmıştı; JWT, PBKDF2 şifre
hash'leme, rate limiter ve rol bazlı yetkilendirme içeriyordu. .NET'e geçişle
birlikte kaldırıldı — kod `ee67107` öncesi commit'lerde mevcuttur.
