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
} // namespace Korsancim