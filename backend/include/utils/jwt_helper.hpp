#ifndef JWT_HELPER_HPP
#define JWT_HELPER_HPP

#include <jwt-cpp/jwt.h>
#include <string>
#include <chrono>
#include <stdexcept>
#include <cstdlib>

namespace Korsancim {
    class JwtHelper {
    private:
        static const std::string& get_secret() {
            // Güvenlik: Secret key çevre değişkeninden okunur, kaynak kodda ASLA hardcoded olmaz!
            static std::string secret;
            if (secret.empty()) {
                const char* env_secret = std::getenv("KORSANCIM_JWT_SECRET");
                if (!env_secret || std::string(env_secret).size() < 16) {
                    throw std::runtime_error(
                        "[JWT] KORSANCIM_JWT_SECRET ortam değişkeni tanımlı değil veya çok kısa! "
                        "Minimum 16 karakter gereklidir. Örnek: export KORSANCIM_JWT_SECRET='cok_gizli_anahtar_minimum_32_char'"
                    );
                }
                secret = std::string(env_secret);
            }
            return secret;
        }

        static const std::string& get_issuer() {
            static const std::string issuer = "korsancim_api";
            return issuer;
        }

    public:
        // Token süresi (saat cinsinden)
        static constexpr int TOKEN_EXPIRY_HOURS = 24;

        // Kullanıcıya özel JWT Token üretme
        static std::string generate_token(int user_id, const std::string& username, const std::string& role = "user") {
            auto token = jwt::create()
                .set_issuer(get_issuer())
                .set_type("JWS")
                .set_payload_claim("user_id", jwt::claim(std::to_string(user_id)))
                .set_payload_claim("username", jwt::claim(username))
                .set_payload_claim("role", jwt::claim(role))
                .set_issued_at(std::chrono::system_clock::now())
                .set_expires_at(std::chrono::system_clock::now() + std::chrono::hours(TOKEN_EXPIRY_HOURS))
                .sign(jwt::algorithm::hs256{get_secret()});

            return token;
        }

        // Gelen Token'ı Doğrulama ve İçi Açma
        static bool verify_token(const std::string& token_str, int& out_user_id, std::string& out_username, std::string& out_role) {
            try {
                auto decoded = jwt::decode(token_str);
                auto verifier = jwt::verify()
                    .allow_algorithm(jwt::algorithm::hs256{get_secret()})
                    .with_issuer(get_issuer());

                verifier.verify(decoded);

                out_user_id = std::stoi(decoded.get_payload_claim("user_id").as_string());
                out_username = decoded.get_payload_claim("username").as_string();
                out_role = decoded.get_payload_claim("role").as_string();

                return true;
            } catch (const std::exception&) {
                return false;
            }
        }
    };
}

#endif