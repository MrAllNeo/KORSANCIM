#ifndef TOPIC_ROUTES_HPP
#define TOPIC_ROUTES_HPP

#include "crow.h"
#include "db/database.hpp"
#include "middleware/auth_middleware.hpp"
#include "utils/logger.hpp"

namespace Korsancim {

template<typename App>
inline void register_topic_routes(App& app, Database& db) {

    // ── 1. KONULARI LİSTELE (HERKESE AÇIK) — Pagination destekli ──
    app.route_dynamic("/api/topics").methods(crow::HTTPMethod::GET)([&db](const crow::request& req) {
        int category_id = safe_stoi(req.url_params.get("category_id"), 0);
        int page        = safe_stoi(req.url_params.get("page"), 1);
        int limit       = safe_stoi(req.url_params.get("limit"), 20);

        if (page < 1)  page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        auto topics      = db.get_topics(category_id, page, limit);
        int  total_count = db.get_topic_count(category_id);
        int  total_pages = (total_count + limit - 1) / limit;

        crow::json::wvalue res;
        res["page"]        = page;
        res["limit"]       = limit;
        res["total_count"] = total_count;
        res["total_pages"] = total_pages;

        crow::json::wvalue::list topic_list;
        for (auto& t : topics) {
            crow::json::wvalue item;
            item["id"]          = t.id;
            item["category_id"] = t.category_id;
            item["user_id"]     = t.user_id;
            item["author"]      = t.author_name;
            item["title"]       = t.title;
            item["content"]     = t.content;
            item["created_at"]  = t.created_at;
            topic_list.push_back(std::move(item));
        }
        res["topics"] = std::move(topic_list);

        return crow::response(200, res);
    });

    // ── 2. YENİ KONU AÇMA (JWT KORUMALI) ─────────────────────
    app.route_dynamic("/api/topics").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto user = extract_user(req);
        if (!user) return unauthorized();

        auto body = crow::json::load(req.body);
        if (!body || !body.has("category_id") || !body.has("title") || !body.has("content")) {
            return crow::response(400, R"({"error": "Eksik veri: category_id, title ve content gerekli."})");
        }

        int category_id = body["category_id"].i();
        std::string title   = body["title"].s();
        std::string content = body["content"].s();

        if (!validate_title(title)) {
            return crow::response(400, R"({"error": "Başlık 5-200 karakter arasında olmalıdır."})");
        }
        if (!validate_content(content)) {
            return crow::response(400, R"({"error": "İçerik 1-10000 karakter arasında olmalıdır."})");
        }

        if (db.create_topic(category_id, user->user_id, title, content)) {
            Logger::info("Yeni konu açıldı: '" + title + "' (Yazar: " + user->username + ")");
            crow::json::wvalue res;
            res["message"] = "Konu başarıyla açıldı!";
            res["author"]  = user->username;
            return crow::response(201, res);
        }

        return crow::response(500, R"({"error": "Konu açılırken hata oluştu."})");
    });

    // ── 3. KONU GÜNCELLEME (Sadece Sahip) ────────────────────
    app.route_dynamic("/api/topics/<int>").methods(crow::HTTPMethod::PUT)([&db](const crow::request& req, int topic_id) {
        auto user = extract_user(req);
        if (!user) return unauthorized();

        auto body = crow::json::load(req.body);
        if (!body || !body.has("title") || !body.has("content")) {
            return crow::response(400, R"({"error": "title ve content gerekli."})");
        }

        std::string title   = body["title"].s();
        std::string content = body["content"].s();

        if (!validate_title(title)) {
            return crow::response(400, R"({"error": "Başlık 5-200 karakter arasında olmalıdır."})");
        }
        if (!validate_content(content)) {
            return crow::response(400, R"({"error": "İçerik 1-10000 karakter arasında olmalıdır."})");
        }

        if (db.update_topic(topic_id, user->user_id, title, content)) {
            Logger::info("Konu güncellendi (ID: " + std::to_string(topic_id) + ", Kullanıcı: " + user->username + ")");
            return crow::response(200, R"({"message": "Konu başarıyla güncellendi."})");
        }

        return crow::response(403, R"({"error": "Konu bulunamadı veya bu konuyu düzenleme yetkiniz yok."})");
    });

    // ── 4. KATEGORİLERİ GETİR (HERKESE AÇIK) ────────────────
    app.route_dynamic("/api/categories").methods(crow::HTTPMethod::GET)([&db]() {
        auto categories = db.get_categories();

        crow::json::wvalue::list cat_list;
        for (auto& c : categories) {
            crow::json::wvalue item;
            item["id"]          = c.id;
            item["name"]        = c.name;
            item["description"] = c.description;
            item["slug"]        = c.slug;
            cat_list.push_back(std::move(item));
        }

        crow::json::wvalue res;
        res["categories"] = std::move(cat_list);
        return crow::response(200, res);
    });

} // register_topic_routes

} // namespace Korsancim

#endif
