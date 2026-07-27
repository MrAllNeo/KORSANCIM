// Tüm sayfaların paylaştığı yardımcılar: XSS kaçışı, oturum ve API çağrısı.

// ── XSS Koruması ────────────────────────────────────────────
// Kullanıcı içeriği innerHTML ile basıldığı için HTML'e gömülmeden önce
// mutlaka bu fonksiyondan geçirilmeli.
function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// URL parametresi olarak gömülecek değerler için.
function escapeAttr(value) {
    return encodeURIComponent(value === null || value === undefined ? '' : String(value));
}

// Metni kısaltır — uzun içerikleri liste görünümünde sınırlamak için.
function truncate(value, max = 160) {
    const text = String(value || '');
    return text.length > max ? text.slice(0, max).trimEnd() + '…' : text;
}

// Arama terimini <mark> ile işaretler.
// ÖNCE escape edip SONRA işaretliyoruz; hem metin hem terim kaçırılmış
// olduğu için üretilen HTML güvenli.
function highlight(value, query) {
    const safe = escapeHtml(value);
    const term = String(query || '').trim();
    if (term.length < 2) return safe;

    const safeTerm = escapeHtml(term).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return safe.replace(new RegExp(safeTerm, 'gi'), m => `<mark class="search-hit">${m}</mark>`);
}

