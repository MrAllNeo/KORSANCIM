#ifndef LOGGER_HPP
#define LOGGER_HPP

#include <iostream>
#include <string>
#include <chrono>
#include <iomanip>
#include <sstream>
#include <mutex>

namespace Korsancim {

    enum class LogLevel {
        INFO,
        WARNING,
        ERROR,
        DEBUG
    };

    class Logger {
    private:
        static std::mutex log_mutex;

        static std::string get_current_time() {
            auto now = std::chrono::system_clock::now();
            auto in_time_t = std::chrono::system_clock::to_time_t(now);
            std::stringstream ss;
            ss << std::put_time(std::localtime(&in_time_t), "%Y-%m-%d %H:%M:%S");
            return ss.str();
        }

    public:
        static void log(LogLevel level, const std::string& message) {
            std::lock_guard<std::mutex> lock(log_mutex);
            std::string level_str;
            std::string color_code;

            switch (level) {
                case LogLevel::INFO:
                    level_str = "[INFO]";
                    color_code = "\033[32m"; // Yeşil
                    break;
                case LogLevel::WARNING:
                    level_str = "[WARN]";
                    color_code = "\033[33m"; // Sarı
                    break;
                case LogLevel::ERROR:
                    level_str = "[ERROR]";
                    color_code = "\033[31m"; // Kırmızı
                    break;
                case LogLevel::DEBUG:
                    level_str = "[DEBUG]";
                    color_code = "\033[36m"; // Turkuaz
                    break;
            }

            std::cout << color_code << "[" << get_current_time() << "] " 
                      << level_str << " " << message << "\033[0m" << std::endl;
        }

        static void info(const std::string& msg) { log(LogLevel::INFO, msg); }
        static void warn(const std::string& msg) { log(LogLevel::WARNING, msg); }
        static void error(const std::string& msg) { log(LogLevel::ERROR, msg); }
        static void debug(const std::string& msg) { log(LogLevel::DEBUG, msg); }
    };

}

#endif