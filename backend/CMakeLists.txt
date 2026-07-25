cmake_minimum_required(VERSION 3.14)
project(Korsancim VERSION 1.1.0)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Derleme zamanı versiyon bilgisi
add_definitions(-DKORSANCIM_VERSION="1.1.0")

# ── Bağımlılıklar ────────────────────────────────────────────
find_package(SQLite3 REQUIRED)
find_package(Threads REQUIRED)
find_package(GTest REQUIRED)
find_package(OpenSSL REQUIRED)

# ── Header Dizinleri ─────────────────────────────────────────
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src # src/config/app_config.hpp için
    ${SQLITE3_INCLUDE_DIRS}
    ${GTEST_INCLUDE_DIRS}
    ${OPENSSL_INCLUDE_DIR}
)

# Standalone Asio ve Crow Makroları
add_definitions(-DCROW_USE_BOOST=0)
add_definitions(-DASIO_STANDALONE)

# ── Backend Kaynak Dosyaları ─────────────────────────────────
set(BACKEND_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/db/database.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/utils/logger.cpp
)

# ── 1. Sunucu Executable ─────────────────────────────────────
add_executable(korsancim_server
    ${CMAKE_CURRENT_SOURCE_DIR}/src/main.cpp
    ${BACKEND_SOURCES}
)

target_link_libraries(korsancim_server
    PRIVATE
    SQLite::SQLite3
    Threads::Threads
    OpenSSL::SSL
    OpenSSL::Crypto
)

# ── 2. Unit Test Executable ───────────────────────────────────
enable_testing()

add_executable(korsancim_tests
    ${CMAKE_CURRENT_SOURCE_DIR}/tests/unit_tests.cpp
    ${BACKEND_SOURCES}
)

target_link_libraries(korsancim_tests
    PRIVATE
    SQLite::SQLite3
    Threads::Threads
    GTest::GTest
    GTest::Main
    OpenSSL::SSL
    OpenSSL::Crypto
)

add_test(NAME KorsancimUnitTests COMMAND korsancim_tests)
