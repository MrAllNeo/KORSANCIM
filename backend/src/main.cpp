#include "crow.h"
#include "db/database.hpp"
#include "middleware/rate_limiter.hpp"
#include "utils/logger.hpp"
#include "config/app_config.hpp"

// API Route Kayıt Fonksiyonları
#include "api/auth_routes.hpp"
#include "api/topic_routes.hpp"
#include "api/comment_routes.hpp"
#include "api/admin_routes.hpp"

int main() {
    // ── 1. Konfigürasyonu Yükle ───────────────────────────────
    Korsancim::AppConfig config;
    try {
        config = Korsancim::AppConfig::from_env();
    } catch (const std::exception& e) {
        Korsancim::Logger::error(std::string("Konfigürasyon hatası: ") + e.what());
        return 1;
    }

    // ── 2. JWT Secret Kontrolü (erken hata için) ──────────────
    try {
        // JwtHelper::generate_token içinde lazy load ediliyor; burada erken doğrula
        Korsancim::JwtHelper::generate_token(0, "_startup_check_", "user");
    } catch (const std::exception& e) {
        Korsancim::Logger::error(std::string("JWT Secret hatası: ") + e.what());
        Korsancim::Logger::error("Lütfen KORSANCIM_JWT_SECRET ortam değişkenini tanımlayın.");
        return 1;
    }

    // ── 3. Veritabanı Bağlantısı ──────────────────────────────
    Korsancim::Database db(config.db_path);
    if (!db.connect()) {
        Korsancim::Logger::error("Veritabanına bağlanılamadı! Çıkılıyor...");
        return 1;
    }

    // ── 4. Tabloları Hazırla (İlk Çalıştırma) ────────────────
    db.execute(
        "CREATE TABLE IF NOT EXISTS users ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  username TEXT UNIQUE NOT NULL,"
        "  password_hash TEXT NOT NULL,"
        "  role TEXT DEFAULT 'user',"
        "  is_banned INTEGER DEFAULT 0,"
        "  ban_reason TEXT DEFAULT '',"
        "  created_at DATETIME DEFAULT CURRENT_TIMESTAMP"
        ");"
    );
    db.execute(
        "CREATE TABLE IF NOT EXISTS categories ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  name TEXT UNIQUE NOT NULL,"
        "  description TEXT,"
        "  slug TEXT UNIQUE NOT NULL"
        ");"
    );
    db.execute(
        "CREATE TABLE IF NOT EXISTS topics ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  category_id INTEGER NOT NULL,"
        "  user_id INTEGER NOT NULL,"
        "  title TEXT NOT NULL,"
        "  content TEXT NOT NULL,"
        "  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,"
        "  FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE CASCADE,"
        "  FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE"
        ");"
    );
    db.execute(
        "CREATE TABLE IF NOT EXISTS comments ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  topic_id INTEGER NOT NULL,"
        "  user_id INTEGER NOT NULL,"
        "  content TEXT NOT NULL,"
        "  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,"
        "  FOREIGN KEY(topic_id) REFERENCES topics(id) ON DELETE CASCADE,"
        "  FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE"
        ");"
    );

    // Performans indexleri
    db.execute("CREATE INDEX IF NOT EXISTS idx_topics_category ON topics(category_id);");
    db.execute("CREATE INDEX IF NOT EXISTS idx_comments_topic ON comments(topic_id);");
    db.execute("CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);");

    // Varsayılan kategoriler
    db.execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES "
               "(1, 'Genel Sohbet', 'Gereksiz sohbetlerin ve muhabbetin adresi', 'genel-sohbet');");
    db.execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES "
               "(2, 'Yazılım & Teknoloji', 'C++, Linux, Python ve kodlama dünyası', 'yazilim-teknoloji');");
    db.execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES "
               "(3, 'Özgür Yazılım & Linux', 'Linux dağıtımları ve açık kaynak araçlar', 'ozgur-yazilim-linux');");

    // ── 5. Uygulama ve Rate Limiter ───────────────────────────
    crow::App<crow::CORSHandler> app;
    auto& cors = app.get_middleware<crow::CORSHandler>();
    cors.global()
        .headers("Authorization, Content-Type")
        .methods("GET, POST, PUT, DELETE, OPTIONS"_method)
        .origin("*");
    Korsancim::RateLimiter rate_limiter(config.rate_limit_max, config.rate_limit_window_sec);

    app.loglevel(crow::LogLevel::Warning);

    // ── 6. CORS — Preflight OPTIONS Handler ───────────────────
    CROW_ROUTE(app, "/<path>").methods(crow::HTTPMethod::OPTIONS)([](const crow::request&, const std::string&) {
        crow::response res(204);
        res.add_header("Access-Control-Allow-Origin",  "*");
        res.add_header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        res.add_header("Access-Control-Allow-Headers", "Authorization, Content-Type");
        return res;
    });

    // ── 7. Route Kayıt ────────────────────────────────────────
    Korsancim::register_auth_routes(app, db, rate_limiter);
    Korsancim::register_topic_routes(app, db);
    Korsancim::register_comment_routes(app, db);
    Korsancim::register_admin_routes(app, db);

    // ── 8. X-Content-Type-Options header (güvenlik) ──────────
    // CORS başlıkları crow::CORSHandler middleware tarafından ekleniyor.

    // ── 9. Sunucuyu Başlat ────────────────────────────────────
    Korsancim::Logger::info("🚀 KORSANCIM Backend " + std::to_string(config.port) + " Portunda Başlatılıyor...");
    app.port(config.port).multithreaded().run();

    return 0;
}