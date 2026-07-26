#!/usr/bin/env bash
# Geliştirme sunucusunu güvenilir biçimde yeniden başlatır.
# Süreç "dotnet" değil "ForumApi" adlı apphost olarak göründüğü için
# ada göre arama yapmak yanıltıcı; portu doğrudan yokluyoruz.
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/ForumApi/ForumApi.csproj"
PORT="${PORT:-5085}"
URL="http://localhost:$PORT"
LOG="${LOG:-/tmp/korsancim-server.log}"

stop() {
    pkill -f "$ROOT/ForumApi/bin/.*/ForumApi" 2>/dev/null
    for _ in $(seq 1 20); do
        curl -s -o /dev/null --max-time 1 "$URL/" || return 0
        sleep 0.5
    done
    echo "UYARI: $PORT hâlâ dolu" >&2
    return 1
}

start() {
    dotnet build "$PROJECT" 2>&1 | grep -E "error|Build succeeded" || return 1

    (
        cd "$ROOT/ForumApi" || exit 1
        Jwt__Key="${Jwt__Key:-dev-anahtari-en-az-32-karakter-olmali!!}" \
        ASPNETCORE_URLS="$URL" \
        setsid dotnet run --no-build > "$LOG" 2>&1 &
    )

    for _ in $(seq 1 60); do
        if curl -s -o /dev/null --max-time 1 "$URL/"; then
            echo "sunucu hazır: $URL  (log: $LOG)"
            return 0
        fi
        sleep 1
    done

    echo "sunucu açılmadı, son log:" >&2
    tail -20 "$LOG" >&2
    return 1
}

case "${1:-restart}" in
    stop)    stop ;;
    start)   start ;;
    restart) stop && start ;;
    *) echo "kullanım: $0 [start|stop|restart]" >&2; exit 1 ;;
esac
