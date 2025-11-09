/*
[Script] FullCalendar初期化＋外部ヘッダー連動
[目的] 月/週/日ビューの切替と0–23時表示、イベントのブランド配色、初回高さ0対策を担う。
[流れ]
  1) ダミーイベントを変換→calendar作成→render→updateSize
  2) eventContent/DidMountで「左アクセント＋淡色背景」チップ描画
  3) 外部ボタン(prev/next/today/view)と連動、期間ラベル同期
*/
(() => {
    const calendarEl = document.getElementById('fc');
    const periodEl = document.getElementById('calPeriod');
    if (!calendarEl) return;

    // 種別→色
    window.typeColor = { meeting: '#e53e3e', personal: '#2d7d32', work: '#4299e1', deadline: '#d69e2e' };
    // HEX→rgba
    window.hexToRgba = (hex, a) => { const m = hex.replace('#', ''); const r = parseInt(m.slice(0, 2), 16), g = parseInt(m.slice(2, 4), 16), b = parseInt(m.slice(4, 6), 16); return `rgba(${r},${g},${b},${a})`; };

    // ダミーイベント
    const sample = [
        { date: '2025-09-14', start: '09:00', end: '10:00', title: '作品発表 ぜミB', type: 'meeting' },
        { date: '2025-09-14', start: '11:00', end: '12:00', title: '英会話', type: 'personal' },
        { date: '2025-09-14', start: '16:30', end: '17:30', title: '美容院', type: 'personal' },
        { date: '2025-09-16', start: '14:00', end: '15:00', title: '研究室', type: 'work' },
        { date: '2025-09-17', start: '13:00', end: '14:00', title: 'プロシップ説明会', type: 'meeting' },
        { date: '2025-09-26', start: '16:00', end: '17:00', title: 'バイト', type: 'work' },
        { date: '2025-09-25', start: '14:00', end: '15:00', title: '中間発表', type: 'meeting' }
    ];
    const fcEvents = sample.map((e, i) => ({
        id: String(i + 1),
        title: e.title,
        start: `${e.date}T${e.start}`,
        end: `${e.date}T${e.end}`,
        extendedProps: { type: e.type, uiColor: (window.typeColor || {})[e.type] || '#4299e1' }
    }));

    const cal = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        locale: 'ja',
        weekNumberCalculation: 'local',
        height: 'parent', expandRows: true,
        headerToolbar: false,
        dayMaxEvents: true,
        selectable: true, selectMirror: true,
        buttonText: { today: '今日', month: '月', week: '週', day: '日' },
        slotMinTime: '00:00:00', slotMaxTime: '24:00:00',
        events: fcEvents,
        eventContent: (info) => {
            const t = info.timeText ? `<span class="t">${info.timeText}</span>` : '';
            return { html: `<div class="evchip">${t}${info.event.title}</div>` };
        },
        eventDidMount: (info) => {
            const hex = info.event.extendedProps.uiColor || (window.typeColor || {})[info.event.extendedProps.type] || '#4299e1';
            info.el.style.setProperty('--evc', hex);
            info.el.style.setProperty('--evbg', window.hexToRgba(hex, 0.12));
            info.el.style.color = hex;
        },
        datesSet: () => { if (periodEl) periodEl.textContent = cal.view.title; syncViewButtons(); },
        dateClick: (info) => { if (window.openCreateModal) window.openCreateModal(info.date); },
        eventClick: (info) => { if (window.openEventView) window.openEventView(info.event); }
    });

    cal.render();
    cal.updateSize(); setTimeout(() => cal.updateSize(), 0);
    window.addEventListener('resize', () => cal.updateSize());

    function syncViewButtons() {
        const v = cal.view.type, map = { dayGridMonth: 'viewMonth', timeGridWeek: 'viewWeek', timeGridDay: 'viewDay' };
        ['viewMonth', 'viewWeek', 'viewDay'].forEach(id => { const el = document.getElementById(id); if (el) el.classList.remove('active'); });
        const id = map[v]; const active = document.getElementById(id); if (active) active.classList.add('active');
    }
    const $ = (id) => document.getElementById(id);
    $('calPrev') && ($('calPrev').onclick = () => cal.prev());
    $('calNext') && ($('calNext').onclick = () => cal.next());
    $('calToday') && ($('calToday').onclick = () => cal.today());
    $('viewMonth') && ($('viewMonth').onclick = () => cal.changeView('dayGridMonth'));
    $('viewWeek') && ($('viewWeek').onclick = () => cal.changeView('timeGridWeek'));
    $('viewDay') && ($('viewDay').onclick = () => cal.changeView('timeGridDay'));
    $('calSync') && ($('calSync').onclick = () => alert('CalDAV/DB同期（ダミー）'));

    window.calendar = cal;
})();
