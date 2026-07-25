-- KORSANCIM Veritabanı Şeması (SQLite)
-- Bu dosya referans amaçlıdır. Gerçek tablo oluşturma main.cpp bootstrap'te yapılır.
PRAGMA foreign_keys = ON;

-- 1. Kullanıcılar Tablosu (Tam Anonim)
-- E-posta, IP veya gerçek isim TUTULMAZ.
CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,           -- PBKDF2-SHA256 (salt$hash formatı)
    role          TEXT DEFAULT 'user',     -- 'user', 'moderator', 'admin'
    is_banned     INTEGER DEFAULT 0,       -- 0 = aktif, 1 = banlı
    ban_reason    TEXT DEFAULT '',
    created_at    DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 2. Kategoriler Tablosu
CREATE TABLE IF NOT EXISTS categories (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT UNIQUE NOT NULL,
    description TEXT,
    slug        TEXT UNIQUE NOT NULL
);

-- 3. Konular (Topics/Threads) Tablosu
CREATE TABLE IF NOT EXISTS topics (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL,
    user_id     INTEGER NOT NULL,
    title       TEXT NOT NULL,
    content     TEXT NOT NULL,
    created_at  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE CASCADE,
    FOREIGN KEY(user_id)     REFERENCES users(id)      ON DELETE CASCADE
);

-- 4. Yorumlar Tablosu
CREATE TABLE IF NOT EXISTS comments (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    topic_id   INTEGER NOT NULL,
    user_id    INTEGER NOT NULL,
    content    TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(topic_id) REFERENCES topics(id)  ON DELETE CASCADE,
    FOREIGN KEY(user_id)  REFERENCES users(id)   ON DELETE CASCADE
);

-- Performans Indexleri
CREATE INDEX IF NOT EXISTS idx_topics_category  ON topics(category_id);
CREATE INDEX IF NOT EXISTS idx_comments_topic   ON comments(topic_id);
CREATE INDEX IF NOT EXISTS idx_users_username   ON users(username);

-- Varsayılan Kategoriler
INSERT OR IGNORE INTO categories (id, name, description, slug) VALUES
(1, 'Genel Sohbet',       'Gereksiz sohbetlerin ve muhabbetin adresi',             'genel-sohbet'),
(2, 'Yazılım & Teknoloji','C++, Linux, Python ve kodlama dünyası',                  'yazilim-teknoloji'),
(3, 'Özgür Yazılım & Linux','Linux dağıtımları, açık kaynak araçlar ve felsefesi', 'ozgur-yazilim-linux');