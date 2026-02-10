// event-forms.js
// イベント作成/編集フォームのプレビューやカラー選択、繰り返し/終日切替を UI で補助するスクリプト。

﻿(function () {
    document.addEventListener('DOMContentLoaded', function () {
        function resolveTimeZone(timeZoneId) {
            if (!timeZoneId) return 'UTC';
            try {
                new Intl.DateTimeFormat('en-US', { timeZone: timeZoneId }).format(new Date());
                return timeZoneId;
            } catch {
                return 'UTC';
            }
        }

        const APP_TIMEZONE = resolveTimeZone(document.body?.dataset?.appTimezone || 'Asia/Tokyo');
        const APP_LOCALE = document.documentElement?.lang ? (document.documentElement.lang === 'ja' ? 'ja-JP' : document.documentElement.lang) : 'ja-JP';
        const titleInput = document.querySelector('[name="Title"]');
        const startInput = document.querySelector('[name="StartDate"]');
        const previewTitle = document.getElementById('prevTitle');
        const previewTime = document.getElementById('prevTime');
        const previewDot = document.getElementById('prevDot');
        const colorInput = document.getElementById('evColor');
        const swatches = document.querySelectorAll('.swatches .sw');

        function formatTime(value) {
            if (!value) return '--:--';
            const raw = String(value);
            const match = raw.match(/T(\d{2}):(\d{2})/);
            if (match) return `${match[1]}:${match[2]}`;
            const dt = new Date(raw);
            if (Number.isNaN(dt.getTime())) return '--:--';
            return dt.toLocaleTimeString(APP_LOCALE, { timeZone: APP_TIMEZONE, hour: '2-digit', minute: '2-digit' });
        }

        function updatePreview() {
            if (previewTitle && titleInput) {
                previewTitle.textContent = titleInput.value || '(タイトル未設定)';
            }
            if (previewTime && startInput) {
                previewTime.textContent = formatTime(startInput.value);
            }
            if (previewDot && colorInput) {
                previewDot.style.background = colorInput.value || '#6366f1';
            }
        }

        [titleInput, startInput, colorInput].forEach(function (el) {
            if (!el) return;
            el.addEventListener('input', updatePreview);
            el.addEventListener('change', updatePreview);
        });

        swatches.forEach(function (btn) {
            btn.addEventListener('click', function () {
                if (!colorInput) return;
                const color = btn.getAttribute('data-c');
                colorInput.value = color;
                colorInput.dispatchEvent(new Event('input', { bubbles: true }));
            });
        });

        updatePreview();
    });
})();
