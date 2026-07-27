# KORSANCIM

CLAUDE AI ile Kodlandı - CODED WITH CLAUDE AI

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
    ├── js/app.js         Ortak yardımcılar: header, oturum, rol kontrolü, XSS kaçışı, rozet render, şikayet modalı
    ├── panel/            Yönetim paneli (Moderator+) — dashboard, kullanıcılar, içerik, şikayetler, rozetler, kategoriler
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

### Şikayet (Report) uçları

Herkes (girişli) şikayet oluşturabilir; sonucu yalnızca kendi şikayetlerinde görür.
Arayüzde konu/yorum/profil sayfalarındaki bayrak ikonlu "Şikayet Et" butonu
(bkz. `js/app.js` `openReportModal`) bu uca bağlanır — kendi içeriğin için
görünmez.

| Metot | Yol | Açıklama |
|---|---|---|
| POST | `/api/reports` 🔒 | Şikayet oluştur (`{ targetType: Topic\|Comment\|User, targetId, reason, note? }`) |
| GET | `/api/reports/mine` 🔒 | Kendi şikayetlerimin durumu |

Aynı hedefe, işlemi bekleyen bir şikayeti varken tekrar şikayet açılamaz.

### Yönetim uçları (rol gerektirir)

⚙️ = `Moderator`, `Admin` veya `Owner` rolü gerektirir. ⚙️👤 = yalnızca `Admin`
veya `Owner` (Moderator giremez). ⚙️👑 = yalnızca `Owner`.

