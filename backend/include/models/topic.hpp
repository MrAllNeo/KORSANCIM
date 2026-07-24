#ifndef TOPIC_HPP
#define TOPIC_HPP

#include <string>

namespace Korsancim {

// Veritabanındaki 'topics' tablosunun C++ karşılığı
struct Topic {
    int id;
    int category_id;
    int user_id;
    std::string title;
    std::string content;
    std::string author_name; // Konuyu açan anonim kullanıcının adı
    std::string created_at;
};

} // namespace Korsancim

#endif // TOPIC_HPP