#include <gtest/gtest.h>
#include "db/database.hpp"
#include "utils/jwt_helper.hpp"
#include "utils/hash_helper.hpp"
#include "utils/logger.hpp"
#include "utils/token_blacklist.hpp"
#include "middleware/auth_middleware.hpp"
#include "middleware/rate_limiter.hpp"
#include <fstream>
#include <cstdlib>

// ─────────────────────────────────────────────────────────────
// Test Fixture — Her test için temiz bir DB
// ─────────────────────────────────────────────────────────────
class DatabaseTest : public ::testing::Test {
protected:
    std::string test_db_path = "test_korsancim_unit.db";
    Korsancim::Database* db;

    void SetUp() override {
        db = new Korsancim::Database(test_db_path);
        ASSERT_TRUE(db->connect());

        db->execute("CREATE TABLE IF NOT EXISTS users ("
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT UNIQUE NOT NULL,"
                    "password_hash TEXT NOT NULL, role TEXT DEFAULT 'user',"
                    "is_banned INTEGER DEFAULT 0, ban_reason TEXT DEFAULT '',"
                    "created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");
        db->execute("CREATE TABLE IF NOT EXISTS categories ("
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT UNIQUE,"
                    "description TEXT, slug TEXT UNIQUE);");
        db->execute("CREATE TABLE IF NOT EXISTS topics ("
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, category_id INTEGER,"
                    "user_id INTEGER, title TEXT, content TEXT,"
                    "created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");
        db->execute("CREATE TABLE IF NOT EXISTS comments ("
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, topic_id INTEGER,"
                    "user_id INTEGER, content TEXT,"
                    "created_at DATETIME DEFAULT CURRENT_TIMESTAMP);");
        db->execute("INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES "
                    "(1, 'Test Kategori', 'Test açıklaması', 'test-kategori');");
    }

    void TearDown() override {
        db->disconnect();
        delete db;
        std::remove(test_db_path.c_str());
    }
};

// ─────────────────────────────────────────────────────────────
// TEST 1: Veritabanı Bağlantı
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, ConnectionTest) {
    EXPECT_NE(db->get_db_handle(), nullptr);
}

// ─────────────────────────────────────────────────────────────
// TEST 2: Kullanıcı Kaydı
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, UserRegistrationTest) {
    EXPECT_TRUE(db->register_user("test_korsan", "Sifre12345!"));
    // Aynı kullanıcı tekrar kaydolamamalı
    EXPECT_FALSE(db->register_user("test_korsan", "Sifre12345!"));
}

// ─────────────────────────────────────────────────────────────
// TEST 3: Kullanıcı Doğrulama
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, UserAuthenticationTest) {
    ASSERT_TRUE(db->register_user("auth_korsan", "DogruSifre123!"));
    EXPECT_TRUE(db->authenticate_user("auth_korsan", "DogruSifre123!"));
    EXPECT_FALSE(db->authenticate_user("auth_korsan", "YanlisSifre!"));
    EXPECT_FALSE(db->authenticate_user("olmayan_user", "herhangi"));
}

// ─────────────────────────────────────────────────────────────
// TEST 4: Konu Oluşturma ve Pagination
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, TopicCreateAndPaginationTest) {
    db->register_user("yazar", "Sifre12345!");
    // Birden fazla konu oluştur
    for (int i = 0; i < 5; i++) {
        db->create_topic(1, 1, "Başlık " + std::to_string(i), "İçerik " + std::to_string(i));
    }
    EXPECT_EQ(db->get_topic_count(1), 5);
    // Sayfa 1, 3 konu
    auto page1 = db->get_topics(1, 1, 3);
    EXPECT_EQ(static_cast<int>(page1.size()), 3);
    // Sayfa 2, 2 konu
    auto page2 = db->get_topics(1, 2, 3);
    EXPECT_EQ(static_cast<int>(page2.size()), 2);
}

// ─────────────────────────────────────────────────────────────
// TEST 5: Konu Güncelleme (Sahiplik Kontrolü)
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, TopicUpdateOwnershipTest) {
    db->register_user("sahip", "Sifre12345!");
    db->create_topic(1, 1, "Orijinal Başlık", "Orijinal İçerik");

    // Sahip güncelleyebilmeli
    EXPECT_TRUE(db->update_topic(1, 1, "Yeni Başlık", "Yeni İçerik"));
    // Başka biri güncelleyememeli
    EXPECT_FALSE(db->update_topic(1, 99, "Hack Başlık", "Hack İçerik"));
}

