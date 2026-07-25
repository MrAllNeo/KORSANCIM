#include "db/database.hpp"
#include "utils/hash_helper.hpp"
#include "utils/logger.hpp"

namespace Korsancim {

Database::Database(const std::string& db_path) : db_path(db_path), db(nullptr) {}

Database::~Database() {
    disconnect();
}

bool Database::connect() {
    std::lock_guard<std::mutex> lock(db_mutex);
    int rc = sqlite3_open(db_path.c_str(), &db);
    if (rc != SQLITE_OK) {
        Logger::error("Veritabanı açma hatası: " + std::string(sqlite3_errmsg(db)));
        return false;
    }
    // WAL modu: multi-thread okuma performansını artırır
    sqlite3_exec(db, "PRAGMA journal_mode=WAL;", nullptr, nullptr, nullptr);
    sqlite3_exec(db, "PRAGMA synchronous=NORMAL;", nullptr, nullptr, nullptr);
    sqlite3_exec(db, "PRAGMA foreign_keys=ON;", nullptr, nullptr, nullptr);

    Logger::info("Veritabanına başarıyla bağlandı: " + db_path);
    return true;
}

void Database::disconnect() {
    std::lock_guard<std::mutex> lock(db_mutex);
    if (db) {
        sqlite3_close(db);
        db = nullptr;
        Logger::info("Veritabanı bağlantısı kapatıldı.");
    }
}

bool Database::execute(const std::string& sql) {
    std::lock_guard<std::mutex> lock(db_mutex);
    char* err_msg = nullptr;
    int rc = sqlite3_exec(db, sql.c_str(), nullptr, nullptr, &err_msg);

    if (rc != SQLITE_OK) {
        Logger::error("SQL Hatası: " + std::string(err_msg));
        sqlite3_free(err_msg);
        return false;
    }
    return true;
}

// ─────────────────────────────────────────────────────────────
// KATEGORİLER
// ─────────────────────────────────────────────────────────────

std::vector<Category> Database::get_categories() {
    std::lock_guard<std::mutex> lock(db_mutex);
    std::vector<Category> categories;
    const char* sql = "SELECT id, name, description, slug FROM categories ORDER BY id ASC;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) == SQLITE_OK) {
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            Category cat;
            cat.id          = sqlite3_column_int(stmt, 0);
            cat.name        = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
            cat.description = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
            cat.slug        = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
            categories.push_back(cat);
        }
    } else {
        Logger::error("Kategorileri çekme hatası: " + std::string(sqlite3_errmsg(db)));
    }

    sqlite3_finalize(stmt);
    return categories;
}

// ─────────────────────────────────────────────────────────────
// KULLANICI İŞLEMLERİ
// ─────────────────────────────────────────────────────────────

bool Database::user_exists(const std::string& username) {
    // db_mutex caller tarafından tutulmuyor, kendi lock'unu alıyoruz
    // (private çağrılar için iç çözüm — deadlock riskini önler)
    const char* sql = "SELECT id FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);
    bool exists = (sqlite3_step(stmt) == SQLITE_ROW);
    sqlite3_finalize(stmt);
    return exists;
}

bool Database::register_user(const std::string& username, const std::string& raw_password) {
    std::lock_guard<std::mutex> lock(db_mutex);

    if (user_exists(username)) {
        Logger::warn("Kullanıcı adı zaten kullanımda: " + username);
        return false;
    }

    std::string salt            = HashHelper::generate_salt();
    std::string hashed_password = HashHelper::hash_password(raw_password, salt);

    const char* sql = "INSERT INTO users (username, password_hash) VALUES (?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        Logger::error("Kullanıcı kayıt prepare hatası: " + std::string(sqlite3_errmsg(db)));
        return false;
    }

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, hashed_password.c_str(), -1, SQLITE_TRANSIENT);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);

    if (success) Logger::info("Yeni kullanıcı kaydedildi: " + username);
    else         Logger::error("Kayıt DB hatası: " + std::string(sqlite3_errmsg(db)));

    return success;
}

bool Database::authenticate_user(const std::string& username, const std::string& raw_password) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "SELECT password_hash FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);

    bool authenticated = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        const unsigned char* stored = sqlite3_column_text(stmt, 0);
        if (stored) {
            authenticated = HashHelper::verify_password(raw_password,
                reinterpret_cast<const char*>(stored));
        }
    }

    sqlite3_finalize(stmt);
    return authenticated;
}

