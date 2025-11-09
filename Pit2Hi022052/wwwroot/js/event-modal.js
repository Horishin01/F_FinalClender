/*
[Script] イベント作成/閲覧モーダル（UIのみ）
[目的] +/日付クリックで開くモーダルの編集/閲覧切替、色プリセット＋プレビュー、保存でカレンダーへ即時反映。
[流れ]
  1) openCreateModal(date) で初期値セット→編集モード→表示
  2) openEventView(event) で既存イベントを閲覧モードで表示
  3) 色: プリセット/カラーピッカー→プレビュー(左アクセント＋淡色背景)に反映
  4) 保存: window.calendar.addEvent(...)（バックエンド通信なし）
*/
(() => {
    const modal = document.getElementById('eventModal');
    const fab = document.getElementById('fab');
    if (!modal) return;

    const emTitle = document.getElementById('emTitle');
    const emDate = document.getElementById('emDate');
    const emStart = document.getElementById('emStart');
    const emEnd = document.getElementById('emEnd');
    const emType = document.getElementById('emType');
    const emDesc = document.getElementById('emDesc');
    const emColor = document.getElementById('emColor');
    const colorSwatches = document.getElementById('colorSwatches');
    const colorPreview = document.getElementById('colorPreview');
    const btnEdit = document.getElementById('btnModalEdit');
    const btnSave = document.getElementById('btnModalSave');
    const btnClose = document.getElementById('btnModalClose');

    let mode = 'view';
    const pad2 = n => String(n).padStart(2, '0');
    const toDate = d => `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
    const toTime = d => `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;

    function setMode(m) {
        mode = m;
        const ro = m === 'view';
        [emTitle, emDate, emStart, emEnd, emType, emDesc, emColor].forEach(el => el && (el.disabled = ro));
        if (btnSave) btnSave.disabled = ro;
        if (btnEdit) btnEdit.textContent = ro ? '編集' : '閲覧に戻す';
    }
    function setColor(hex) {
        if (emColor) emColor.value = hex;
        if (colorPreview) {
            colorPreview.style.setProperty('--evc', hex);
            colorPreview.style.setProperty('--evbg', (window.hexToRgba ? window.hexToRgba(hex, 0.12) : 'rgba(66,153,225,.12)'));
            colorPreview.style.color = hex;
        }
    }

    // プリセット色
    const presets = ['#e53e3e', '#d69e2e', '#4299e1', '#2d7d32', '#6f42c1', '#ef4444', '#10b981'];
    if (colorSwatches) {
        colorSwatches.innerHTML = presets.map(c => `<button type='button' data-c='${c}' title='${c}' style="background:${c}"></button>`).join('');
        colorSwatches.addEventListener('click', (e) => { const b = e.target.closest('button[data-c]'); if (!b) return; setColor(b.dataset.c); });
    }
    emColor && emColor.addEventListener('input', () => setColor(emColor.value));
    emType && emType.addEventListener('change', () => setColor((window.typeColor || {})[emType.value] || '#4299e1'));

    // 公開API
    window.openCreateModal = (d) => {
        const now = d || new Date();
        emTitle.value = ''; emDate.value = toDate(now);
        const sh = now.getHours() + 1, eh = sh + 1;
        emStart.value = `${pad2(sh % 24)}:00`; emEnd.value = `${pad2(eh % 24)}:00`;
        emType.value = 'meeting'; setColor((window.typeColor || {})['meeting'] || '#4299e1');
        emDesc.value = ''; setMode('edit'); modal.classList.remove('d-none');
    };
    window.openEventView = (ev) => {
        if (!ev) return;
        const s = ev.start, e = ev.end || new Date(s.getTime() + 60 * 60 * 1000);
        emTitle.value = ev.title || ''; emDate.value = toDate(s);
        emStart.value = toTime(s); emEnd.value = toTime(e);
        emType.value = ev.extendedProps?.type || 'meeting';
        setColor(ev.extendedProps?.uiColor || (window.typeColor || {})[emType.value] || '#4299e1');
        emDesc.value = ''; setMode('view'); modal.classList.remove('d-none');
    };

    // 操作
    fab && fab.addEventListener('click', () => window.openCreateModal(new Date()));
    btnEdit && btnEdit.addEventListener('click', () => setMode(mode === 'view' ? 'edit' : 'view'));
    btnClose && btnClose.addEventListener('click', () => modal.classList.add('d-none'));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !modal.classList.contains('d-none')) modal.classList.add('d-none'); });

    // 保存: UIのみ（カレンダーへ即反映）
    btnSave && btnSave.addEventListener('click', (e) => {
        e.preventDefault();
        const cal = window.calendar; if (!cal) return;
        const title = emTitle?.value || '新規イベント';
        const start = new Date(`${emDate?.value}T${emStart?.value}`);
        const end = new Date(`${emDate?.value}T${emEnd?.value}`);
        const hex = emColor?.value || '#4299e1';
        cal.addEvent({ title, start, end, extendedProps: { type: emType?.value || 'meeting', uiColor: hex } });
        modal.classList.add('d-none');
    });
})();
