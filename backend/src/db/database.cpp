#include "db/database.hpp"
#include "utils/hash_helper.hpp"
#include <iostream>

namespace Korsancim {

    Database::Database(const std::string& db_path) : db_path(db_path), db(nullptr) {}

    Database::~Database() {
        disconnect();
    }

    bool Database::connect() {
        int rc = sqlite3_open(db_path.c_str(), &db);
        if (rc != SQLITE_OK) {
            std::cerr << "❌ Veritabanı açma hatası: " << sqlite3_errmsg(db) << std::endl;
            return false;
        }
        std::cout << "✅ Veritabanına başarıyla bağlandı: " << db_path << std::endl;
        return true;
    }

    void Database::disconnect() {
        if (db) {
            sqlite3_close(db);
            db = nullptr;
            std::cout << "🔌 Veritabanı bağlantısı kapatıldı." << std::endl;
        }
    }

    bool Database::execute(const std::string& sql) {
        char* err_msg = nullptr;
        int rc = sqlite3_exec(db, sql.c_str(), nullptr, nullptr, &err_msg);
        
        if (rc != SQLITE_OK) {
            std::cerr << "❌ SQL Hatası: " << err_msg << std::endl;
            sqlite3_free(err_msg);
            return false;
        }
        return true;
    }

    std::vector<Category> Database::get_categories() {
        std::vector<Category> categories;
        std::string sql = "SELECT id, name, description, slug FROM categories;";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) == SQLITE_OK) {
            while (sqlite3_step(stmt) == SQLITE_ROW) {
                Category cat;
                cat.id = sqlite3_column_int(stmt, 0);
                cat.name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
                cat.description = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
                cat.slug = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
                categories.push_back(cat);
            }
        } else {
            std::cerr << "❌ Kategorileri çekme hatası: " << sqlite3_errmsg(db) << std::endl;
        }

        sqlite3_finalize(stmt);
        return categories;
    }

    bool Database::user_exists(const std::string& username) {
        std::string sql = "SELECT id FROM users WHERE username = ?;";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
            return false;
        }

        sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);

        bool exists = (sqlite3_step(stmt) == SQLITE_ROW);
        sqlite3_finalize(stmt);
        return exists;
    }

    // 1. KULLANICI KAYDI (Şifreyi Hash'leyerek Saklar)
    bool Database::register_user(const std::string& username, const std::string& raw_password) {
        if (user_exists(username)) {
            std::cout << "⚠️ Kullanıcı adı zaten kullanımda: " << username << std::endl;
            return false;
        }

        // Şifreyi Salt + SHA-256 ile maskele
        std::string salt = HashHelper::generate_salt();
        std::string hashed_password = HashHelper::hash_password(raw_password, salt);

        std::string sql = "INSERT INTO users (username, password_hash) VALUES (?, ?);";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
            std::cerr << "❌ Hata (Prepare): " << sqlite3_errmsg(db) << std::endl;
            return false;
        }

        sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 2, hashed_password.c_str(), -1, SQLITE_TRANSIENT);

        bool success = (sqlite3_step(stmt) == SQLITE_DONE);
        sqlite3_finalize(stmt);

        if (success) {
            std::cout << "👤 Yeni kullanıcı güvenli şekilde kaydedildi: " << username << std::endl;
        } else {
            std::cerr << "❌ Kayıt hatası: " << sqlite3_errmsg(db) << std::endl;
        }

        return success;
    }

    // 2. KULLANICI DOĞRULAMA (Maskeli Şifreyi Kontrol Eder)
    bool Database::authenticate_user(const std::string& username, const std::string& raw_password) {
        std::string sql = "SELECT password_hash FROM users WHERE username = ?;";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
            return false;
        }

        sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);

        bool authenticated = false;
        if (sqlite3_step(stmt) == SQLITE_ROW) {
            const unsigned char* stored_hash_ptr = sqlite3_column_text(stmt, 0);
            if (stored_hash_ptr) {
                std::string stored_hash(reinterpret_cast<const char*>(stored_hash_ptr));
                // Maskeli şifreyi HashHelper ile doğrula
                authenticated = HashHelper::verify_password(raw_password, stored_hash);
            }
        }

        sqlite3_finalize(stmt);
        return authenticated;
    }

    bool Database::create_topic(int category_id, int user_id, const std::string& title, const std::string& content) {
        std::string sql = "INSERT INTO topics (category_id, user_id, title, content) VALUES (?, ?, ?, ?);";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
            std::cerr << "❌ Hata (Prepare): " << sqlite3_errmsg(db) << std::endl;
            return false;
        }

        sqlite3_bind_int(stmt, 1, category_id);
        sqlite3_bind_int(stmt, 2, user_id);
        sqlite3_bind_text(stmt, 3, title.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 4, content.c_str(), -1, SQLITE_TRANSIENT);

        bool success = (sqlite3_step(stmt) == SQLITE_DONE);
        sqlite3_finalize(stmt);

        if (success) {
            std::cout << "📌 Yeni konu açıldı: " << title << std::endl;
        } else {
            std::cerr << "❌ Konu açma hatası: " << sqlite3_errmsg(db) << std::endl;
        }

        return success;
    }

    std::vector<Topic> Database::get_topics(int category_id) {
        std::vector<Topic> topics;
        std::string sql = "SELECT t.id, t.category_id, t.user_id, t.title, t.content, t.created_at, u.username "
                          "FROM topics t JOIN users u ON t.user_id = u.id ";
        if (category_id > 0) {
            sql += "WHERE t.category_id = ? ";
        }
        sql += "ORDER BY t.created_at DESC;";

        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) == SQLITE_OK) {
            if (category_id > 0) {
                sqlite3_bind_int(stmt, 1, category_id);
            }

            while (sqlite3_step(stmt) == SQLITE_ROW) {
                Topic top;
                top.id = sqlite3_column_int(stmt, 0);
                top.category_id = sqlite3_column_int(stmt, 1);
                top.user_id = sqlite3_column_int(stmt, 2);
                top.title = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
                top.content = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
                top.created_at = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
                top.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 6));
                topics.push_back(top);
            }
        } else {
            std::cerr << "❌ Konuları çekme hatası: " << sqlite3_errmsg(db) << std::endl;
        }

        sqlite3_finalize(stmt);
        return topics;
    }

    bool Database::create_comment(int topic_id, int user_id, const std::string& content) {
        std::string sql = "INSERT INTO comments (topic_id, user_id, content) VALUES (?, ?, ?);";
        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
            std::cerr << "❌ Hata (Prepare): " << sqlite3_errmsg(db) << std::endl;
            return false;
        }

        sqlite3_bind_int(stmt, 1, topic_id);
        sqlite3_bind_int(stmt, 2, user_id);
        sqlite3_bind_text(stmt, 3, content.c_str(), -1, SQLITE_TRANSIENT);

        bool success = (sqlite3_step(stmt) == SQLITE_DONE);
        sqlite3_finalize(stmt);

        return success;
    }

    std::vector<Comment> Database::get_comments_by_topic(int topic_id) {
        std::vector<Comment> comments;
        std::string sql = "SELECT c.id, c.topic_id, c.user_id, c.content, c.created_at, u.username "
                          "FROM comments c JOIN users u ON c.user_id = u.id "
                          "WHERE c.topic_id = ? ORDER BY c.created_at ASC;";

        sqlite3_stmt* stmt;

        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) == SQLITE_OK) {
            sqlite3_bind_int(stmt, 1, topic_id);

            while (sqlite3_step(stmt) == SQLITE_ROW) {
                Comment com;
                com.id = sqlite3_column_int(stmt, 0);
                com.topic_id = sqlite3_column_int(stmt, 1);
                com.user_id = sqlite3_column_int(stmt, 2);
                com.content = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
                com.created_at = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
                com.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
                comments.push_back(com);
            }
        }

        sqlite3_finalize(stmt);
        return comments;
    }
bool Database::get_user_by_username(const std::string& username, User& out_user) {
    std::string sql = "SELECT id, username, role FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        return false;
    }

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);

    bool found = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        out_user.id = sqlite3_column_int(stmt, 0);
        out_user.username = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
        const unsigned char* role_ptr = sqlite3_column_text(stmt, 2);
        out_user.role = role_ptr ? reinterpret_cast<const char*>(role_ptr) : "user";
        found = true;
    }

    sqlite3_finalize(stmt);
    return found;
}
bool Database::delete_topic(int topic_id) {
    std::string sql = "DELETE FROM topics WHERE id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        return false;
    }

    sqlite3_bind_int(stmt, 1, topic_id);
    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);

    return success;
}
}