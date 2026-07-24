#include <gtest/gtest.h>
#include "db/database.hpp"
#include <fstream>
#include "utils/jwt_helper.hpp"
// Test için geçici bir test veritabanı oluşturan fixture
class DatabaseTest : public ::testing::Test {
protected:
    std::string test_db_path = "test_korsancim.db";
    Korsancim::Database* db;

    void SetUp() override {
        // Her test öncesi sıfır veritabanı kur
        db = new Korsancim::Database(test_db_path);
        ASSERT_TRUE(db->connect());

        // Tabloları oluştur
        db->execute("CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT UNIQUE, password_hash TEXT);");
        db->execute("CREATE TABLE IF NOT EXISTS categories (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, description TEXT, slug TEXT UNIQUE);");
        db->execute("CREATE TABLE IF NOT EXISTS topics (id INTEGER PRIMARY KEY AUTOINCREMENT, category_id INTEGER, user_id INTEGER, title TEXT, content TEXT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");
    }

    void TearDown() override {
        db->disconnect();
        delete db;
        // Test bittiğinde geçici test DB dosyasını sil
        std::remove(test_db_path.c_str());
    }
};

// TEST 1: Veritabanı Bağlantı Testi
TEST_F(DatabaseTest, ConnectionTest) {
    EXPECT_NE(db->get_db_handle(), nullptr);
}

// TEST 2: Kullanıcı Kaydı Testi
TEST_F(DatabaseTest, UserRegistrationTest) {
    bool registered = db->register_user("test_korsan", "hash_sifre_123");
    EXPECT_TRUE(registered);

    // Aynı kullanıcı tekrar kaydolamasın
    bool duplicate = db->register_user("test_korsan", "hash_sifre_123");
    EXPECT_FALSE(duplicate);
}

// TEST 3: Konu Oluşturma Testi
TEST_F(DatabaseTest, CreateTopicTest) {
    db->register_user("yazar_korsan", "pass123");
    bool topic_created = db->create_topic(1, 1, "Test Başlığı", "Test İçeriği C++");
    EXPECT_TRUE(topic_created);
}
// TEST 4: JWT Token Üretme ve Doğrulama Testi
TEST(JwtTest, GenerateAndVerifyToken) {
    int user_id = 42;
    std::string username = "korsan_kaptan";
    std::string role = "admin";

    // 1. Token Üret
    std::string token = Korsancim::JwtHelper::generate_token(user_id, username, role);
    EXPECT_FALSE(token.empty());

    // 2. Token Doğrula
    int verified_id = 0;
    std::string verified_username, verified_role;
    bool is_valid = Korsancim::JwtHelper::verify_token(token, verified_id, verified_username, verified_role);

    EXPECT_TRUE(is_valid);
    EXPECT_EQ(verified_id, 42);
    EXPECT_EQ(verified_username, "korsan_kaptan");
    EXPECT_EQ(verified_role, "admin");

    // 3. Sahte/Bozuk Token Testi
    std::string fake_token = token + "bozuk_kısım";
    bool is_fake_valid = Korsancim::JwtHelper::verify_token(fake_token, verified_id, verified_username, verified_role);
    EXPECT_FALSE(is_fake_valid);
}