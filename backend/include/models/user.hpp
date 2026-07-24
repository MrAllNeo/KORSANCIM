#ifndef USER_HPP
#define USER_HPP

#include <string>

namespace Korsancim {

// Veritabanındaki 'users' tablosunun C++ karşılığı
struct User {
    int id;
    std::string username;
    std::string password_hash;
    std::string role; // 'user' veya 'admin'
    std::string created_at;
};

} // namespace Korsancim

#endif // USER_HPP