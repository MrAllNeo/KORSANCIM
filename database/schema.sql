-- KORSANCIM Veritabanı Şeması (SQLite)
PRAGMA foreign_keys = ON;

-- 1. Kullanıcılar Tablosu (Tam Anonim)
-- E-posta, IP veya gerçek isim TUTULMAZ.
CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT UNIQUE NOT NULL,       -- Rumuz
    password_hash TEXT NOT NULL,         -- Şifrelenmiş Parola (SHA256 / Argon2)
    role TEXT DEFAULT 'user',            -- 'user' veya 'admin'
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 2. Kategoriler Tablosu
CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT UNIQUE NOT NULL,
    description TEXT,
    slug TEXT UNIQUE NOT NULL
);

-- 3. Konular (Topics/Threads) Tablosu
CREATE TABLE IF NOT EXISTS topics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    title TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE CASCADE,
    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- 4. Yanıtlar / Yorumlar (Posts/Replies) Tablosu
CREATE TABLE IF NOT EXISTS posts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    topic_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    content TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(topic_id) REFERENCES topics(id) ON DELETE CASCADE,
    FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Varsayılan Başlangıç Kategorileri
INSERT OR IGNORE INTO categories (name, description, slug) VALUES 
('Özgür Yazılım & Linux', 'Linux dağıtımları, açık kaynak araçlar ve felsefesi', 'ozgur-yazilim-linux'),
('C++ & Geliştirme', 'C++ kodlama, mimari ve performans sohbetleri', 'cpp-gelistirme'),
('Genel & Anonim Sohbet', 'Özgür internet üzerine serbest kürsü', 'genel-anonim-sohbet');