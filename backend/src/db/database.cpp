#include "db/database.hpp"

namespace Korsancim {

// Kurucu Fonksiyon (Constructor): Bağlantıyı varsayılan olarak nullptr (boş pointer) başlatır
Database::Database(const std::string& path) : db(nullptr), db_path(path) {}

// Yıkıcı Fonksiyon (Destructor): Sınıf sonlandığında otomatik çalışır
Database::~Database() {
    disconnect();
}

// 1. Veritabanına Bağlanma
bool Database::connect() {
    // sqlite3_open: Veritabanı dosyasını açar, yoksa oluşturur
    int result = sqlite3_open(db_path.c_str(), &db);
    
    if (result != SQLITE_OK) {
        std::cerr << "❌ Veritabanı bağlantı hatası: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }

    std::cout << "✅ Veritabanına başarıyla bağlandı: " << db_path << std::endl;
    return true;
}

// 2. Bağlantıyı Kapatma
void Database::disconnect() {
    if (db != nullptr) {
        sqlite3_close(db);
        db = nullptr;
        std::cout << "🔌 Veritabanı bağlantısı kapatıldı." << std::endl;
    }
}

// 3. Raw SQL Çalıştırma (INSERT, UPDATE, DELETE vb.)
bool Database::execute(const std::string& sql) {
    if (db == nullptr) {
        std::cerr << "❌ Hata: Veritabanı bağlı değil!" << std::endl;
        return false;
    }

    char* errorMessage = nullptr;
    // sqlite3_exec: SQL komutunu doğrudan çalıştırır
    int result = sqlite3_exec(db, sql.c_str(), nullptr, nullptr, &errorMessage);

    if (result != SQLITE_OK) {
        std::cerr << "❌ SQL Hata: " << errorMessage << std::endl;
        sqlite3_free(errorMessage); // Bellek sızıntısını önlemek için hata mesajını temizliyoruz
        return false;
    }

    return true;
}
// Tüm kategorileri veritabanından çekip liste olarak döndüren fonksiyon
std::vector<Category> Database::get_categories() {
    std::vector<Category> categories;

    if (db == nullptr) {
        std::cerr << "❌ Hata: Veritabanı bağlı değil!" << std::endl;
        return categories;
    }

    std::string sql = "SELECT id, name, description, slug FROM categories;";
    sqlite3_stmt* stmt;

    // 1. SQL sorgusunu hazırla (prepare)
    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Sorgu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return categories;
    }

    // 2. Satır satır verileri döngüyle oku (step)
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Category cat;
        cat.id = sqlite3_column_int(stmt, 0);
        cat.name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
        cat.description = sqlite3_column_text(stmt, 2) ? reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)) : "";
        cat.slug = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));

        categories.push_back(cat);
    }

    // 3. Belleği ve statement'ı temizle (finalize)
    sqlite3_finalize(stmt);

    return categories;
}
// 1. Kullanıcı adının var olup olmadığını kontrol eder
bool Database::user_exists(const std::string& username) {
    if (db == nullptr) return false;

    std::string sql = "SELECT id FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        return false;
    }

    // '?' parametresine kullanıcı adını bağlıyoruz (SQL Injection Önleme)
    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_STATIC);

    bool exists = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        exists = true; // Eğer satır döndüyse kullanıcı adı zaten var!
    }

    sqlite3_finalize(stmt);
    return exists;
}