bool Database::get_user_by_username(const std::string& username, User& out_user) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "SELECT id, username, role, is_banned, ban_reason, created_at "
                      "FROM users WHERE username = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, username.c_str(), -1, SQLITE_TRANSIENT);

    bool found = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        out_user.id       = sqlite3_column_int(stmt, 0);
        out_user.username = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));

        auto role_ptr     = sqlite3_column_text(stmt, 2);
        out_user.role     = role_ptr ? reinterpret_cast<const char*>(role_ptr) : "user";

        out_user.is_banned = (sqlite3_column_int(stmt, 3) == 1);

        auto reason_ptr    = sqlite3_column_text(stmt, 4);
        out_user.ban_reason = reason_ptr ? reinterpret_cast<const char*>(reason_ptr) : "";

        auto date_ptr      = sqlite3_column_text(stmt, 5);
        out_user.created_at = date_ptr ? reinterpret_cast<const char*>(date_ptr) : "";

        found = true;
    }

    sqlite3_finalize(stmt);
    return found;
}

// ─────────────────────────────────────────────────────────────
// ADMIN / BAN İŞLEMLERİ
// ─────────────────────────────────────────────────────────────

std::vector<User> Database::get_all_users() {
    std::lock_guard<std::mutex> lock(db_mutex);
    std::vector<User> users;
    const char* sql = "SELECT id, username, role, is_banned, ban_reason, created_at "
                      "FROM users ORDER BY id ASC;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) == SQLITE_OK) {
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            User u;
            u.id       = sqlite3_column_int(stmt, 0);
            u.username = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));

            auto role_ptr = sqlite3_column_text(stmt, 2);
            u.role        = role_ptr ? reinterpret_cast<const char*>(role_ptr) : "user";

            u.is_banned = (sqlite3_column_int(stmt, 3) == 1);

            auto reason_ptr = sqlite3_column_text(stmt, 4);
            u.ban_reason    = reason_ptr ? reinterpret_cast<const char*>(reason_ptr) : "";

            auto date_ptr   = sqlite3_column_text(stmt, 5);
            u.created_at    = date_ptr ? reinterpret_cast<const char*>(date_ptr) : "";

            users.push_back(u);
        }
    }

    sqlite3_finalize(stmt);
    return users;
}

bool Database::ban_user(int user_id, const std::string& reason) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "UPDATE users SET is_banned = 1, ban_reason = ? WHERE id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, reason.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 2, user_id);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);
    return success;
}

bool Database::unban_user(int user_id) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "UPDATE users SET is_banned = 0, ban_reason = '' WHERE id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_int(stmt, 1, user_id);
    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);
    return success;
}

bool Database::update_user_role(int user_id, const std::string& new_role) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "UPDATE users SET role = ? WHERE id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, new_role.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 2, user_id);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);
    return success;
}

// ─────────────────────────────────────────────────────────────
// KONU (TOPIC) İŞLEMLERİ
// ─────────────────────────────────────────────────────────────

bool Database::create_topic(int category_id, int user_id, const std::string& title, const std::string& content) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "INSERT INTO topics (category_id, user_id, title, content) VALUES (?, ?, ?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        Logger::error("Konu oluşturma prepare hatası: " + std::string(sqlite3_errmsg(db)));
        return false;
    }

    sqlite3_bind_int(stmt, 1, category_id);
    sqlite3_bind_int(stmt, 2, user_id);
    sqlite3_bind_text(stmt, 3, title.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, content.c_str(), -1, SQLITE_TRANSIENT);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);

    if (success) Logger::info("Yeni konu oluşturuldu: " + title);
    else         Logger::error("Konu DB hatası: " + std::string(sqlite3_errmsg(db)));

    return success;
}

std::vector<Topic> Database::get_topics(int category_id, int page, int limit) {
    std::lock_guard<std::mutex> lock(db_mutex);
    std::vector<Topic> topics;

    if (page < 1)  page  = 1;
    if (limit < 1 || limit > 100) limit = 20;
    int offset = (page - 1) * limit;

    std::string sql =
        "SELECT t.id, t.category_id, t.user_id, t.title, t.content, t.created_at, u.username "
        "FROM topics t JOIN users u ON t.user_id = u.id ";

    if (category_id > 0) sql += "WHERE t.category_id = ? ";
    sql += "ORDER BY t.created_at DESC LIMIT ? OFFSET ?;";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) {
        Logger::error("Konuları çekme hatası: " + std::string(sqlite3_errmsg(db)));
        return topics;
    }

    int bind_idx = 1;
    if (category_id > 0) sqlite3_bind_int(stmt, bind_idx++, category_id);
    sqlite3_bind_int(stmt, bind_idx++, limit);
    sqlite3_bind_int(stmt, bind_idx,   offset);

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Topic top;
        top.id          = sqlite3_column_int(stmt, 0);
        top.category_id = sqlite3_column_int(stmt, 1);
        top.user_id     = sqlite3_column_int(stmt, 2);
        top.title       = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        top.content     = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
        top.created_at  = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        top.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 6));
        topics.push_back(top);
    }

    sqlite3_finalize(stmt);
    return topics;
}

