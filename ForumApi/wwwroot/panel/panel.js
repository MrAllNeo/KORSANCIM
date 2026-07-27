// Panel sayfalarının ortak üst çubuğu. js/app.js'ten SONRA yüklenmeli
// (requireStaff, isAdmin, isOwner, escapeHtml vb. oradan gelir).
function renderPanelNav(active) {
    const mount = document.getElementById('panel-header');
    if (!mount) return;

    const link = (href, icon, text, key) => `
        <a href="${href}" class="btn btn-quiet btn-sm ${active === key ? 'text-ink' : ''}">
            <i data-lucide="${icon}" class="w-4 h-4"></i>
            <span class="hidden sm:inline">${text}</span>
        </a>`;

    mount.innerHTML = `
        <header class="sticky top-0 z-40 border-b border-line bg-base/85 backdrop-blur">
            <div class="max-w-5xl mx-auto px-4 h-14 flex items-center gap-1">
                <a href="index.html" class="flex items-center gap-2 mr-3 shrink-0" aria-label="Panel anasayfası">
                    <img src="../static/favicon.svg" alt="" class="w-7 h-7 rounded-md">
                    <span class="font-bold text-sm text-ink hidden sm:inline">Yönetim Paneli</span>
                </a>
                ${link('index.html', 'layout-dashboard', 'Panel', 'dashboard')}
                ${link('users.html', 'users', 'Kullanıcılar', 'users')}
                ${link('content.html', 'file-text', 'İçerik', 'content')}
                ${link('reports.html', 'flag', 'Şikayetler', 'reports')}
                ${isAdmin() ? link('badges.html', 'award', 'Rozetler', 'badges') : ''}
                ${isAdmin() ? link('categories.html', 'folder-tree', 'Kategoriler', 'categories') : ''}
                <a href="../index.html" class="btn btn-quiet btn-sm ml-auto">
                    <i data-lucide="arrow-left" class="w-4 h-4"></i>
                    <span class="hidden sm:inline">Siteye Dön</span>
                </a>
            </div>
        </header>`;

    if (typeof lucide !== 'undefined') lucide.createIcons();
}

// Basit sayfalama şeridi — panel sayfaları arasında ortak.
function renderPagination(mountId, page, totalPages, onPage) {
    const mount = document.getElementById(mountId);
    if (!mount) return;

    if (totalPages <= 1) {
        mount.innerHTML = '';
        return;
    }

    let html = '';
    for (let p = 1; p <= totalPages; p++) {
        html += `<button data-page="${p}" class="btn btn-quiet btn-sm ${p === page ? 'text-ink' : ''}">${p}</button>`;
    }
    mount.innerHTML = html;
    mount.querySelectorAll('button[data-page]').forEach(btn => {
        btn.addEventListener('click', () => onPage(Number(btn.dataset.page)));
    });
}