// ─────────────────────────────────────────────────────────────
// TEST 6: Yorum Oluşturma ve Güncelleme
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, CommentCRUDTest) {
    db->register_user("yorumcu", "Sifre12345!");
    db->create_topic(1, 1, "Test Konu", "İçerik");
    EXPECT_TRUE(db->create_comment(1, 1, "Test yorumu"));

    auto comments = db->get_comments_by_topic(1);
    ASSERT_EQ(static_cast<int>(comments.size()), 1);
    EXPECT_EQ(comments[0].content, "Test yorumu");

    // Sahip güncelleyebilmeli
    EXPECT_TRUE(db->update_comment(1, 1, "Güncellenmiş yorum"));
    // Başka biri güncelleyememeli
    EXPECT_FALSE(db->update_comment(1, 99, "Başkasının yorumu"));
}

// ─────────────────────────────────────────────────────────────
// TEST 7: Admin Ban / Unban / Rol
// ─────────────────────────────────────────────────────────────
TEST_F(DatabaseTest, BanAndRoleManagementTest) {
    ASSERT_TRUE(db->register_user("banli_korsan", "Sifre12345!"));
    Korsancim::User u;
    ASSERT_TRUE(db->get_user_by_username("banli_korsan", u));
    EXPECT_FALSE(u.is_banned);

    EXPECT_TRUE(db->ban_user(u.id, "Kural ihlali"));
    Korsancim::User banned;
    ASSERT_TRUE(db->get_user_by_username("banli_korsan", banned));
    EXPECT_TRUE(banned.is_banned);
    EXPECT_EQ(banned.ban_reason, "Kural ihlali");

    EXPECT_TRUE(db->unban_user(u.id));
    Korsancim::User unbanned;
    ASSERT_TRUE(db->get_user_by_username("banli_korsan", unbanned));
    EXPECT_FALSE(unbanned.is_banned);

    EXPECT_TRUE(db->update_user_role(u.id, "moderator"));
    Korsancim::User mod;
    ASSERT_TRUE(db->get_user_by_username("banli_korsan", mod));
    EXPECT_EQ(mod.role, "moderator");
}

// ─────────────────────────────────────────────────────────────
// TEST 8: PBKDF2 Şifre Hash ve Doğrulama
// ─────────────────────────────────────────────────────────────
TEST(HashTest, PBKDF2PasswordHashingAndVerification) {
    std::string password = "Korsan_Sifre_2026!";

    std::string salt   = Korsancim::HashHelper::generate_salt();
    std::string hashed = Korsancim::HashHelper::hash_password(password, salt);

    // Hash, şifrenin kendisi değil
    EXPECT_NE(password, hashed);
    EXPECT_FALSE(hashed.empty());
    // Format: salt$hash
    EXPECT_NE(hashed.find('$'), std::string::npos);

    // Doğru şifre ile doğrulama
    EXPECT_TRUE(Korsancim::HashHelper::verify_password(password, hashed));
    // Yanlış şifre ile doğrulama
    EXPECT_FALSE(Korsancim::HashHelper::verify_password("Yanlis_Sifre_123", hashed));

    // Farklı salt → farklı hash (deterministik değil, salt-dependent)
    std::string salt2   = Korsancim::HashHelper::generate_salt();
    std::string hashed2 = Korsancim::HashHelper::hash_password(password, salt2);
    EXPECT_NE(hashed, hashed2);
}

// ─────────────────────────────────────────────────────────────
// TEST 9: JWT Token Üretme ve Doğrulama
// ─────────────────────────────────────────────────────────────
TEST(JwtTest, GenerateAndVerifyToken) {
    // Env var set et (test ortamı için)
    setenv("KORSANCIM_JWT_SECRET", "test_gizli_anahtar_minimum_32_char_test!", 1);

    std::string token = Korsancim::JwtHelper::generate_token(42, "korsan_kaptan", "admin");
    EXPECT_FALSE(token.empty());

    int out_id = 0;
    std::string out_user, out_role;
    EXPECT_TRUE(Korsancim::JwtHelper::verify_token(token, out_id, out_user, out_role));
    EXPECT_EQ(out_id, 42);
    EXPECT_EQ(out_user, "korsan_kaptan");
    EXPECT_EQ(out_role, "admin");

    // Bozuk token reddedilmeli
    EXPECT_FALSE(Korsancim::JwtHelper::verify_token(token + "bozuk", out_id, out_user, out_role));
}