// 2. Yeni kullanıcı kaydı oluşturur
bool Database::register_user(const std::string& username, const std::string& password_hash) {
    if (db == nullptr) return false;

    // Önce kullanıcı adı alınmış mı bakıyoruz
    if (user_exists(username)) {
        std::cerr << "⚠️ Kullanıcı adı zaten kullanımda: " << username << std::endl;
        return false;
    }

    std::string sql = "INSERT INTO users (username, password_hash) VALUES (?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Kayıt sorgusu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }

    // Parametreleri güvenli şekilde bağlıyoruz
    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(stmt, 2, password_hash.c_str(), -1, SQLITE_STATIC);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);

    if (success) {
        std::cout << "👤 Yeni anonim kullanıcı kaydedildi: " << username << std::endl;
    } else {
        std::cerr << "❌ Kullanıcı kaydı başarısız: " << sqlite3_errmsg(db) << std::endl;
    }

    sqlite3_finalize(stmt);
    return success;
}
// Kullanıcı doğrulama (Login)
bool Database::authenticate_user(const std::string& username, const std::string& password) {
    if (db == nullptr) return false;

    std::string sql = "SELECT password_hash FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        return false;
    }

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_STATIC);

    bool authenticated = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        std::string stored_password = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        // Şimdilik Düz Metin Kontrolü (İleride Hash Karşılaştırması Yapacağız)
        if (stored_password == password) {
            authenticated = true;
        }
    }

    sqlite3_finalize(stmt);
    return authenticated;
}
// 1. Yeni Konu Oluşturma
bool Database::create_topic(int category_id, int user_id, const std::string& title, const std::string& content) {
    if (db == nullptr) return false;

    std::string sql = "INSERT INTO topics (category_id, user_id, title, content) VALUES (?, ?, ?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Konu ekleme sorgusu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }

    sqlite3_bind_int(stmt, 1, category_id);
    sqlite3_bind_int(stmt, 2, user_id);
    sqlite3_bind_text(stmt, 3, title.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(stmt, 4, content.c_str(), -1, SQLITE_STATIC);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    if (success) {
        std::cout << "📌 Yeni konu açıldı: " << title << std::endl;
    } else {
        std::cerr << "❌ Konu açma başarısız: " << sqlite3_errmsg(db) << std::endl;
    }

    sqlite3_finalize(stmt);
    return success;
}

// 2. Konuları Listeleme (JOIN kullanarak yazarı da çekiyoruz)
std::vector<Topic> Database::get_topics(int category_id) {
    std::vector<Topic> topics;
    if (db == nullptr) return topics;

    std::string sql = "SELECT t.id, t.category_id, t.user_id, t.title, t.content, t.created_at, u.username "
                      "FROM topics t "
                      "JOIN users u ON t.user_id = u.id ";

    if (category_id > 0) {
        sql += "WHERE t.category_id = ? ";
    }
    sql += "ORDER BY t.created_at DESC;";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Konuları çekme sorgusu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return topics;
    }

    if (category_id > 0) {
        sqlite3_bind_int(stmt, 1, category_id);
    }

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Topic t;
        t.id = sqlite3_column_int(stmt, 0);
        t.category_id = sqlite3_column_int(stmt, 1);
        t.user_id = sqlite3_column_int(stmt, 2);
        t.title = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        t.content = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
        t.created_at = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        t.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 6));

        topics.push_back(t);
    }

    sqlite3_finalize(stmt);
    return topics;
}
// 1. Yeni Yorum Ekleme
bool Database::create_comment(int topic_id, int user_id, const std::string& content) {
    if (db == nullptr) return false;

    std::string sql = "INSERT INTO comments (topic_id, user_id, content) VALUES (?, ?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Yorum ekleme sorgusu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }

    sqlite3_bind_int(stmt, 1, topic_id);
    sqlite3_bind_int(stmt, 2, user_id);
    sqlite3_bind_text(stmt, 3, content.c_str(), -1, SQLITE_STATIC);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    if (success) {
        std::cout << "💬 Yeni yorum eklendi (Topic ID: " << topic_id << ")" << std::endl;
    } else {
        // Hatanın tam SQLite sebebini terminale basıyoruz:
        std::cerr << "❌ Yorum ekleme başarısız! SQLite Hatası: " << sqlite3_errmsg(db) << std::endl;
    }

    sqlite3_finalize(stmt);
    return success;
}

// 2. Bir Konuya Ait Yorumları Listeleme
std::vector<Comment> Database::get_comments_by_topic(int topic_id) {
    std::vector<Comment> comments;
    if (db == nullptr) return comments;

    std::string sql = "SELECT c.id, c.topic_id, c.user_id, c.content, c.created_at, u.username "
                      "FROM comments c "
                      "JOIN users u ON c.user_id = u.id "
                      "WHERE c.topic_id = ? "
                      "ORDER BY c.created_at ASC;";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Yorumları çekme sorgusu hazırlanamadı: " << sqlite3_errmsg(db) << std::endl;
        return comments;
    }

    sqlite3_bind_int(stmt, 1, topic_id);

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Comment c;
        c.id = sqlite3_column_int(stmt, 0);
        c.topic_id = sqlite3_column_int(stmt, 1);
        c.user_id = sqlite3_column_int(stmt, 2);
        c.content = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        c.created_at = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
        c.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));

        comments.push_back(c);
    }

    sqlite3_finalize(stmt);
    return comments;
}
} // namespace Korsancim