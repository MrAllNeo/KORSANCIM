#ifndef COMMENT_ROUTES_HPP
#define COMMENT_ROUTES_HPP

#include "crow.h"
#include "db/database.hpp"
#include "middleware/auth_middleware.hpp"
#include "utils/logger.hpp"

namespace Korsancim {

template<typename App>
inline void register_comment_routes(App& app, Database& db) {

    // ── 1. YORUMLARI LİSTELE (HERKESE AÇIK) ─────────────────
    app.template route_dynamic("/api/comments").methods(crow::HTTPMethod::GET)([&db](const crow::request& req) {
        int topic_id = safe_stoi(req.url_params.get("topic_id"), -1);
        if (topic_id <= 0) {
            return crow::response(400, R"({"error": "Geçerli bir topic_id parametresi gerekli."})");
        }

        auto comments = db.get_comments_by_topic(topic_id);

        crow::json::wvalue::list comment_list;
        for (auto& c : comments) {
            crow::json::wvalue item;
            item["id"]         = c.id;
            item["topic_id"]   = c.topic_id;
            item["user_id"]    = c.user_id;
            item["author"]     = c.author_name;
            item["content"]    = c.content;
            item["created_at"] = c.created_at;
            comment_list.push_back(std::move(item));
        }

        crow::json::wvalue res;
        res["topic_id"] = topic_id;
        res["comments"] = std::move(comment_list);
        return crow::response(200, res);
    });

    // ── 2. YENİ YORUM YAZMA (JWT KORUMALI) ───────────────────
    app.template route_dynamic("/api/comments").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto user = extract_user(req);
        if (!user) return unauthorized();

        auto body = crow::json::load(req.body);
        if (!body || !body.has("topic_id") || !body.has("content")) {
            return crow::response(400, R"({"error": "topic_id ve content gerekli."})");
        }

        int topic_id        = body["topic_id"].i();
        std::string content = body["content"].s();

        if (!validate_content(content)) {
            return crow::response(400, R"({"error": "Yorum içeriği 1-10000 karakter arasında olmalıdır."})");
        }

        if (db.create_comment(topic_id, user->user_id, content)) {
            Logger::info("Yorum eklendi (Topic ID: " + std::to_string(topic_id) + ", Yazar: " + user->username + ")");
            crow::json::wvalue res;
            res["message"] = "Yorum başarıyla eklendi!";
            res["author"]  = user->username;
            return crow::response(201, res);
        }

        return crow::response(500, R"({"error": "Yorum eklenirken hata oluştu."})");
    });

    // ── 3. YORUM GÜNCELLEME (Sadece Sahip) ───────────────────
    app.template route_dynamic("/api/comments/<int>").methods(crow::HTTPMethod::PUT)([&db](const crow::request& req, int comment_id) {
        auto user = extract_user(req);
        if (!user) return unauthorized();

        auto body = crow::json::load(req.body);
        if (!body || !body.has("content")) {
            return crow::response(400, R"({"error": "content alanı gerekli."})");
        }

        std::string content = body["content"].s();
        if (!validate_content(content)) {
            return crow::response(400, R"({"error": "İçerik 1-10000 karakter arasında olmalıdır."})");
        }

        if (db.update_comment(comment_id, user->user_id, content)) {
            Logger::info("Yorum güncellendi (ID: " + std::to_string(comment_id) + ", Kullanıcı: " + user->username + ")");
            return crow::response(200, R"({"message": "Yorum başarıyla güncellendi."})");
        }

        return crow::response(403, R"({"error": "Yorum bulunamadı veya bu yorumu düzenleme yetkiniz yok."})");
    });

    // ── 4. YORUM SİLME (Sahip veya Admin/Moderator) ──────────
    app.template route_dynamic("/api/comments/<int>").methods(crow::HTTPMethod::DELETE)([&db](const crow::request& req, int comment_id) {
        auto user = extract_user(req);
        if (!user) return unauthorized();

        if (db.delete_comment(comment_id, user->user_id, user->role)) {
            Logger::info("Yorum silindi (ID: " + std::to_string(comment_id) + ", Silen: " + user->username + ")");
            return crow::response(200, R"({"message": "Yorum başarıyla silindi."})");
        }

        return crow::response(403, R"({"error": "Yorum bulunamadı veya silme yetkiniz yok."})");
    });

} // register_comment_routes

} // namespace Korsancim

#endif
