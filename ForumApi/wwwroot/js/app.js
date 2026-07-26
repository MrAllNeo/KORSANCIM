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
        expiresAt: Date.now() + (data.expiresInHours || 12) * 3600 * 1000
    }));
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

// ── Ortak Header (giriş/çıkış alanı) ────────────────────────
function renderUserNav() {
    const userNav = document.getElementById('user-nav-area');
    if (!userNav) return;

    const username = getUsername();

    if (username) {
        userNav.innerHTML = `
            <div class="flex items-center gap-3 bg-slate-950/80 border border-slate-800/80 pl-2 pr-3 py-1.5 rounded-xl text-xs">
                <a href="profile.html?u=${escapeAttr(username)}" class="flex items-center gap-2 hover:text-indigo-400 transition group cursor-pointer">
                    <div class="w-7 h-7 rounded-lg bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center shrink-0 group-hover:border-indigo-500/50">
                        <i data-lucide="user" class="w-4 h-4 text-indigo-400"></i>
                    </div>
                    <span class="text-slate-200 font-semibold group-hover:text-indigo-400 transition">${escapeHtml(username)}</span>
                </a>
                <button onclick="logout()" title="Çıkış Yap" class="text-slate-500 hover:text-rose-400 transition ml-1 p-1">
                    <i data-lucide="log-out" class="w-3.5 h-3.5"></i>
                </button>
            </div>
        `;
    } else {
        userNav.innerHTML = `
            <a href="auth.html" class="bg-slate-800 hover:bg-slate-700 text-slate-200 px-3.5 py-2 rounded-xl text-xs font-semibold transition flex items-center gap-1.5 border border-slate-700">
                <i data-lucide="user" class="w-4 h-4"></i> Giriş Yap / Kayıt
            </a>
        `;
    }

    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
}