// ─────────────────────────────────────────────────────────────
// TEST 10: Token Blacklist (Logout)
// ─────────────────────────────────────────────────────────────
TEST(TokenBlacklistTest, BlacklistAndCheck) {
    auto& bl = Korsancim::TokenBlacklist::instance();
    std::string fake_token = "test.blacklist.token";

    EXPECT_FALSE(bl.is_blacklisted(fake_token));
    bl.add(fake_token);
    EXPECT_TRUE(bl.is_blacklisted(fake_token));
    EXPECT_FALSE(bl.is_blacklisted("baska.token"));
}

// ─────────────────────────────────────────────────────────────
// TEST 11: Rate Limiter
// ─────────────────────────────────────────────────────────────
TEST(MiddlewareTest, RateLimiting) {
    Korsancim::RateLimiter limiter(3, 60);  // 60 sn'de max 3 istek
    std::string ip = "10.0.0.99";

    EXPECT_TRUE(limiter.is_allowed(ip));   // 1. istek
    EXPECT_TRUE(limiter.is_allowed(ip));   // 2. istek
    EXPECT_TRUE(limiter.is_allowed(ip));   // 3. istek
    EXPECT_FALSE(limiter.is_allowed(ip));  // 4. istek — BLOKLANDI
    EXPECT_FALSE(limiter.is_allowed(ip));  // 5. istek — hâlâ bloklu
}

// ─────────────────────────────────────────────────────────────
// TEST 12: Input Validasyon Yardımcıları
// ─────────────────────────────────────────────────────────────
TEST(ValidationTest, InputValidationHelpers) {
    // Username: 3-32 karakter
    EXPECT_FALSE(Korsancim::validate_username("ab"));      // çok kısa
    EXPECT_TRUE(Korsancim::validate_username("abc"));      // tam sınır
    EXPECT_TRUE(Korsancim::validate_username("merhaba"));  // geçerli
    EXPECT_FALSE(Korsancim::validate_username(std::string(33, 'x'))); // çok uzun

    // Password: min 8 karakter
    EXPECT_FALSE(Korsancim::validate_password("1234567")); // 7 karakter
    EXPECT_TRUE(Korsancim::validate_password("12345678")); // 8 karakter

    // Title: 5-200 karakter
    EXPECT_FALSE(Korsancim::validate_title("abc"));        // çok kısa
    EXPECT_TRUE(Korsancim::validate_title("Beş karakter")); // geçerli

    // Content: 1-10000 karakter
    EXPECT_FALSE(Korsancim::validate_content(""));         // boş
    EXPECT_TRUE(Korsancim::validate_content("a"));         // tek karakter geçerli
}

// ─────────────────────────────────────────────────────────────
// TEST 13: Logger (Crash etmemeli)
// ─────────────────────────────────────────────────────────────
TEST(LoggerTest, ConsoleLogging) {
    EXPECT_NO_THROW(Korsancim::Logger::info("Test bilgi mesajı"));
    EXPECT_NO_THROW(Korsancim::Logger::warn("Test uyarı mesajı"));
    EXPECT_NO_THROW(Korsancim::Logger::error("Test hata mesajı"));
    EXPECT_NO_THROW(Korsancim::Logger::debug("Test debug mesajı"));
}

// ─────────────────────────────────────────────────────────────
// TEST 14: safe_stoi
// ─────────────────────────────────────────────────────────────
TEST(UtilsTest, SafeStoi) {
    EXPECT_EQ(Korsancim::safe_stoi("42"),    42);
    EXPECT_EQ(Korsancim::safe_stoi("0"),     0);
    EXPECT_EQ(Korsancim::safe_stoi("abc"),  -1);   // geçersiz → default
    EXPECT_EQ(Korsancim::safe_stoi(nullptr), -1);  // nullptr → default
    EXPECT_EQ(Korsancim::safe_stoi("",      99),   99); // boş → custom default
}