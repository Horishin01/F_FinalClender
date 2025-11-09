/*!
 * calendar-ui-backend.js
 * 目的: FullCalendar を “バックエンド連携版(UI固定)” で初期化
 * 流れ:
 *  1) DOMContentLoaded → FullCalendar生成（height:auto, contentHeight:'auto', expandRows:true）
 *  2) 期間ラベル/ビュー切替/前後・今日のナビを外部UIにバインド
 *  3) /Events/GetEvents から予定取得（既存コントローラ）
 *  4) クリック: 日付→Create へ遷移, 予定→Details へ遷移
 *  5) 同期ボタン→ /Events/Sync POST → 再読込
 */

(function () {
    function $(s, r = document) { return r.querySelector(s); }

    document.addEventListener('DOMContentLoaded', function () {
        const el = $('#calendar');
        const period = $('#calPeriod');

        // FullCalendar 本体
        const cal = new FullCalendar.Calendar(el, {
            initialView: 'dayGridMonth',
            headerToolbar: false,          // 内蔵ヘッダは使わない
            locale: 'ja',
            timeZone: 'Asia/Tokyo',
            buttonText: { today: '今日', month: '月', week: '週', day: '日' },

            // ← “親の高さ”に依存しないよう auto を採用
            height: 'auto',
            contentHeight: 'auto',
            expandRows: true,
            dayMaxEvents: true,

            slotMinTime: '00:00:00',
            slotMaxTime: '24:00:00',

            events: '/Events/GetEvents',   // 既存のバックエンド
            eventClassNames: function (arg) {
                // 種別（Descriptionや拡張プロパティにあれば拾う / 無ければ空）
                const t = (arg.event.extendedProps?.type || '').toString().toLowerCase();
                if (t === 'meeting' || t === 'work' || t === 'personal') return [t];
                return [];
            },

            // UI 同期
            datesSet(info) { period.textContent = cal.view.title; },

            // クリック挙動
            dateClick(info) {
                const start = info.dateStr;
                // 1時間枠を仮で付与
                const end = new Date(info.date.getTime() + 60 * 60 * 1000).toISOString();
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            },
            eventClick(info) {
                const id = info.event.id;
                if (id) window.location.href = `/Events/Details?id=${encodeURIComponent(id)}`;
            }
        });

        cal.render(); // ← レンダリング

        // 外部UIバインド
        $('#calPrev').onclick = () => cal.prev();
        $('#calNext').onclick = () => cal.next();
        $('#calToday').onclick = () => cal.today();

        const viewBtns = { viewMonth: 'dayGridMonth', viewWeek: 'timeGridWeek', viewDay: 'timeGridDay' };
        Object.entries(viewBtns).forEach(([id, v]) => {
            const btn = $('#' + id);
            btn.onclick = () => {
                cal.changeView(v);
                // active 表示
                Object.keys(viewBtns).forEach(k => $('#' + k).classList.remove('active'));
                btn.classList.add('active');
            };
        });

        // 同期
        const syncBtn = $('#syncBtn');
        if (syncBtn) {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            syncBtn.addEventListener('click', async () => {
                syncBtn.disabled = true;
                try {
                    const res = await fetch('/Events/Sync', {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': token }
                    });
                    if (res.status === 429) { alert('同期は60秒に1回までです。'); }
                    else if (res.ok) {
                        const r = await res.json().catch(() => ({ saved: '?', scanned: '?' }));
                        alert(`同期完了: 保存 ${r.saved} 件 / 取得 ${r.scanned} 件`);
                        cal.refetchEvents();
                    } else {
                        alert('同期に失敗しました。');
                    }
                } catch (e) {
                    alert('同期でエラーが発生しました。');
                } finally {
                    syncBtn.disabled = false;
                }
            });
        }

        // FAB: 新規モーダルの代わりに Create へ
        const fab = $('#fab');
        if (fab) {
            fab.addEventListener('click', (e) => {
                e.preventDefault();
                const now = new Date();
                const end = new Date(now.getTime() + 60 * 60 * 1000);
                window.location.href = `/Events/Create?startDate=${now.toISOString()}&endDate=${end.toISOString()}`;
            });
        }
    });
})();
