(function () {
    // 共通ユーティリティ
    const $ = (s, r = document) => r.querySelector(s);
    const pad = n => String(n).padStart(2, "0");
    const hhmm = v => {
        if (!v) return "";
        const d = new Date(v);
        if (!isNaN(d.getTime())) return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
        // フォームの文字列(yyyy-MM-ddTHH:mm 等)にも対応
        const m = String(v).match(/T?(\d{2}):(\d{2})/);
        return m ? `${m[1]}:${m[2]}` : String(v);
    };

    // Create/Edit のプレビュー連動
    function bindPreview() {
        const title = document.querySelector("[name='Title']");
        const start = document.querySelector("[name='Start']");
        const end = document.querySelector("[name='End']");
        const prevTitle = $("#prevTitle");
        const prevTime = $("#prevTime");
        const prevDot = $("#prevDot");
        if (!prevTitle || !title) return; // 詳細画面などはスキップ

        const update = () => {
            prevTitle.textContent = title.value || "件名";
            const s = hhmm(start && start.value);
            const e = hhmm(end && end.value);
            prevTime.textContent = s ? (e ? `${s}–${e}` : s) : "00:00";
        };
        ["input", "change"].forEach(ev => {
            title && title.addEventListener(ev, update);
            start && start.addEventListener(ev, update);
            end && end.addEventListener(ev, update);
        });
        update();

        // 色（フロントのみ）
        const picker = $("#evColor");
        const setColor = c => { if (prevDot) prevDot.style.background = c; if (picker) picker.value = c; };
        if (picker) picker.addEventListener("input", e => setColor(e.target.value));
        document.querySelectorAll(".sw").forEach(b => b.addEventListener("click", () => setColor(b.dataset.c)));
    }

    // Details の概要行ビルド
    window.__buildDetailsSummary = function (opt) {
        const timeEl = document.getElementById(opt.timeId);
        const dotEl = document.getElementById(opt.dotId);
        if (timeEl) {
            const s = hhmm(opt.start);
            const e = hhmm(opt.end);
            timeEl.textContent = s ? (e ? `${s}–${e}` : s) : "";
        }
        if (dotEl) dotEl.style.background = "#3b82f6";
    };

    document.addEventListener("DOMContentLoaded", bindPreview);
})();
