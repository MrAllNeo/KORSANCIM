#ifndef APP_CONFIG_HPP
#define APP_CONFIG_HPP

#include <string>
#include <cstdlib>
#include <stdexcept>

namespace Korsancim {
    struct AppConfig {
        std::string db_path;
        int         port;
        int         rate_limit_max;
        int         rate_limit_window_sec;

        // Tüm ayarları çevre değişkenlerinden yükler.
        // JWT secret doğrudan JwtHelper tarafından yönetilir.
        static AppConfig from_env() {
            AppConfig cfg;

            // Veritabanı yolu (varsayılan: korsancim.db)
            const char* db = std::getenv("KORSANCIM_DB_PATH");
            cfg.db_path = db ? db : "korsancim.db";

            // Port (varsayılan: 8080)
            const char* port_str = std::getenv("KORSANCIM_PORT");
            cfg.port = port_str ? std::atoi(port_str) : 8080;
            if (cfg.port <= 0 || cfg.port > 65535) {
                throw std::runtime_error("KORSANCIM_PORT geçersiz port numarası!");
            }

            // Rate limiter — max istek sayısı (varsayılan: 10)
            const char* rl_max = std::getenv("KORSANCIM_RATE_LIMIT_MAX");
            cfg.rate_limit_max = rl_max ? std::atoi(rl_max) : 10;

            // Rate limiter — zaman penceresi saniye (varsayılan: 1)
            const char* rl_win = std::getenv("KORSANCIM_RATE_LIMIT_WINDOW");
            cfg.rate_limit_window_sec = rl_win ? std::atoi(rl_win) : 1;

            return cfg;
        }
    };
}

#endif
