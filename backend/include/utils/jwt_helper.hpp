#ifndef JWT_HELPER_HPP
#define JWT_HELPER_HPP

#include <jwt-cpp/jwt.h>
#include <string>
#include <chrono>

namespace Korsancim {
    class JwtHelper {
    private:
        // Gizli İmza Anahtarımız (Production'da çevre değişkeninden çekilir)
        inline static const std::string SECRET_KEY = "korsancim_gizli_kapitan_anahtari_2026!";
        inline static const std::string ISSUER = "korsancim_api";

    public:
        // 1. Kullanıcıya özel JWT Token üretme (24 Saat Geçerli)
        static std::string generate_token(int user_id, const std::string& username, const std::string& role = "user") {
            auto token = jwt::create()
                .set_issuer(ISSUER)
                .set_type("JWS")
                .set_payload_claim("user_id", jwt::claim(std::to_string(user_id)))
                .set_payload_claim("username", jwt::claim(username))
                .set_payload_claim("role", jwt::claim(role))
                .set_issued_at(std::chrono::system_clock::now())
                .set_expires_at(std::chrono::system_clock::now() + std::chrono::hours(24))
                .sign(jwt::algorithm::hs256{SECRET_KEY});

            return token;
        }

        // 2. Gelen Token'ı Doğrulama ve İçi Açma
        static bool verify_token(const std::string& token_str, int& out_user_id, std::string& out_username, std::string& out_role) {
            try {
                auto decoded = jwt::decode(token_str);
                auto verifier = jwt::verify()
                    .allow_algorithm(jwt::algorithm::hs256{SECRET_KEY})
                    .with_issuer(ISSUER);

                verifier.verify(decoded);

                // Token geçerli, içindeki bilgileri çıkaralım
                out_user_id = std::stoi(decoded.get_payload_claim("user_id").as_string());
                out_username = decoded.get_payload_claim("username").as_string();
                out_role = decoded.get_payload_claim("role").as_string();

                return true;
            } catch (const std::exception& e) {
                // Token geçersiz veya süresi dolmuş!
                return false;
            }
        }
    };
}

#endif