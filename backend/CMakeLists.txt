cmake_minimum_required(VERSION 3.10)

project(KorsancimBackend VERSION 1.0)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Threading desteği
find_package(Threads REQUIRED)

# Include klasörümüz
include_directories(include)

# Ana derleme hedefi (database.cpp dosyasını buraya ekledik)
add_executable(korsancim_server 
    src/main.cpp
    src/db/database.cpp
)

# Gerekli kütüphaneleri bağlayalım
target_link_libraries(korsancim_server PRIVATE Threads::Threads sqlite3)