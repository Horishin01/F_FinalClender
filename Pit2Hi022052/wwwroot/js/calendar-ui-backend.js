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
                // 種別/ソース/優先度に応じてクラス付与（色分けに使用）
                const props = arg.event.extendedProps || {};
                const classes = [];
                const cat = (props.type || '').toString().toLowerCase();
                const src = (props.source || '').toString().toLowerCase();
                const prio = (props.priority || '').toString().toLowerCase();
                if (cat) classes.push('cat-' + cat);
                if (src) classes.push('src-' + src);
                if (prio) classes.push('prio-' + prio);
                if (arg.event.allDay) classes.push('all-day');
                return classes;
            },
            eventContent: function (arg) {
                const props = arg.event.extendedProps || {};
                const time = arg.timeText ? `<span class="ev-time">${arg.timeText}</span>` : '';
                const source = props.source ? `<span class="ev-badge ev-source">${props.source}</span>` : '';
                const prio = props.priority ? `<span class="ev-badge ev-prio">${props.priority}</span>` : '';
                return {
                    html: `
                        <div class="ev-row wrap">
                            ${time}<span class="ev-title">${arg.event.title}</span>${source}${prio}
                        </div>
                    `
                };
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
