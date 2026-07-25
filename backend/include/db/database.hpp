#ifndef DATABASE_HPP
#define DATABASE_HPP

#include <sqlite3.h>
#include <string>
#include <vector>
#include <mutex>

namespace Korsancim {

    struct User {
        int         id;
        std::string username;
        std::string role;          // "user", "moderator", "admin"
        bool        is_banned;
        std::string ban_reason;
        std::string created_at;
    };

    struct Category {
        int         id;
        std::string name;
        std::string description;
        std::string slug;
    };

    struct Topic {
        int         id;
        int         category_id;
        int         user_id;
        std::string title;
        std::string content;
        std::string created_at;
        std::string author_name;
    };

    struct Comment {
        int         id;
        int         topic_id;
        int         user_id;
        std::string content;
        std::string created_at;
        std::string author_name;
    };

    struct PagedResult {
        int total_count;   // Toplam kayıt sayısı (pagination için)
        int page;
        int limit;
    };

    class Database {
    private:
        std::string  db_path;
        sqlite3*     db;
        mutable std::mutex db_mutex;  // Thread-safety için mutex

    public:
        Database(const std::string& db_path);
        ~Database();

        bool connect();
        void disconnect();
        bool execute(const std::string& sql);
        sqlite3* get_db_handle() const { return db; }

        // ── Kullanıcı İşlemleri ──────────────────────────────────
        bool user_exists(const std::string& username);
        bool register_user(const std::string& username, const std::string& raw_password);
        bool authenticate_user(const std::string& username, const std::string& raw_password);
        bool get_user_by_username(const std::string& username, User& out_user);

        // ── Admin / Ban İşlemleri ─────────────────────────────────
        bool ban_user(int user_id, const std::string& reason);
        bool unban_user(int user_id);
        bool update_user_role(int user_id, const std::string& new_role);
        std::vector<User> get_all_users();

        // ── Kategori İşlemleri ────────────────────────────────────
        std::vector<Category> get_categories();

        // ── Konu (Topic) İşlemleri ───────────────────────────────
        bool create_topic(int category_id, int user_id, const std::string& title, const std::string& content);
        std::vector<Topic> get_topics(int category_id = 0, int page = 1, int limit = 20);
        int  get_topic_count(int category_id = 0);
        bool delete_topic(int topic_id);
        bool update_topic(int topic_id, int user_id, const std::string& title, const std::string& content);

        // ── Yorum (Comment) İşlemleri ────────────────────────────
        bool create_comment(int topic_id, int user_id, const std::string& content);
        std::vector<Comment> get_comments_by_topic(int topic_id);
        bool update_comment(int comment_id, int user_id, const std::string& content);
        bool delete_comment(int comment_id, int user_id, const std::string& role);
    };

} // namespace Korsancim

#endif