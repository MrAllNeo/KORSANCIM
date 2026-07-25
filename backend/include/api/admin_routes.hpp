#ifndef ADMIN_ROUTES_HPP
#define ADMIN_ROUTES_HPP

#include "crow.h"
#include "db/database.hpp"
#include "middleware/auth_middleware.hpp"
#include "utils/logger.hpp"

namespace Korsancim {

template<typename App>
inline void register_admin_routes(App& app, Database& db) {

    // ── 1. TÜM KULLANICILARI LİSTELE (Admin/Moderator) ───────
    app.template route_dynamic("/api/admin/users").methods(crow::HTTPMethod::GET)([&db](const crow::request& req) {
        auto user = require_role(req, "moderator");
        if (!user) return forbidden("Bu endpoint Admin veya Moderator yetkisi gerektiriyor.");

        auto users = db.get_all_users();
        crow::json::wvalue::list user_list;
        for (auto& u : users) {
            crow::json::wvalue item;
            item["id"]         = u.id;
            item["username"]   = u.username;
            item["role"]       = u.role;
            item["is_banned"]  = u.is_banned;
            item["ban_reason"] = u.ban_reason;
            item["created_at"] = u.created_at;
            user_list.push_back(std::move(item));
        }

        crow::json::wvalue res;
        res["users"] = std::move(user_list);
        return crow::response(200, res);
    });

    // ── 2. KONU SİLME (Admin/Moderator) ─────────────────────
    app.template route_dynamic("/api/admin/topics/<int>").methods(crow::HTTPMethod::DELETE)([&db](const crow::request& req, int topic_id) {
        auto user = require_role(req, "moderator");
        if (!user) return forbidden("Bu işlem için Admin veya Moderator yetkisi gereklidir.");

        if (db.delete_topic(topic_id)) {
            Logger::info("Konu silindi (ID: " + std::to_string(topic_id) + ", Silen: " + user->username + ")");
            return crow::response(200, R"({"message": "Konu başarıyla silindi."})");
        }

        return crow::response(500, R"({"error": "Konu silinirken hata oluştu veya konu bulunamadı."})");
    });

    // ── 3. KULLANICI BANLAMA (Admin/Moderator) ───────────────
    app.template route_dynamic("/api/admin/users/<int>/ban").methods(crow::HTTPMethod::POST)([&db](const crow::request& req, int target_user_id) {
        auto user = require_role(req, "moderator");
        if (!user) return forbidden("Bu işlem için Admin veya Moderator yetkisi gereklidir.");

        auto body = crow::json::load(req.body);
        std::string reason = "Kural ihlali";
        if (body && body.has("reason")) {
            reason = std::string(body["reason"].s());
            if (reason.empty() || reason.size() > 500) {
                return crow::response(400, R"({"error": "Ban sebebi 1-500 karakter arasında olmalıdır."})");
            }
        }

        if (db.ban_user(target_user_id, reason)) {
            Logger::warn("Kullanıcı banlandı (User ID: " + std::to_string(target_user_id) +
                         ", Sebep: " + reason + ", Banlayan: " + user->username + ")");
            return crow::response(200, R"({"message": "Kullanıcı başarıyla banlandı."})");
        }

        return crow::response(500, R"({"error": "Banlama işlemi başarısız."})");
    });

    // ── 4. KULLANICI BANINI KALDIR (Admin/Moderator) ─────────
    app.template route_dynamic("/api/admin/users/<int>/unban").methods(crow::HTTPMethod::POST)([&db](const crow::request& req, int target_user_id) {
        auto user = require_role(req, "moderator");
        if (!user) return forbidden("Bu işlem için Admin veya Moderator yetkisi gereklidir.");

        if (db.unban_user(target_user_id)) {
            Logger::info("Kullanıcı banı kaldırıldı (User ID: " + std::to_string(target_user_id) +
                         ", İşlem yapan: " + user->username + ")");
            return crow::response(200, R"({"message": "Kullanıcının banı kaldırıldı."})");
        }

        return crow::response(500, R"({"error": "Ban kaldırma işlemi başarısız."})");
    });

    // ── 5. KULLANICI ROLÜ GÜNCELLE (Sadece Admin) ────────────
    app.template route_dynamic("/api/admin/users/<int>/role").methods(crow::HTTPMethod::PUT)([&db](const crow::request& req, int target_user_id) {
        auto user = require_role(req, "admin");
        if (!user) return forbidden("Sadece Admin rol değiştirebilir.");

        auto body = crow::json::load(req.body);
        if (!body || !body.has("role")) {
            return crow::response(400, R"({"error": "role alanı gerekli."})");
        }

        std::string new_role = body["role"].s();
        if (new_role != "user" && new_role != "moderator" && new_role != "admin") {
            return crow::response(400, R"({"error": "Geçersiz rol. Kabul edilen değerler: user, moderator, admin."})");
        }

        if (db.update_user_role(target_user_id, new_role)) {
            Logger::info("Kullanıcı rolü değiştirildi (User ID: " + std::to_string(target_user_id) +
                         " -> " + new_role + ", İşlem yapan: " + user->username + ")");
            return crow::response(200, R"({"message": "Kullanıcı rolü başarıyla güncellendi."})");
        }

        return crow::response(500, R"({"error": "Rol güncellenemedi."})");
    });

} // register_admin_routes

} // namespace Korsancim

#endif
