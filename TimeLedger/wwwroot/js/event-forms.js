// event-forms.js
// イベント作成/編集フォームのプレビューやカラー選択、繰り返し/終日切替を UI で補助するスクリプト。

﻿(function () {
    document.addEventListener('DOMContentLoaded', function () {
        const titleInput = document.querySelector('[name="Title"]');
        const startInput = document.querySelector('[name="StartDate"]');
        const previewTitle = document.getElementById('prevTitle');
        const previewTime = document.getElementById('prevTime');
        const previewDot = document.getElementById('prevDot');
        const colorInput = document.getElementById('evColor');
        const swatches = document.querySelectorAll('.swatches .sw');

        function pad(n) {
            return ('0' + n).slice(-2);
        }

        function formatTime(value) {
            if (!value) return '--:--';
            const dt = new Date(value);
            if (Number.isNaN(dt.getTime())) return '--:--';
            return pad(dt.getHours()) + ':' + pad(dt.getMinutes());
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
