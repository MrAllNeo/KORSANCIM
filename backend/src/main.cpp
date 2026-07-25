#include "crow.h"
#include "db/database.hpp"
#include "utils/jwt_helper.hpp"
#include <iostream>
#define ASIO_STANDALONE
#include "crow.h"
#include "db/database.hpp"
#include "utils/jwt_helper.hpp"
#include <iostream>
int main() {
    crow::SimpleApp app;
    Korsancim::Database db("korsancim.db");

    if (!db.connect()) {
        std::cerr << "❌ Veritabanına bağlanılamadı!" << std::endl;
        return 1;
    }

    // Tabloları Hazırla
    db.execute("CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT UNIQUE, password_hash TEXT, role TEXT DEFAULT 'user');");
    db.execute("CREATE TABLE IF NOT EXISTS categories (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, description TEXT, slug TEXT UNIQUE);");
    db.execute("CREATE TABLE IF NOT EXISTS topics (id INTEGER PRIMARY KEY AUTOINCREMENT, category_id INTEGER, user_id INTEGER, title TEXT, content TEXT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");
    db.execute("CREATE TABLE IF NOT EXISTS comments (id INTEGER PRIMARY KEY AUTOINCREMENT, topic_id INTEGER, user_id INTEGER, content TEXT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");

    // Varsayılan Kategorileri Ekle (Boşsa)
    db.execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES (1, 'Genel Sohbet', 'Gereksiz sohbetlerin ve muhabbetin adresi', 'genel-sohbet');");
    db.execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES (2, 'Yazılım & Teknoloji', 'C++, Linux, Python ve kodlama dunyasi', 'yazilim-teknoloji');");

    // 1. KULLANICI KAYIT (REGISTER)
    CROW_ROUTE(app, "/api/auth/register").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            return crow::response(400, "{\"error\": \"Geçersiz parametreler\"}");
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s();

        if (db.register_user(username, password)) {
            return crow::response(201, "{\"message\": \"Kullanıcı başarıyla oluşturuldu!\"}");
        } else {
            return crow::response(400, "{\"error\": \"Kullanıcı adı zaten kullanımda!\"}");
        }
    });

    // 2. KULLANICI GİRİŞ (LOGIN - DB'den Gerçek ID ve Rol ile JWT Üretir)
    CROW_ROUTE(app, "/api/auth/login").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            return crow::response(400, "{\"error\": \"Eksik bilgi\"}");
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s();

        if (db.authenticate_user(username, password)) {
            Korsancim::User user_info;
            if (db.get_user_by_username(username, user_info)) {
                std::string token = Korsancim::JwtHelper::generate_token(user_info.id, user_info.username, user_info.role);

                crow::json::wvalue res;
                res["message"] = "Giriş başarılı!";
                res["token"] = token;
                res["role"] = user_info.role;
                return crow::response(200, res);
            }
        }
        
        return crow::response(401, "{\"error\": \"Kullanıcı adı veya şifre hatalı!\"}");
    });

    // 3. KATEGORİLERİ GETİR (HERKESE AÇIK)
    CROW_ROUTE(app, "/api/categories").methods(crow::HTTPMethod::GET)([&db]() {
        auto categories = db.get_categories();
        crow::json::wvalue res = crow::json::wvalue::list();
        for (size_t i = 0; i < categories.size(); ++i) {
            res[i]["id"] = categories[i].id;
            res[i]["name"] = categories[i].name;
            res[i]["description"] = categories[i].description;
            res[i]["slug"] = categories[i].slug;
        }
        return crow::response(200, res);
    });

    // 4. YENİ KONU AÇMA (JWT KORUMALI)
    CROW_ROUTE(app, "/api/topics").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto auth_header = req.get_header_value("Authorization");
        if (auth_header.empty() || auth_header.substr(0, 7) != "Bearer ") {
            return crow::response(401, "{\"error\": \"Yetkisiz erişim! Token gerekli.\"}");
        }

        std::string token = auth_header.substr(7);
        int user_id = 0;
        std::string username, role;

        if (!Korsancim::JwtHelper::verify_token(token, user_id, username, role)) {
            return crow::response(401, "{\"error\": \"Geçersiz veya süresi dolmuş token!\"}");
        }

        auto body = crow::json::load(req.body);
        if (!body || !body.has("category_id") || !body.has("title") || !body.has("content")) {
            return crow::response(400, "{\"error\": \"Eksik veri gönderildi\"}");
        }

        int category_id = body["category_id"].i();
        std::string title = body["title"].s();
        std::string content = body["content"].s();

        if (db.create_topic(category_id, user_id, title, content)) {
            crow::json::wvalue res;
            res["message"] = "Konu başarıyla açıldı!";
            res["author"] = username;
            return crow::response(201, res);
        }

        return crow::response(500, "{\"error\": \"Konu açılırken hata oluştu\"}");
    });

    // 5. KONULARI LİSTELE (HERKESE AÇIK)
    CROW_ROUTE(app, "/api/topics").methods(crow::HTTPMethod::GET)([&db](const crow::request& req) {
        int category_id = 0;
        if (req.url_params.get("category_id") != nullptr) {
            category_id = std::stoi(req.url_params.get("category_id"));
        }

        auto topics = db.get_topics(category_id);
        crow::json::wvalue res = crow::json::wvalue::list();
        for (size_t i = 0; i < topics.size(); ++i) {
            res[i]["id"] = topics[i].id;
            res[i]["category_id"] = topics[i].category_id;
            res[i]["user_id"] = topics[i].user_id;
            res[i]["author"] = topics[i].author_name;
            res[i]["title"] = topics[i].title;
            res[i]["content"] = topics[i].content;
            res[i]["created_at"] = topics[i].created_at;
        }
        return crow::response(200, res);
    });

    // 6. YENİ YORUM YAZMA (JWT KORUMALI)
    CROW_ROUTE(app, "/api/comments").methods(crow::HTTPMethod::POST)([&db](const crow::request& req) {
        auto auth_header = req.get_header_value("Authorization");
        if (auth_header.empty() || auth_header.substr(0, 7) != "Bearer ") {
            return crow::response(401, "{\"error\": \"Yetkisiz erişim! Token gerekli.\"}");
        }

        std::string token = auth_header.substr(7);
        int user_id = 0;
        std::string username, role;

        if (!Korsancim::JwtHelper::verify_token(token, user_id, username, role)) {
            return crow::response(401, "{\"error\": \"Geçersiz veya süresi dolmuş token!\"}");
        }

        auto body = crow::json::load(req.body);
        if (!body || !body.has("topic_id") || !body.has("content")) {
            return crow::response(400, "{\"error\": \"Eksik veri gönderildi\"}");
        }

        int topic_id = body["topic_id"].i();
        std::string content = body["content"].s();

        if (db.create_comment(topic_id, user_id, content)) {
            crow::json::wvalue res;
            res["message"] = "Yorum başarıyla eklendi!";
            res["author"] = username;
            return crow::response(201, res);
        }

        return crow::response(500, "{\"error\": \"Yorum eklenirken hata oluştu\"}");
    });

    // 7. YORUMLARI LİSTELE (HERKESE AÇIK)
    CROW_ROUTE(app, "/api/comments").methods(crow::HTTPMethod::GET)([&db](const crow::request& req) {
        if (req.url_params.get("topic_id") == nullptr) {
            return crow::response(400, "{\"error\": \"topic_id parametresi gerekli\"}");
        }

        int topic_id = std::stoi(req.url_params.get("topic_id"));
        auto comments = db.get_comments_by_topic(topic_id);

        crow::json::wvalue res = crow::json::wvalue::list();
        for (size_t i = 0; i < comments.size(); ++i) {
            res[i]["id"] = comments[i].id;
            res[i]["topic_id"] = comments[i].topic_id;
            res[i]["user_id"] = comments[i].user_id;
            res[i]["author"] = comments[i].author_name;
            res[i]["content"] = comments[i].content;
            res[i]["created_at"] = comments[i].created_at;
        }
        return crow::response(200, res);
    });

    // 8. ADMIN: KONU SİLME (SADECE ADMIN / MODERATOR ROLÜ YAPABİLİR)
    CROW_ROUTE(app, "/api/admin/topics/<int>").methods(crow::HTTPMethod::DELETE)([&db](const crow::request& req, int topic_id) {
        auto auth_header = req.get_header_value("Authorization");
        if (auth_header.empty() || auth_header.substr(0, 7) != "Bearer ") {
            return crow::response(401, "{\"error\": \"Yetkisiz erişim! Token gerekli.\"}");
        }

        std::string token = auth_header.substr(7);
        int user_id = 0;
        std::string username, role;

        if (!Korsancim::JwtHelper::verify_token(token, user_id, username, role)) {
            return crow::response(401, "{\"error\": \"Geçersiz token!\"}");
        }

        // Yetki Kontrolü: Yalnızca admin veya moderator silebilir!
        if (role != "admin" && role != "moderator") {
            return crow::response(403, "{\"error\": \"Bu işlem için yetkiniz yok! (Admin / Moderator gereklidir)\"}");
        }

        if (db.delete_topic(topic_id)) {
            return crow::response(200, "{\"message\": \"Konu başarıyla silindi.\"}");
        }

        return crow::response(500, "{\"error\": \"Konu silinirken bir hata oluştu.\"}");
    });

    std::cout << "🚀 KORSANCIM Backend Sunucusu 8080 Portunda Çalışıyor..." << std::endl;
    app.port(8080).multithreaded().run();
}