// ── Zengin içerik (kod farkında hafif işaretleme) ───────────
// Kod bloklarını ESCAPE'TEN ÖNCE geçici bir token'la çıkarıyoruz ki içindeki
// ** / * / ` gibi işaretler markdown olarak yorumlanmasın. Geri kalan metin
// escapeHtml'den geçtikten SONRA yalnızca bizim eklediğimiz etiketler (<p>,
// <strong>, <code>, <pre>...) basılıyor — kullanıcı girdisi hiçbir zaman ham
// HTML olarak yorumlanmıyor.
function renderRichContent(raw) {
    const blocks = [];
    const withPlaceholders = String(raw || '').replace(
        /```([a-zA-Z0-9_+-]*)\n?([\s\S]*?)```/g,
        (_, lang, code) => {
            const token = `CODEBLOCK${blocks.length}`;
            blocks.push({ lang: lang.trim().replace(/[^a-zA-Z0-9_-]/g, ''), code });
            return token;
        }
    );

    let escaped = escapeHtml(withPlaceholders);
    escaped = escaped.replace(/`([^`\n]+)`/g, (_, code) => `<code class="inline-code">${code}</code>`);
    escaped = escaped.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>');
    escaped = escaped.replace(/(^|[^*])\*([^*\n]+)\*(?!\*)/g, '$1<em>$2</em>');

    // Paragraflara SONRA bölüyoruz — kod bloğu düz metinle aynı satırda (boş
    // satır olmadan) yazılsa bile placeholder kaybolmasın; tarayıcı <p>
    // içindeki <pre>'yi otomatik ayırır, görsel bir sorun çıkarmaz.
    const withParagraphs = escaped
        .split(/\n{2,}/)
        .map(segment => `<p>${segment.replace(/\n/g, '<br>')}</p>`)
        .join('');

    return withParagraphs.replace(/CODEBLOCK(\d+)/g, (_, i) => {
        const block = blocks[Number(i)];
        const langClass = block.lang ? ` class="language-${block.lang}"` : '';
        return `<pre class="code-block"><code${langClass}>${escapeHtml(block.code)}</code></pre>`;
    });
}

// renderRichContent çıktısındaki kod bloklarını sözdizimi renklendirmesinden
// geçirir. highlight.js CDN'den yüklenmemişse sessizce atlanır.
function highlightCodeBlocks() {
    if (typeof hljs === 'undefined') return;
    document.querySelectorAll('pre.code-block code').forEach(el => hljs.highlightElement(el));
}

// Liste/kart önizlemeleri için işaretleyicileri temizlenmiş düz metin —
// aksi halde kod bloğu ``` işaretleri kısaltılmış önizlemede çıplak görünürdü.
function stripRichSyntax(raw) {
    return String(raw || '')
        .replace(/```[a-zA-Z0-9_+-]*\n?([\s\S]*?)```/g, '$1')
        .replace(/`([^`\n]+)`/g, '$1')
        .replace(/\*\*([^*\n]+)\*\*/g, '$1')
        .replace(/(^|[^*])\*([^*\n]+)\*(?!\*)/g, '$1$2')
        .replace(/\s+/g, ' ')
        .trim();
}

// Varsayılan avatar. SVG nitelikleri TEK tırnakla yazılı — bu değer
// src="..." içine gömüldüğünde çift tırnak niteliği erken kapatıyordu.
const DEFAULT_AVATAR = "data:image/svg+xml;utf8,"
    + "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32' viewBox='0 0 24 24'"
    + " fill='none' stroke='%23818cf8' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>"
    + "<path d='M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2'/><circle cx='12' cy='7' r='4'/></svg>";

// ── Kullanıcı Ünvanları (tier) ──────────────────────────────
// Stiller css/app.css içinde. Eşleşme TAM kullanıcı adı üzerinden yapılır —
// eskiden 'creator' geçen her ad (ör. "creator123") ünvanı kapıyordu.
const USER_TIERS = [
    {
        id: 'creator',
        usernames: ['creator'],
        label: 'CREATOR & FOUNDER',
        icon: 'crown',
        badgeClass: 'tier-badge tier-shine tier-creator-badge',
        nameClass: 'tier-creator-name'
    },
    {
        id: 'assistant',
        usernames: ['claude'],
        label: 'ASSISTANT OF CREATOR',
        icon: 'sparkles',
        badgeClass: 'tier-badge tier-shine tier-assistant-badge',
        nameClass: 'tier-assistant-name'
    }
];

const MEMBER_TIER = {
    id: 'member',
    label: 'Korsan',
    icon: null,
    badgeClass: 'tier-badge tier-member-badge',
    nameClass: 'tier-member-name'
};

function getUserTier(username) {
    const key = String(username || '').trim().toLowerCase();
    return USER_TIERS.find(t => t.usernames.includes(key)) || MEMBER_TIER;
}

// size: 'sm' (yorum/liste içi) | 'md' (profil başlığı)
function renderBadge(username, size = 'sm') {
    const tier = getUserTier(username);
    const sizing = size === 'md'
        ? 'text-[11px] px-3 py-1 rounded-full'
        : 'text-[10px] px-2 py-0.5 rounded-md';
    const icon = tier.icon
        ? `<i data-lucide="${tier.icon}" class="w-3.5 h-3.5 shrink-0"></i>`
        : '';

    return `<span class="${tier.badgeClass} ${sizing}">${icon}${escapeHtml(tier.label)}</span>`;
}

function renderUserName(username, extraClass = '') {
    const tier = getUserTier(username);
    return `<span class="${tier.nameClass} ${extraClass}">${escapeHtml(username)}</span>`;
}

// ── Oturum ──────────────────────────────────────────────────
const SESSION_KEY = 'currentUser';

function getSession() {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    try {
        const session = JSON.parse(raw);
        if (!session || !session.token || !session.username) return null;
        if (session.expiresAt && Date.now() > session.expiresAt) {
            localStorage.removeItem(SESSION_KEY);
            return null;
        }
        return session;
    } catch {
        localStorage.removeItem(SESSION_KEY);
        return null;
    }
}

function saveSession(data) {
    localStorage.setItem(SESSION_KEY, JSON.stringify({
        token: data.token,
        username: data.username,
        email: data.email,
        userId: data.userId,
        avatarUrl: data.avatarUrl || null,
        role: data.role || 'User',
        expiresAt: Date.now() + (data.expiresInHours || 12) * 3600 * 1000
    }));
}

// Profil sayfasında kendi avatarını değiştirince header'ın da yeni resmi
// göstermesi için oturumdaki avatarUrl'i güncelleriz — yoksa bir sonraki
// girişe kadar eski görsel görünmeye devam eder.
function updateSessionAvatar(avatarUrl) {
    const session = getSession();
    if (!session) return;
    session.avatarUrl = avatarUrl || null;
    localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

function getUsername() {
    const session = getSession();
    return session ? session.username : null;
}

function isLoggedIn() {
    return getSession() !== null;
}

function logout() {
    localStorage.removeItem(SESSION_KEY);
    window.location.href = 'index.html';
}

// Oturum yoksa giriş sayfasına yollar. Korumalı sayfaların başında çağrılır.
function requireLogin() {
    if (!isLoggedIn()) {
        window.location.href = 'auth.html';
        return false;
    }
    return true;
}

// ── Rol kontrolü ─────────────────────────────────────────────
// Gerçek yetkilendirme her zaman sunucuda (/api/admin/*) — bunlar yalnızca
// arayüzde neyin gösterileceğine karar vermek için.
function getRole() {
    return getSession()?.role || null;
}

function isModerator() { return ['Moderator', 'Admin', 'Owner'].includes(getRole()); }
function isAdmin() { return ['Admin', 'Owner'].includes(getRole()); }
function isOwner() { return getRole() === 'Owner'; }

// Panel sayfalarının başında çağrılır: girişli değilse veya en az Moderator
// değilse siteye geri yollar.
function requireStaff() {
    if (!requireLogin()) return false;
    if (!isModerator()) {
        window.location.href = 'index.html';
        return false;
    }
    return true;
}

// ── API ─────────────────────────────────────────────────────
// Token'ı Authorization header'ına ekler ve 401 durumunda oturumu düşürür.
// Yollar göreli olmalı ('/api/...') — sabit host yazılmamalı.
async function apiFetch(path, options = {}) {
    const session = getSession();
    const headers = new Headers(options.headers || {});

    if (session) {
        headers.set('Authorization', `Bearer ${session.token}`);
    }

    const response = await fetch(path, { ...options, headers });

    if (response.status === 401) {
        localStorage.removeItem(SESSION_KEY);
        window.location.href = 'auth.html';
        throw new Error('Oturum süresi doldu, lütfen tekrar giriş yapın.');
    }

    return response;
}

// JSON gövdeli istekler için kısayol.
function apiFetchJson(path, method, body) {
    return apiFetch(path, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
}

// Hata gövdesinden okunabilir mesaj çıkarır.
async function readError(response, fallback) {
    try {
        const data = await response.json();
        return data.error || data.message || fallback;
    } catch {
        return fallback;
    }
}

// ── Kategoriler ─────────────────────────────────────────────
// Tek kaynak: /api/categories. Sayfa başına bir kez çekilip önbelleğe alınır;
// isimler artık HTML içinde sabit kodlu değil.
let _categoryCache = null;

async function getCategories() {
    if (_categoryCache) return _categoryCache;
    try {
        const res = await fetch('/api/categories');
        _categoryCache = res.ok ? await res.json() : [];
    } catch {
        _categoryCache = [];
    }
    return _categoryCache;
}

function categoryName(id) {
    const found = (_categoryCache || []).find(c => c.id === id);
    return found ? found.name : `Kategori #${id}`;
}