Rol hiyerarşisi: `User < Moderator < Admin < Owner`. Owner tekildir (site
sahibi), migration'da `CREATOR` kullanıcı adına otomatik atanır ve hiçbir
uçtan yeniden atanamaz. **Admin dahil kimse rol değiştiremez** — bunu yalnızca
Owner yapabilir; bu, adminlerin birbirini terfi/azletmesini imkânsız kılar.

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/admin/stats` ⚙️ | Dashboard verisi: toplu sayımlar, son 14 gün büyüme (üye/konu/yorum), en aktif kullanıcılar, en çok şikayet edilen kullanıcılar, karma aktivite akışı |
| GET | `/api/admin/users?search=&page=&pageSize=` ⚙️ | Kullanıcı listesi (rol/ban/rozet durumu dahil) |
| POST | `/api/admin/users/{id}/ban` ⚙️ | Kullanıcıyı banla (`{ reason }`) — Admin/Owner banlanamaz, kendine uygulanamaz |
| POST | `/api/admin/users/{id}/unban` ⚙️ | Banı kaldır |
| PUT | `/api/admin/users/{id}/role` ⚙️👑 | Rol ata: `User` / `Moderator` / `Admin` (Owner atanamaz) |
| PUT | `/api/admin/users/{id}/badge` ⚙️👤 | Kullanıcıya rozet ata/kaldır (`{ badgeId }`, `null` = kaldır) |
| DELETE | `/api/admin/topics/{id}` ⚙️ | Konuyu sahiplikten bağımsız sil (moderasyon) |
| PUT | `/api/admin/topics/{id}` ⚙️👤 | Konuyu sahiplikten bağımsız düzenle (moderasyon) |
| DELETE | `/api/admin/comments/{id}` ⚙️ | Yorumu sahiplikten bağımsız sil (moderasyon) |
| PUT | `/api/admin/comments/{id}` ⚙️👤 | Yorumu sahiplikten bağımsız düzenle (moderasyon) |
| GET | `/api/admin/reports?status=&page=&pageSize=` ⚙️ | Şikayet kuyruğu (bekleyenler önce), hedef önizlemesi dahil |
| PUT | `/api/admin/reports/{id}/status` ⚙️ | Durum güncelle: `Pending`/`Reviewing`/`Resolved`/`Dismissed` (`{ status, resolutionNote? }`) |
| GET | `/api/admin/badges` ⚙️ | Rozet listesi (kullanıcı sayısı dahil) |
| POST | `/api/admin/badges` ⚙️👑 | Yeni rozet tanımla (`{ name, icon?, colorTheme, shine }`) |
| PUT | `/api/admin/badges/{id}` ⚙️👑 | Rozeti düzenle |
| DELETE | `/api/admin/badges/{id}` ⚙️👑 | Rozeti sil (kullanıcılar rozetsiz kalır) |
| POST | `/api/admin/categories` ⚙️👤 | Kategori oluştur (`{ name, description, icon, displayOrder }`) |
| PUT | `/api/admin/categories/{id}` ⚙️👤 | Kategoriyi düzenle |
| DELETE | `/api/admin/categories/{id}` ⚙️👤 | Kategoriyi sil — içinde konu varsa reddedilir |

Bir Admin'in yetkisi kötüye kullanılırsa çözüm önce Owner'ın onu
`User`/`Moderator`'a indirmesi, sonra gerekirse banlanmasıdır — Admin/Owner
hesapları doğrudan banlanamaz.

Rozet renk teması önceden tanımlı bir setten seçilir (`gold`/`cyan`/`purple`/
`green`/`red`/`plain`, bkz. `Models/BadgeThemes.cs`) — admin'in serbest CSS/renk
girmesine izin verilmiyor, hem tasarım tutarlılığı hem injection riski için.

Arama terimi en az 2 karakter olmalı, `limit` en fazla 50. Yanıt gövdesi
`{ query, topics, comments, users, totals }` şeklinde gruplanmış döner.
Arayüzde `index.html?q=aranan` ile doğrudan sonuç sayfasına bağlanılabilir.

Dosya yükleme: yalnızca JPG/PNG/GIF/WEBP, dosya başına 5 MB, konu başına en
fazla 5 dosya. Dosyalar rastgele adla kaydedilir, kullanıcının verdiği ad
kullanılmaz.

## Yönetim Paneli (Arayüz)

`wwwroot/panel/` altında, ayrı bir uygulama/deploy DEĞİL — aynı statik dosya
sunumunun bir alt yolu. Sayfalar:

| Sayfa | İçerik |
|---|---|
| `panel/index.html` | Dashboard — toplam sayımlar, son 14 gün büyüme grafiği (üye/konu/yorum), en aktif kullanıcılar, en çok şikayet edilenler, kategoriye göre dağılım, karma aktivite akışı |
| `panel/users.html` | Kullanıcı arama + sayfalama, ban/unban, rol değiştirme (yalnızca Owner), rozet atama (Admin+) |
| `panel/content.html` | Tüm konuların listesi (kategori filtresi), doğrudan silme; düzenlemek için konuya girilir |
| `panel/reports.html` | Şikayet kuyruğu — durum filtresi, hedefe git, incelemeye al / çöz / reddet (çözüm notu ile) |
| `panel/badges.html` | Rozet listesi (Moderator+ görür); oluşturma/düzenleme/silme yalnızca Owner |
| `panel/categories.html` | Kategori CRUD (Admin+); içinde konu olan kategori silinemez |

**Erişim modeli — iki katman:**
1. **Gerçek yetki**: her `/api/admin/*` isteği sunucuda `[Authorize(Roles=...)]`
   ile korunur (bkz. yukarıdaki tablo). Bu, panelin tek gerçek güvenlik sınırı.
2. **Arayüz kapısı**: her panel sayfası `requireStaff()` (bkz. `js/app.js`) ile
   açılır — girişli değilse veya rolü Moderator'ın altındaysa siteye geri
   yollar. Bu yalnızca kullanıcı deneyimi içindir; panel HTML/JS dosyaları
   herkese açık statik dosyalardır (URL'i bilen indirebilir), gerçek veriye
   erişim yine token+rol kontrolünden geçer.

Konu/yorum **düzenleme** moderasyonu ayrı bir ekran yerine `topic-detail.html`
üzerinden yapılır: sahip olmayan bir Admin/Owner konuyu görüntülediğinde
Düzenle/Sil butonları otomatik görünür (Moderator yalnızca Sil görür,
matrise uygun), sunucu tarafı `/api/topics/{id}` yerine `/api/admin/topics/{id}`
çağrılır. Ana site header'ında "Panel" linki yalnızca Moderator+ rolündeki
oturumlarda görünür.

Henüz yok (D4'e bırakıldı): denetim kaydı (audit log — kim ne zaman ne
yaptı), site ayarları ekranı (kayıt aç/kapa, bakım modu, duyuru şeridi),
panel'e özel daha sıkı rate limiting ve kısa ömürlü admin token'ı,
subdomain + reverse-proxy şifresi (bilerek "yayına çıkış" partisine ertelendi).

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
- Rol sistemi (`User` / `Moderator` / `Admin` / `Owner`) ve ban (`IsBanned`,
  `BanReason`) veritabanında tutulur, rol JWT'ye claim olarak gömülür. Banlanan
  kullanıcı hem girişte hem de **mevcut token'ıyla** anında reddedilir — ban,
  token'ın 12 saatlik ömrü dolana kadar beklemez (bkz. `Program.cs` içindeki
  kimlik doğrulama sonrası middleware).
- Admin/Owner rolündeki bir hesap banlanamaz (kilitlenme riskine karşı); kimse
  kendini banlayamaz. Rol değişikliği yalnızca Owner'a açık — Admin bile rol
  atayamaz/azledemez.

Rozet sistemi veritabanında (`Badges` tablosu, `Users.BadgeId`) ve yönetim
panelinden atanır — kod içinde sabit kullanıcı adı eşleşmesi kalmadı.

Henüz yapılmadı: e-posta doğrulama, denetim kaydı (audit log), token iptali
(logout sunucuda değil yalnızca istemcide).

## Testler

```bash
dotnet test ForumApi.Tests/ForumApi.Tests.csproj
```

`ForumApi.Tests`, `WebApplicationFactory<Program>` ile her test sınıfı için
izole, bellek-içi bir SQLite veritabanı kurar (gerçek `forum.db`'ye dokunmaz).
Kapsam: kayıt/giriş, sahiplik (başkasının konusunu/yorumunu düzenleyememe),
beğeni toggle'ı ve unique index, arama (konu/yorum/kullanıcı), dosya yükleme
doğrulaması (uzantı/boyut/içerik türü uyuşmazlığı, `.svg` reddi), rol/ban
(yetkisiz erişim, ban→giriş reddi, ban→mevcut token reddi, Admin'in rol
değiştirememesi, yalnızca Owner'ın değiştirebilmesi, Owner'ın Owner atayamaması/
başka Owner'ı değiştirememesi, Admin/Owner'ın banlanamaması, Admin'in
başkasının konusunu düzenleyebilmesi ama Moderator'ın düzenleyememesi,
dashboard istatistik ucunun rol koruması),
şikayet sistemi (oluşturma, tekrar şikayeti reddetme, moderatörün listeleyip
çözümleyebilmesi, sıradan kullanıcının listeyi görememesi),
rozet/kategori yönetimi (rozet CRUD'unun yalnızca Owner'a açık olması, geçersiz
tema reddi, rozet atamanın Admin+ ile Moderator arasındaki farkı, var olmayan
rozet atama reddi, kategori CRUD'unun Admin+ ile Moderator arasındaki farkı,
içinde konu olan kategorinin silinememesi, yinelenen kategori adı reddi).

## Geçmiş

Projenin ilk sürümü C++ (Crow + SQLite) ile yazılmıştı; JWT, PBKDF2 şifre
hash'leme, rate limiter ve rol bazlı yetkilendirme içeriyordu. .NET'e geçişle
birlikte kaldırıldı — kod `ee67107` öncesi commit'lerde mevcuttur.
