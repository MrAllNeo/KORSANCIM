#include "crow_all.h"
#include "db/database.hpp" // Yazdığımız veritabanı sınıfını dahil ettik
#include <iostream>

int main() {
    // 1. Veritabanı Nesnemizi Oluşturalım
    // Yol doğrudan ana klasördeki database/korsancim.db dosyasını hedeflesin:
Korsancim::Database db("database/korsancim.db");

    // 2. Veritabanına Bağlanalım
    if (!db.connect()) {
        std::cerr << "💥 Veritabanı başlatılamadığı için sunucu durduruluyor!" << std::endl;
        return 1;
    }

    crow::SimpleApp app;

    // Test API Uç Noktası (Endpoint)
    CROW_ROUTE(app, "/api/ping")([](){
        crow::json::wvalue res;
        res["status"] = "success";
        res["message"] = "KORSANCIM C++ Backend Servisi ve Veritabanı Aktif!";
        res["anonim_mod"] = true;
        return res;
    });
// Kategorileri Listeleyen API Uç Noktası
    CROW_ROUTE(app, "/api/categories")([&db](){
        crow::json::wvalue res;
        auto categories = db.get_categories();

        std::vector<crow::json::wvalue> cat_list;
        for (const auto& cat : categories) {
            crow::json::wvalue c;
            c["id"] = cat.id;
            c["name"] = cat.name;
            c["description"] = cat.description;
            c["slug"] = cat.slug;
            cat_list.push_back(c);
        }

        res["status"] = "success";
        res["categories"] = std::move(cat_list);
        return res;
    });
    // Kullanıcı Kayıt Uç Noktası (POST /api/auth/register)
    CROW_ROUTE(app, "/api/auth/register").methods(crow::HTTPMethod::Post)([&db](const crow::request& req){
        crow::json::wvalue res;
        
        // Gelen JSON verisini parse ediyoruz
        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            res["status"] = "error";
            res["message"] = "Eksik parametre! 'username' ve 'password' zorunludur.";
            return crow::response(400, res);
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s(); // İleride burayı SHA256/Argon2 ile hash'leyeceğiz

        // Kullanıcı adı kontrolü
        if (db.user_exists(username)) {
            res["status"] = "error";
            res["message"] = "Bu rumuz zaten başka bir anonim korsan tarafından alınmış!";
            return crow::response(400, res);
        }

        // Kayıt işlemi
        if (db.register_user(username, password)) {
            res["status"] = "success";
            res["message"] = "Anonim kaydınız başarıyla oluşturuldu! Hoş geldiniz.";
            return crow::response(201, res);
        } else {
            res["status"] = "error";
            res["message"] = "Sunucu hatası: Kayıt oluşturulamadı.";
            return crow::response(500, res);
        }
    });
    // Kullanıcı Giriş Uç Noktası (POST /api/auth/login)
    CROW_ROUTE(app, "/api/auth/login").methods(crow::HTTPMethod::Post)([&db](const crow::request& req){
        crow::json::wvalue res;
        
        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            res["status"] = "error";
            res["message"] = "Eksik parametre! 'username' ve 'password' zorunludur.";
            return crow::response(400, res);
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s();

        if (db.authenticate_user(username, password)) {
            res["status"] = "success";
            res["message"] = "Giriş başarılı! Hoş geldin korsan.";
            res["user"] = username;
            return crow::response(200, res);
        } else {
            res["status"] = "error";
            res["message"] = "Hatalı kullanıcı adı veya şifre!";
            return crow::response(401, res);
        }
    });
    std::cout << "=========================================" << std::endl;
    std::cout << " 🚀 KORSANCIM Sunucusu Başlatılıyor...  " << std::endl;
    std::cout << " Port: 8080                              " << std::endl;
    std::cout << "=========================================" << std::endl;

    app.port(8080).multithreaded().run();

    return 0;
}