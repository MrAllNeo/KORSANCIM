#ifndef HASH_HELPER_HPP
#define HASH_HELPER_HPP

#include <openssl/evp.h>
#include <openssl/rand.h>
#include <string>
#include <sstream>
#include <iomanip>

namespace Korsancim {
    class HashHelper {
    public:
        // Rastgele 16 baytlık Tuz (Salt) üretir
        static std::string generate_salt() {
            unsigned char salt[16];
            RAND_bytes(salt, sizeof(salt));
            
            std::stringstream ss;
            for(int i = 0; i < 16; i++) {
                ss << std::hex << std::setw(2) << std::setfill('0') << (int)salt[i];
            }
            return ss.str();
        }

        // Şifre + Salt birleşimini SHA-256 ile Hash'ler
        static std::string hash_password(const std::string& password, const std::string& salt) {
            std::string salted_password = password + salt;
            
            EVP_MD_CTX* context = EVP_MD_CTX_new();
            const EVP_MD* md = EVP_sha256();
            unsigned char hash[EVP_MAX_MD_SIZE];
            unsigned int lengthOfHash = 0;

            EVP_DigestInit_ex(context, md, NULL);
            EVP_DigestUpdate(context, salted_password.c_str(), salted_password.size());
            EVP_DigestFinal_ex(context, hash, &lengthOfHash);
            EVP_MD_CTX_free(context);

            std::stringstream ss;
            for(unsigned int i = 0; i < lengthOfHash; i++) {
                ss << std::hex << std::setw(2) << std::setfill('0') << (int)hash[i];
            }
            
            // Format: salt$hash_degeri
            return salt + "$" + ss.str();
        }

        // Girilen şifrenin veritabanındaki salted_hash ile eşleşip eşleşmediğini doğrular
        static bool verify_password(const std::string& password, const std::string& stored_hash) {
            size_t delimiter_pos = stored_hash.find('$');
            if (delimiter_pos == std::string::npos) {
                return false;
            }

            std::string salt = stored_hash.substr(0, delimiter_pos);
            std::string computed_hash = hash_password(password, salt);

            return computed_hash == stored_hash;
        }
    };
}

#endif