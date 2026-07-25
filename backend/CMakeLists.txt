cmake_minimum_required(VERSION 3.14)
project(Korsancim)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Bağımlılıklar
find_package(SQLite3 REQUIRED)
find_package(Threads REQUIRED)
find_package(GTest REQUIRED)
find_package(OpenSSL REQUIRED)

# Header Dizinleri
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${SQLITE3_INCLUDE_DIRS}
    ${GTEST_INCLUDE_DIRS}
    ${OPENSSL_INCLUDE_DIR}
)

# Crow & Standalone Asio Tanımlamaları
add_definitions(-DCROW_USE_BOOST=0)
add_definitions(-DASIO_STANDALONE)

# Backend Kaynak Dosyaları
set(BACKEND_SOURCES
    src/db/database.cpp
)

# 1. SUNUCU EXECUTABLE
add_executable(korsancim_server
    src/main.cpp
    ${BACKEND_SOURCES}
)

target_link_libraries(korsancim_server
    PRIVATE
    ${SQLITE3_LIBRARIES}
    SQLite::SQLite3
    Threads::Threads
    OpenSSL::SSL
    OpenSSL::Crypto
)

# 2. UNIT TEST EXECUTABLE
enable_testing()

add_executable(korsancim_tests
    ./tests/unit_tests.cpp
    ${BACKEND_SOURCES}
)

target_link_libraries(korsancim_tests
    PRIVATE
    ${SQLITE3_LIBRARIES}
    SQLite::SQLite3
    Threads::Threads
    GTest::GTest
    GTest::Main
    OpenSSL::SSL
    OpenSSL::Crypto
)

add_test(NAME KorsancimUnitTests COMMAND korsancim_tests)