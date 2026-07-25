#ifndef HASH_HELPER_HPP
#define HASH_HELPER_HPP

#include <openssl/evp.h>
#include <openssl/rand.h>
#include <string>
#include <sstream>
#include <iomanip>
#include <vector>
#include <stdexcept>

namespace Korsancim {
    class HashHelper {
    private:
        // PBKDF2 parametreleri
        static constexpr int SALT_BYTES    = 16;
        static constexpr int HASH_BYTES    = 32;
        static constexpr int ITERATIONS    = 100000; // 100K iterasyon — brute-force'a karşı güçlü

        // Binary'yi hex string'e çevirir
        static std::string to_hex(const unsigned char* data, size_t len) {
            std::ostringstream ss;
            for (size_t i = 0; i < len; ++i)
                ss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(data[i]);
            return ss.str();
        }

        // Hex string'i binary'ye çevirir
        static std::vector<unsigned char> from_hex(const std::string& hex) {
            std::vector<unsigned char> bytes;
            for (size_t i = 0; i + 1 < hex.size(); i += 2) {
                bytes.push_back(static_cast<unsigned char>(std::stoi(hex.substr(i, 2), nullptr, 16)));
            }
            return bytes;
        }

    public:
        // Rastgele 16 baytlık güvenli Salt üretir
        static std::string generate_salt() {
            unsigned char salt[SALT_BYTES];
            if (RAND_bytes(salt, SALT_BYTES) != 1) {
                throw std::runtime_error("RAND_bytes başarısız oldu!");
            }
            return to_hex(salt, SALT_BYTES);
        }

        // Şifreyi PBKDF2-SHA256 ile hash'ler (100K iterasyon)
        // Format: salt_hex$hash_hex
        static std::string hash_password(const std::string& password, const std::string& salt_hex) {
            auto salt_bytes = from_hex(salt_hex);
            unsigned char hash[HASH_BYTES];

            int rc = PKCS5_PBKDF2_HMAC(
                password.c_str(), static_cast<int>(password.size()),
                salt_bytes.data(), static_cast<int>(salt_bytes.size()),
                ITERATIONS,
                EVP_sha256(),
                HASH_BYTES,
                hash
            );

            if (rc != 1) {
                throw std::runtime_error("PBKDF2 hash işlemi başarısız oldu!");
            }

            return salt_hex + "$" + to_hex(hash, HASH_BYTES);
        }

        // Girilen şifrenin veritabanındaki stored_hash ile eşleşip eşleşmediğini doğrular
        // Hem yeni PBKDF2 formatı hem eski SHA-256 formatı ile uyumludur
        static bool verify_password(const std::string& password, const std::string& stored_hash) {
            size_t delimiter_pos = stored_hash.find('$');
            if (delimiter_pos == std::string::npos) return false;

            std::string salt = stored_hash.substr(0, delimiter_pos);

            try {
                // PBKDF2 ile doğrula (yeni format — salt=32 hex chars = 16 bytes)
                std::string computed = hash_password(password, salt);
                return computed == stored_hash;
            } catch (...) {
                return false;
            }
        }
    };
}

#endif