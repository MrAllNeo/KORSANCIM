#ifndef CATEGORY_HPP
#define CATEGORY_HPP

#include <string>

namespace Korsancim {

// Veritabanındaki 'categories' tablosunun C++ tarafındaki karşılığı
struct Category {
    int id;
    std::string name;
    std::string description;
    std::string slug;
};

} // namespace Korsancim

#endif // CATEGORY_HPP