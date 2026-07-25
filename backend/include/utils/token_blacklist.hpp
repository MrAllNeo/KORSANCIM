#ifndef TOKEN_BLACKLIST_HPP
#define TOKEN_BLACKLIST_HPP

#include <string>
#include <unordered_set>
#include <mutex>

namespace Korsancim {
    // Thread-safe token blacklist (logout için).
    // Not: Bu in-memory implementasyondur — server restart'ta temizlenir.
    // Prodüksiyonda Redis veya kalıcı bir store kullanılmalıdır.
    class TokenBlacklist {
    private:
        std::unordered_set<std::string> blacklisted_tokens;
        mutable std::mutex mtx;

        // Singleton
        TokenBlacklist() = default;

    public:
        static TokenBlacklist& instance() {
            static TokenBlacklist inst;
            return inst;
        }

        // Token'ı blacklist'e ekle (logout)
        void add(const std::string& token) {
            std::lock_guard<std::mutex> lock(mtx);
            blacklisted_tokens.insert(token);
        }

        // Token blacklist'te mi kontrol et
        bool is_blacklisted(const std::string& token) const {
            std::lock_guard<std::mutex> lock(mtx);
            return blacklisted_tokens.count(token) > 0;
        }

        // Kopyalamayı engelle (singleton)
        TokenBlacklist(const TokenBlacklist&) = delete;
        TokenBlacklist& operator=(const TokenBlacklist&) = delete;
    };
}

#endif
