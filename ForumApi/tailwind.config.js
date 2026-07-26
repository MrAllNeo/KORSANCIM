/** @type {import('tailwindcss').Config} */
// Eskiden wwwroot/js/theme.js içinde CDN'e runtime'da veriliyordu; artık
// derleme zamanında burada okunuyor. Renk değerleri wwwroot/css/app.css'teki
// :root custom property'leriyle birebir aynı tutulmalı.
module.exports = {
    content: ["./wwwroot/**/*.html", "./wwwroot/js/**/*.js"],
    theme: {
        extend: {
            colors: {
                base: '#0b0d10',
                surface: '#14171c',
                raised: '#191d23',
                sunken: '#0f1216',

                line: '#232830',
                'line-strong': '#2e3540',

                ink: '#e6e8eb',
                'ink-soft': '#9aa4b2',
                'ink-faint': '#6b7480',

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
    },
    plugins: []
};
