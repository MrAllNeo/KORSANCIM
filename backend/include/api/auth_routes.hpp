#ifndef AUTH_ROUTES_HPP
#define AUTH_ROUTES_HPP

#include "crow.h"
#include "db/database.hpp"
#include "middleware/auth_middleware.hpp"
#include "middleware/rate_limiter.hpp"
#include "utils/jwt_helper.hpp"
#include "utils/token_blacklist.hpp"
#include "utils/logger.hpp"

namespace Korsancim {

template<typename App>
inline void register_auth_routes(App& app, Database& db, RateLimiter& rate_limiter) {

    // ── 1. KULLANICI KAYIT ────────────────────────────────────
    app.template route_dynamic("/api/auth/register").methods(crow::HTTPMethod::POST)([&db, &rate_limiter](const crow::request& req) {
        if (!rate_limiter.is_allowed(req.remote_ip_address)) {
            Logger::warn("Rate limit aşıldı (Register)! IP: " + req.remote_ip_address);
            return crow::response(429, R"({"error": "Çok fazla istek. Lütfen bekleyin."})");
        }

        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            return crow::response(400, R"({"error": "Eksik parametreler: username ve password gerekli."})");
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s();

        // ── Input Validasyon
        if (!validate_username(username)) {
            return crow::response(400, R"({"error": "Kullanıcı adı 3-32 karakter arasında olmalıdır."})");
        }
        if (!validate_password(password)) {
            return crow::response(400, R"({"error": "Şifre en az 8 karakter olmalıdır."})");
        }

        if (db.register_user(username, password)) {
            Logger::info("Yeni kullanıcı kaydoldu: " + username);
            return crow::response(201, R"({"message": "Kullanıcı başarıyla oluşturuldu!"})");
        }

        return crow::response(409, R"({"error": "Kullanıcı adı zaten kullanımda!"})");
    });

    // ── 2. KULLANICI GİRİŞ ────────────────────────────────────
    app.template route_dynamic("/api/auth/login").methods(crow::HTTPMethod::POST)([&db, &rate_limiter](const crow::request& req) {
        if (!rate_limiter.is_allowed(req.remote_ip_address)) {
            Logger::warn("Rate limit aşıldı (Login)! IP: " + req.remote_ip_address);
            return crow::response(429, R"({"error": "Çok fazla hatalı giriş denemesi. Lütfen bekleyin."})");
        }

        auto body = crow::json::load(req.body);
        if (!body || !body.has("username") || !body.has("password")) {
            return crow::response(400, R"({"error": "Eksik bilgi: username ve password gerekli."})");
        }

        std::string username = body["username"].s();
        std::string password = body["password"].s();

        if (!validate_username(username) || !validate_password(password)) {
            return crow::response(401, R"({"error": "Kullanıcı adı veya şifre hatalı!"})");
        }

        if (db.authenticate_user(username, password)) {
            User user_info;
            if (db.get_user_by_username(username, user_info)) {
                if (user_info.is_banned) {
                    Logger::warn("Banlı kullanıcı giriş denemesi: " + username);
                    crow::json::wvalue err_res;
                    err_res["error"]  = "Hesabınız askıya alınmıştır!";
                    err_res["reason"] = user_info.ban_reason;
                    return crow::response(403, err_res);
                }

                std::string token = JwtHelper::generate_token(user_info.id, user_info.username, user_info.role);
                Logger::info("Başarılı giriş: " + username + " (" + user_info.role + ")");

                crow::json::wvalue res;
                res["message"] = "Giriş başarılı!";
                res["token"]   = token;
                res["role"]    = user_info.role;
                return crow::response(200, res);
            }
        }

        Logger::warn("Hatalı giriş denemesi: " + username);
        return crow::response(401, R"({"error": "Kullanıcı adı veya şifre hatalı!"})");
    });

    // ── 3. LOGOUT (Token Blacklist) ───────────────────────────
    app.template route_dynamic("/api/auth/logout").methods(crow::HTTPMethod::POST)([](const crow::request& req) {
        auto auth_header = req.get_header_value("Authorization");
        if (auth_header.size() > 7 && auth_header.substr(0, 7) == "Bearer ") {
            std::string token = auth_header.substr(7);
            TokenBlacklist::instance().add(token);
            Logger::info("Kullanıcı çıkış yaptı (token blacklist'e eklendi).");
        }
        return crow::response(200, R"({"message": "Başarıyla çıkış yapıldı."})");
    });

} // register_auth_routes

} // namespace Korsancim

#endif