// ── Ortak Başlık ────────────────────────────────────────────
// Her sayfa <div id="site-header"></div> koyar, gerisini burası halleder.
// Böylece profil sekmesi tüm sayfalarda tutarlı biçimde bulunur.
function renderHeader(options = {}) {
    const mount = document.getElementById('site-header');
    if (!mount) return;

    const { active = '', showNewTopic = true } = options;
    const session = getSession();
    const username = session?.username || null;
    const avatarUrl = session?.avatarUrl;

    const navLink = (href, icon, text, key) => `
        <a href="${href}" class="btn btn-quiet ${active === key ? 'text-ink' : ''}">
            <i data-lucide="${icon}" class="w-4 h-4"></i>
            <span class="hidden sm:inline">${text}</span>
        </a>`;

    const userArea = username
        ? `<div class="flex items-center gap-1">
               <a href="profile.html?u=${escapeAttr(username)}"
                  class="btn btn-ghost btn-sm ${active === 'profile' ? 'text-ink' : ''}" title="Profilim">
                   <span class="w-6 h-6 rounded-md bg-accent/15 border border-accent/30 grid place-items-center overflow-hidden shrink-0">
                       ${avatarUrl
                ? `<img src="${escapeHtml(avatarUrl)}" alt="" class="w-full h-full object-cover">`
                : `<span class="text-[10px] font-bold text-accent-soft">${escapeHtml(username.charAt(0).toUpperCase())}</span>`}
                   </span>
                   <span class="hidden sm:flex items-center gap-1.5 min-w-0">
                       ${renderUserName(username, 'text-[13px] truncate max-w-[9ch]')}${renderBadge(username)}
                   </span>
               </a>
               <button onclick="logout()" class="btn btn-quiet btn-danger" title="Çıkış Yap" aria-label="Çıkış Yap">
                   <i data-lucide="log-out" class="w-4 h-4"></i>
               </button>
           </div>`
        : `<a href="auth.html" class="btn btn-ghost btn-sm">
               <i data-lucide="log-in" class="w-4 h-4"></i>
               <span class="hidden sm:inline">Giriş Yap</span>
           </a>`;

    mount.innerHTML = `
        <header class="sticky top-0 z-40 border-b border-line bg-base/85 backdrop-blur">
            <div class="max-w-3xl mx-auto px-4 h-14 flex items-center gap-2">
                <a href="index.html" class="flex items-center gap-2.5 mr-auto shrink-0" aria-label="Ana sayfa">
                    <img src="static/favicon.svg" alt="KORSANCIM" class="w-8 h-8 rounded-lg shrink-0">
                    <span class="font-bold tracking-tight text-ink hidden sm:inline">KORSANCIM</span>
                </a>
                ${navLink('index.html', 'layout-list', 'Konular', 'home')}
                ${showNewTopic ? navLink('create-topic.html', 'square-pen', 'Yeni Konu', 'new') : ''}
                ${isModerator() ? navLink('panel/index.html', 'shield', 'Panel', 'panel') : ''}
                ${userArea}
            </div>
        </header>`;

    if (typeof lucide !== 'undefined') lucide.createIcons();
}

