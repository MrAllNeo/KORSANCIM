// "Sakin derinlik" paleti — Tailwind CDN'e semantik renk isimleri tanıtır.
// CDN script'inden SONRA, sayfa içeriğinden ÖNCE yüklenmeli.
tailwind.config = {
    theme: {
        extend: {
            colors: {
                // Yüzeyler: düz, gradyansız, katman katman açılan koyu tonlar
                base: '#0b0d10',      // sayfa zemini
                surface: '#14171c',   // kart
                raised: '#191d23',    // kart üstü / hover
                sunken: '#0f1216',    // girdi alanı, kod bloğu

                // Kenarlıklar ayrımı taşır, gölge değil
                line: '#232830',
                'line-strong': '#2e3540',

                // Metin hiyerarşisi
                ink: '#e6e8eb',
                'ink-soft': '#9aa4b2',
                'ink-faint': '#6b7480',

                // Tek vurgu rengi
                accent: '#6366f1',
                'accent-soft': '#818cf8',
                'accent-dim': '#312e81',

                ok: '#34d399',
                warn: '#fbbf24',
                bad: '#f87171'
            },
            borderRadius: {
                DEFAULT: '10px',
                lg: '12px',
                xl: '14px'
            },
            fontFamily: {
                sans: ['ui-sans-serif', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'Helvetica Neue', 'Arial', 'sans-serif']
            }
        }
    }
};
