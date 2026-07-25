#include <gtest/gtest.h>
#include "db/database.hpp"
#include <fstream>
#include "utils/jwt_helper.hpp"
#include "utils/hash_helper.hpp"
#include "utils/logger.hpp"
#include "./middleware/rate_limiter.hpp"
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
// TEST 5: Password Hashing ve Doğrulama Testi
TEST(HashTest, PasswordHashingAndVerification) {
    std::string raw_password = "Korsan_Sifre_2026!";
    
    // 1. Şifreyi Hash'le (Salt otomatik üretilir)
    std::string salt = Korsancim::HashHelper::generate_salt();
    std::string hashed_password = Korsancim::HashHelper::hash_password(raw_password, salt);
    
    EXPECT_NE(raw_password, hashed_password);
    EXPECT_FALSE(hashed_password.empty());

    // 2. Doğru şifre ile doğrulama yap
    bool is_correct = Korsancim::HashHelper::verify_password(raw_password, hashed_password);
    EXPECT_TRUE(is_correct);

    // 3. Yanlış şifre ile doğrulama dene
    bool is_wrong = Korsancim::HashHelper::verify_password("Yanlis_Sifre_123", hashed_password);
    EXPECT_FALSE(is_wrong);
}
// TEST 6: User Ban & Role Management Test
// TEST 6: User Ban & Role Management Test
TEST(AdminTest, BanAndRoleManagement) {
    Korsancim::Database db("test_korsancim.db");
    ASSERT_TRUE(db.connect());

    // Tabloların var olduğundan emin olalım
    db.execute("CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT UNIQUE, password_hash TEXT, role TEXT DEFAULT 'user', is_banned INTEGER DEFAULT 0, ban_reason TEXT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");

    std::string test_user = "banli_korsan";
    db.register_user(test_user, "Sifre123!");

    Korsancim::User u;
    ASSERT_TRUE(db.get_user_by_username(test_user, u));
    EXPECT_FALSE(u.is_banned);

    // 1. Kullanıcıyı Banla
    EXPECT_TRUE(db.ban_user(u.id, "Kural ihlali yapti"));
    
    Korsancim::User banned_u;
    ASSERT_TRUE(db.get_user_by_username(test_user, banned_u));
    EXPECT_TRUE(banned_u.is_banned);
    EXPECT_EQ(banned_u.ban_reason, "Kural ihlali yapti");

    // 2. Banı Kaldır
    EXPECT_TRUE(db.unban_user(u.id));
    
    Korsancim::User unbanned_u;
    ASSERT_TRUE(db.get_user_by_username(test_user, unbanned_u));
    EXPECT_FALSE(unbanned_u.is_banned);

    // 3. Rolü Moderator Yap
    EXPECT_TRUE(db.update_user_role(u.id, "moderator"));
    
    Korsancim::User mod_u;
    ASSERT_TRUE(db.get_user_by_username(test_user, mod_u));
    EXPECT_EQ(mod_u.role, "moderator");

    db.disconnect();
}
// TEST 7: Logger Testi
TEST(LoggerTest, ConsoleLogging) {
    EXPECT_NO_THROW(Korsancim::Logger::info("Test bilgi mesaji"));
    EXPECT_NO_THROW(Korsancim::Logger::warn("Test uyari mesaji"));
    EXPECT_NO_THROW(Korsancim::Logger::error("Test hata mesaji"));
}
// TEST 8: Rate Limiter (Brute-Force Koruması)
TEST(MiddlewareTest, RateLimiting) {
    // 1 saniyede maksimum 3 isteğe izin veren sınarlayıcı
    Korsancim::RateLimiter limiter(3, 1); 
    std::string test_ip = "192.168.1.100";

    EXPECT_TRUE(limiter.is_allowed(test_ip));  // 1. İstek - İzin Verildi
    EXPECT_TRUE(limiter.is_allowed(test_ip));  // 2. İstek - İzin Verildi
    EXPECT_TRUE(limiter.is_allowed(test_ip));  // 3. İstek - İzin Verildi
    EXPECT_FALSE(limiter.is_allowed(test_ip)); // 4. İstek - BLOKLANDI!
}