// Geriye dönük uyumluluk: eski sayfalar bu adı çağırıyordu.
function renderUserNav() {
    renderHeader();
}

// ── Küçük yardımcılar ───────────────────────────────────────
function formatDate(value) {
    return new Date(value).toLocaleDateString('tr-TR', {
        day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit'
    });
}

// "3 dakika önce" biçimi — liste görünümlerinde daha okunaklı.
// 30 günü aşan tarihlerde tam tarihe düşer.
function timeAgo(value) {
    const seconds = Math.floor((Date.now() - new Date(value)) / 1000);

    if (seconds < 0) return 'az önce';   // saat farkından doğan negatifler
    if (seconds < 60) return 'az önce';

    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes} dakika önce`;

    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} saat önce`;

    const days = Math.floor(hours / 24);
    if (days < 7) return `${days} gün önce`;
    if (days < 30) return `${Math.floor(days / 7)} hafta önce`;

    return formatDate(value);
}

// Metin alanlarına canlı karakter sayacı bağlar.
function attachCounter(inputId, counterId, max) {
    const input = document.getElementById(inputId);
    const counter = document.getElementById(counterId);
    if (!input || !counter) return;

    const update = () => {
        const used = input.value.length;
        counter.textContent = `${used} / ${max}`;
        counter.classList.toggle('text-bad', used > max);
    };

    input.addEventListener('input', update);
    update();
}
