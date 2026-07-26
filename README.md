# KORSANCIM

Anonim topluluk forumu. ASP.NET Core 8 Web API + SQLite, statik HTML/Tailwind arayüz.

## Yapı

```
ForumApi/
├── Controllers/     Auth, Topics, Comments, Users endpoint'leri
├── Models/          Topic, Comment, User, Category, TopicLike, CommentLike
├── Data/            AppDbContext (EF Core)
├── Migrations/      EF Core migration'ları
├── tailwind.config.js  Tailwind renk/font token'ları (derleme zamanında okunur)
├── tailwind-input.css  Tailwind derlemesinin kaynak dosyası
└── wwwroot/         Arayüz (index, auth, create-topic, topic-detail, profile)
    ├── css/app.css       Tasarım sistemi (renk, kart, buton, rozet)
    ├── css/tailwind.css  Derlenmiş Tailwind çıktısı (git'e dahil, elle düzenlenmez)
    ├── js/app.js         Ortak yardımcılar: header, oturum, XSS kaçışı, rozetler
    └── uploads/          Kullanıcı yüklemeleri (git'e dahil değil)
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

### CSS (Tailwind)

Tailwind, CDN üzerinden değil yerel olarak derlenir — `wwwroot/css/tailwind.css`
git'e dahildir, `dotnet run` bu dosyayı olduğu gibi statik servis eder;
**çalışma zamanında Node gerekmez.** Node yalnızca stil/`tailwind.config.js`
değiştiğinde, geliştirme sırasında gerekir:

```bash
cd ForumApi
npm install        # ilk kurulumda bir kez
npm run build:css  # HTML/JS'teki class'ları tarar, tailwind.css'i yeniden üretir
npm run watch:css  # geliştirirken dosya değişikliklerini izler
```

`tailwind.config.js`'teki renkler `wwwroot/css/app.css`'teki `:root` custom
property'leriyle birebir aynı tutulmalı — biri değişirse diğeri elle
güncellenmeli.

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

### Yönetim uçları (rol gerektirir)

⚙️ = `Admin` veya `Moderator` rolü gerektirir. ⚙️👑 = yalnızca `Admin`.

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/admin/users?search=&page=&pageSize=` ⚙️ | Kullanıcı listesi (rol/ban durumu dahil) |
| POST | `/api/admin/users/{id}/ban` ⚙️ | Kullanıcıyı banla (`{ reason }`) — Admin banlanamaz, kendine uygulanamaz |
| POST | `/api/admin/users/{id}/unban` ⚙️ | Banı kaldır |
| PUT | `/api/admin/users/{id}/role` ⚙️👑 | Rol ata: `User` / `Moderator` / `Admin` |
| DELETE | `/api/admin/topics/{id}` ⚙️ | Konuyu sahiplikten bağımsız sil (moderasyon) |
| DELETE | `/api/admin/comments/{id}` ⚙️ | Yorumu sahiplikten bağımsız sil (moderasyon) |

İlk admin, veritabanı migration'ında `CREATOR` kullanıcı adına otomatik atanır;
sonrasında rol yükseltmesi yalnızca mevcut bir Admin üzerinden yapılabilir
(kendini yükseltme/self-servis rol atama yok).

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
  istekler tek IP'den geliyormuş gibi görünür. Test ortamında
  (`ASPNETCORE_ENVIRONMENT=Testing`) devre dışı bırakılır.
- Rol sistemi (`User` / `Moderator` / `Admin`) ve ban (`IsBanned`, `BanReason`)
  veritabanında tutulur, rol JWT'ye claim olarak gömülür. Banlanan kullanıcı
  hem girişte hem de **mevcut token'ıyla** anında reddedilir — ban, token'ın
  12 saatlik ömrü dolana kadar beklemez (bkz. `Program.cs` içindeki
  kimlik doğrulama sonrası middleware).
- Admin rolündeki bir hesap banlanamaz (kilitlenme riskine karşı); kimse
  kendini banlayamaz veya kendi rolünü değiştiremez.

Henüz yapılmadı: e-posta doğrulama, admin paneli arayüzü (uçlar hazır, arayüz
yok), token iptali (logout sunucuda değil yalnızca istemcide).

## Testler

```bash
dotnet test ForumApi.Tests/ForumApi.Tests.csproj
```

`ForumApi.Tests`, `WebApplicationFactory<Program>` ile her test sınıfı için
izole, bellek-içi bir SQLite veritabanı kurar (gerçek `forum.db`'ye dokunmaz).
Kapsam: kayıt/giriş, sahiplik (başkasının konusunu/yorumunu düzenleyememe),
beğeni toggle'ı ve unique index, arama (konu/yorum/kullanıcı), dosya yükleme
doğrulaması (uzantı/boyut/içerik türü uyuşmazlığı, `.svg` reddi), rol/ban
(yetkisiz erişim, ban→giriş reddi, ban→mevcut token reddi, moderatörün rol
değiştirememesi, Admin'in banlanamaması).

## Geçmiş

Projenin ilk sürümü C++ (Crow + SQLite) ile yazılmıştı; JWT, PBKDF2 şifre
hash'leme, rate limiter ve rol bazlı yetkilendirme içeriyordu. .NET'e geçişle
birlikte kaldırıldı — kod `ee67107` öncesi commit'lerde mevcuttur.
