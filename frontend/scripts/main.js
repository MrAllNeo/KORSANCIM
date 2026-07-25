// Backend API Adresimiz
const API_URL = 'http://localhost:3000/api';

// 1. Anasayfa Konularını Backend'den Çek ve Listele
async function fetchTopics() {
    try {
        const response = await fetch(`${API_URL}/topics`);
        const topics = await response.json();

        const container = document.getElementById('topics-container');
        if (!container) return;

        container.innerHTML = topics.map(topic => `
            <div class="bg-slate-900/50 backdrop-blur-xl border border-slate-800/80 rounded-2xl p-5 shadow-xl hover:border-slate-700/80 transition group">
                <div class="flex items-start justify-between gap-4">
                    <div class="flex gap-4">
                        <div class="w-10 h-10 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-slate-300 shrink-0">
                            ${topic.author ? topic.author[0].toUpperCase() : 'U'}
                        </div>
                        <div>
                            <div class="flex items-center gap-2 mb-1">
                                <span class="text-xs font-semibold px-2 py-0.5 rounded-md bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">${topic.category_name || 'Genel'}</span>
                                <span class="text-xs text-slate-500">• ${topic.created_at}</span>
                            </div>
                            <h2 class="text-base font-bold text-slate-100 group-hover:text-indigo-400 transition cursor-pointer" onclick="window.location.href='topic-detail.html?id=${topic.id}'">
                                ${topic.title}
                            </h2>
                            <p class="text-slate-400 text-sm mt-1 line-clamp-1">${topic.content}</p>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');

    } catch (error) {
        console.error('Konular çekilirken hata oluştu:', error);
    }
}

// Sayfa yüklendiğinde çalıştır
document.addEventListener('DOMContentLoaded', () => {
    fetchTopics();
});