#ifndef COMMENT_HPP
#define COMMENT_HPP

#include <string>

namespace Korsancim {

// Veritabanındaki 'comments' tablosunun C++ karşılığı
struct Comment {
    int id;
    int topic_id;
    int user_id;
    std::string content;
    std::string author_name; // Yorumu yapan anonim korsanın adı
    std::string created_at;
};

} // namespace Korsancim

#endif // COMMENT_HPP