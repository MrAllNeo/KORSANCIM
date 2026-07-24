#ifndef DATABASE_HPP
#define DATABASE_HPP
#include "models/user.hpp"
#include <sqlite3.h> // SQLite C-API kütüphanesi
#include <string>
#include <iostream>
#include <vector>
#include "../models/comment.hpp"
#include "models/category.hpp"
#include "../models/topic.hpp"
namespace Korsancim {

class Database {
private:
    sqlite3* db; // SQLite veritabanı bağlantı pointer'ı (bellek adresi)
    std::string db_path; // Veritabanı dosyasının yolu (örn: database/korsancim.db)

public:
    // Kurucu Fonksiyon (Constructor): Sınıf oluşturulduğunda dosya yolunu alır
    Database(const std::string& path);

    // Yıkıcı Fonksiyon (Destructor): Sınıf hafızadan silinirken bağlantıyı otomatik kapatır
    ~Database();

    // Veritabanına bağlanma fonksiyonu
    bool connect();

    // Veritabanı bağlantısını kapatma fonksiyonu
    void disconnect();

    // Raw SQL sorgusu çalıştırmak için (Örn: INSERT, UPDATE, DELETE işlemleri)
    bool execute(const std::string& sql);

    // Bağlantı durumunu kontrol etme
    sqlite3* get_db_handle() const { return db; }
    // Tüm kategorileri veritabanından sorgulayıp liste olarak döndürür
    std::vector<Category> get_categories();
    // Yeni kullanıcı kaydı oluşturur
    bool register_user(const std::string& username, const std::string& password_hash);

    // Kullanıcı adının daha önce alınıp alınmadığını kontrol eder
    bool user_exists(const std::string& username);
    // Kullanıcı giriş kontrolü (Kullanıcı adı ve şifre eşleşiyor mu?)
    bool authenticate_user(const std::string& username, const std::string& password);
    // Yeni konu oluşturma
    bool create_topic(int category_id, int user_id, const std::string& title, const std::string& content);
// Yorum İşlemleri (Comments)
    bool create_comment(int topic_id, int user_id, const std::string& content);
    std::vector<Comment> get_comments_by_topic(int topic_id);
    // Konuları listeleme (category_id > 0 ise sadece o kategoriyi, 0 ise tüm konuları getirir)
    std::vector<Topic> get_topics(int category_id = 0);
};

} // namespace Korsancim

#endif // DATABASE_HPP