#ifndef AUTH_MIDDLEWARE_HPP
#define AUTH_MIDDLEWARE_HPP

#include "crow.h"
#include "utils/jwt_helper.hpp"
#include "utils/token_blacklist.hpp"
#include <string>
#include <optional>

namespace Korsancim {

    // Doğrulanmış kullanıcı bilgileri
    struct AuthUser {
        int         user_id;
        std::string username;
        std::string role;
    };

    // JWT token'ı doğrular ve kullanıcı bilgilerini döndürür.
    // Token geçersiz, süresi dolmuş veya blacklist'teyse std::nullopt döner.
    inline std::optional<AuthUser> extract_user(const crow::request& req) {
        auto auth_header = req.get_header_value("Authorization");
        if (auth_header.empty() || auth_header.substr(0, 7) != "Bearer ") {
            return std::nullopt;
        }

        std::string token = auth_header.substr(7);

        // Blacklist kontrolü (logout edilmiş token)
        if (TokenBlacklist::instance().is_blacklisted(token)) {
            return std::nullopt;
        }

        AuthUser user;
        if (!JwtHelper::verify_token(token, user.user_id, user.username, user.role)) {
            return std::nullopt;
        }

        return user;
    }

    // Token'ı doğrular VE belirli bir rol gerektirir.
    inline std::optional<AuthUser> require_role(const crow::request& req, const std::string& required_role) {
        auto user = extract_user(req);
        if (!user) return std::nullopt;

        if (required_role == "admin" && user->role != "admin") return std::nullopt;
        if (required_role == "moderator" && user->role != "admin" && user->role != "moderator") return std::nullopt;

        return user;
    }

    // Yetkisiz erişim hatası (401)
    inline crow::response unauthorized(const std::string& msg = "Yetkisiz erişim! Token gerekli.") {
        return crow::response(401, "{\"error\": \"" + msg + "\"}");
    }

    // Yasak erişim hatası (403)
    inline crow::response forbidden(const std::string& msg = "Bu işlem için yetkiniz yok!") {
        return crow::response(403, "{\"error\": \"" + msg + "\"}");
    }

    // Güvenli stoi — hata durumunda -1 döner
    inline int safe_stoi(const char* str, int default_val = -1) {
        if (!str) return default_val;
        try {
            return std::stoi(std::string(str));
        } catch (...) {
            return default_val;
        }
    }

    // Input uzunluk doğrulama yardımcıları
    inline bool validate_username(const std::string& s) { return s.size() >= 3 && s.size() <= 32; }
    inline bool validate_password(const std::string& s) { return s.size() >= 8; }
    inline bool validate_title(const std::string& s)    { return s.size() >= 5 && s.size() <= 200; }
    inline bool validate_content(const std::string& s)  { return s.size() >= 1 && s.size() <= 10000; }

} // namespace Korsancim

#endif
