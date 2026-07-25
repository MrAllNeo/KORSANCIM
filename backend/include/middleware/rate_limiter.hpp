#ifndef RATE_LIMITER_HPP
#define RATE_LIMITER_HPP

#include <string>
#include <unordered_map>
#include <chrono>
#include <mutex>

namespace Korsancim {

    struct ClientRequestInfo {
        int request_count;
        std::chrono::steady_clock::time_point last_request_time;
    };

    class RateLimiter {
    private:
        int max_requests;
        std::chrono::seconds window_duration;
        std::unordered_map<std::string, ClientRequestInfo> clients;
        std::mutex limiter_mutex;

    public:
        RateLimiter(int max_req = 10, int window_sec = 1) 
            : max_requests(max_req), window_duration(window_sec) {}

        bool is_allowed(const std::string& client_ip) {
            std::lock_guard<std::mutex> lock(limiter_mutex);
            auto now = std::chrono::steady_clock::now();

            auto it = clients.find(client_ip);
            if (it == clients.end()) {
                clients[client_ip] = {1, now};
                return true;
            }

            auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - it->second.last_request_time);

            if (elapsed >= window_duration) {
                // Zaman penceresi sıfırlandı
                it->second.request_count = 1;
                it->second.last_request_time = now;
                return true;
            } else {
                if (it->second.request_count < max_requests) {
                    it->second.request_count++;
                    return true;
                } else {
                    return false; // Sınır aşıldı!
                }
            }
        }
    };

}

#endif