int Database::get_topic_count(int category_id) {
    std::lock_guard<std::mutex> lock(db_mutex);
    std::string sql = "SELECT COUNT(*) FROM topics";
    if (category_id > 0) sql += " WHERE category_id = ?";
    sql += ";";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) return 0;

    if (category_id > 0) sqlite3_bind_int(stmt, 1, category_id);

    int count = 0;
    if (sqlite3_step(stmt) == SQLITE_ROW) count = sqlite3_column_int(stmt, 0);

    sqlite3_finalize(stmt);
    return count;
}

bool Database::delete_topic(int topic_id) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "DELETE FROM topics WHERE id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_int(stmt, 1, topic_id);
    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    sqlite3_finalize(stmt);
    return success;
}

bool Database::update_topic(int topic_id, int user_id, const std::string& title, const std::string& content) {
    std::lock_guard<std::mutex> lock(db_mutex);
    // Sadece konunun sahibi güncelleyebilir
    const char* sql = "UPDATE topics SET title = ?, content = ? WHERE id = ? AND user_id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, title.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, content.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 3, topic_id);
    sqlite3_bind_int(stmt, 4, user_id);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    int changes  = sqlite3_changes(db);
    sqlite3_finalize(stmt);

    return success && changes > 0;
}

// ─────────────────────────────────────────────────────────────
// YORUM (COMMENT) İŞLEMLERİ
// ─────────────────────────────────────────────────────────────

bool Database::create_comment(int topic_id, int user_id, const std::string& content) {
    std::lock_guard<std::mutex> lock(db_mutex);
    const char* sql = "INSERT INTO comments (topic_id, user_id, content) VALUES (?, ?, ?);";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        Logger::error("Yorum oluşturma prepare hatası: " + std::string(sqlite3_errmsg(db)));
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
    std::lock_guard<std::mutex> lock(db_mutex);
    std::vector<Comment> comments;
    const char* sql =
        "SELECT c.id, c.topic_id, c.user_id, c.content, c.created_at, u.username "
        "FROM comments c JOIN users u ON c.user_id = u.id "
        "WHERE c.topic_id = ? ORDER BY c.created_at ASC;";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        Logger::error("Yorumları çekme hatası: " + std::string(sqlite3_errmsg(db)));
        return comments;
    }

    sqlite3_bind_int(stmt, 1, topic_id);

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Comment com;
        com.id          = sqlite3_column_int(stmt, 0);
        com.topic_id    = sqlite3_column_int(stmt, 1);
        com.user_id     = sqlite3_column_int(stmt, 2);
        com.content     = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        com.created_at  = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
        com.author_name = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        comments.push_back(com);
    }

    sqlite3_finalize(stmt);
    return comments;
}

bool Database::update_comment(int comment_id, int user_id, const std::string& content) {
    std::lock_guard<std::mutex> lock(db_mutex);
    // Sadece yorumun sahibi güncelleyebilir
    const char* sql = "UPDATE comments SET content = ? WHERE id = ? AND user_id = ?;";
    sqlite3_stmt* stmt;

    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) return false;

    sqlite3_bind_text(stmt, 1, content.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 2, comment_id);
    sqlite3_bind_int(stmt, 3, user_id);

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    int changes  = sqlite3_changes(db);
    sqlite3_finalize(stmt);

    return success && changes > 0;
}

bool Database::delete_comment(int comment_id, int user_id, const std::string& role) {
    std::lock_guard<std::mutex> lock(db_mutex);
    // Sahip veya admin/moderator silebilir
    std::string sql;
    sqlite3_stmt* stmt;

    if (role == "admin" || role == "moderator") {
        sql = "DELETE FROM comments WHERE id = ?;";
        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) return false;
        sqlite3_bind_int(stmt, 1, comment_id);
    } else {
        sql = "DELETE FROM comments WHERE id = ? AND user_id = ?;";
        if (sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, nullptr) != SQLITE_OK) return false;
        sqlite3_bind_int(stmt, 1, comment_id);
        sqlite3_bind_int(stmt, 2, user_id);
    }

    bool success = (sqlite3_step(stmt) == SQLITE_DONE);
    int changes  = sqlite3_changes(db);
    sqlite3_finalize(stmt);

    return success && changes > 0;
}

} // namespace Korsancim