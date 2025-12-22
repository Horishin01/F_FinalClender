// packlist.js
// Packlist ページのクライアント処理。サーバー埋め込みの JSON を読み取り、チェックリスト UI を構築・保存する。

(() => {
    const dataEl = document.getElementById('packlist-data');
    if (!dataEl) return;

    let seed = {};
    try {
        seed = JSON.parse(dataEl.textContent || '{}');
    } catch (err) {
        console.warn('Packlist JSON の読み込みに失敗しました。', err);
    }

    const today = seed.today || new Date().toISOString().slice(0, 10);
    const events = Array.isArray(seed.events) ? seed.events : [];
    const categories = Array.from(new Set(events.map(e => e.category || 'その他')));

    const presets = {
        Work: ['ノートPC', 'ACアダプタ', '名刺', 'ヘッドセット', '会議資料'],
        Study: ['ノート', 'タブレット / PC', '参考書', 'イヤホン', '飲み物'],
        Health: ['ウェア', 'タオル', '水筒', 'イヤホン', 'ロッカーキー'],
        Errand: ['財布', 'エコバッグ', 'メモリスト', '小銭 / IC カード'],
        Personal: ['財布', 'スマホ', '鍵', 'ハンカチ', 'モバイルバッテリー'],
        Meeting: ['議事メモ', '名刺', '資料プリント', 'ペン', '水']
    };
    const baseEssentials = ['財布', 'スマホ', '鍵'];

    const dateLabel = document.getElementById('packlistDateLabel');
    if (dateLabel) {
        const parsed = new Date(today.replace(/-/g, '/'));
        dateLabel.textContent = !isNaN(parsed.getTime())
            ? parsed.toLocaleDateString('ja-JP', { month: 'long', day: 'numeric', weekday: 'short' })
            : today;
    }

    renderEvents(events);
    renderChecklist(categories.length ? categories : ['Personal']);
    hookReset(categories.length ? categories : ['Personal']);

    function storageKey(category) {
        return `packlist:v1:${today}:${category}`;
    }

    function loadState(category) {
        try {
            const raw = localStorage.getItem(storageKey(category));
            return raw ? new Set(JSON.parse(raw)) : new Set();
        } catch {
            return new Set();
        }
    }

    function saveState(category, checkedSet) {
        try {
            localStorage.setItem(storageKey(category), JSON.stringify([...checkedSet]));
        } catch {
            // ignore storage errors (e.g. private mode)
        }
    }

    function renderEvents(items) {
        const list = document.getElementById('packlistEventList');
        if (!list) return;
        list.innerHTML = '';

        if (!items.length) {
            const li = document.createElement('li');
            li.className = 'packlist-event';
            li.textContent = '今日の予定がありません。ミニマムセットだけ表示します。';
            list.appendChild(li);
            return;
        }

        items.forEach(ev => {
            const li = document.createElement('li');
            li.className = 'packlist-event';

            const time = document.createElement('div');
            time.className = 'packlist-time';
            time.textContent = ev.time || '--:--';

            const text = document.createElement('div');
            text.className = 'packlist-event-text';

            const title = document.createElement('div');
            title.className = 'packlist-event-title';
            title.textContent = ev.title || '無題の予定';

            const cat = document.createElement('span');
            cat.className = 'packlist-event-cat';
            cat.textContent = ev.category || 'その他';

            text.appendChild(title);
            text.appendChild(cat);

            li.appendChild(time);
            li.appendChild(text);
            list.appendChild(li);
        });
    }

    function renderChecklist(categoryList) {
        const container = document.getElementById('packlistChecklist');
        if (!container) return;

        container.innerHTML = '';

        categoryList.forEach(category => {
            const items = buildItems(category);
            const checked = loadState(category);

            const card = document.createElement('div');
            card.className = 'packlist-check-card';

            const head = document.createElement('div');
            head.className = 'packlist-check-head';

            const title = document.createElement('div');
            title.className = 'packlist-check-title';
            title.innerHTML = `<span class="badge">${category}</span><span>プリセット</span>`;

            const counter = document.createElement('span');
            counter.className = 'packlist-check-counter badge';
            head.appendChild(title);
            head.appendChild(counter);

            const list = document.createElement('div');
            list.className = 'packlist-checklist';

            items.forEach(label => {
                const row = document.createElement('label');
                row.className = 'packlist-checkitem';

                const input = document.createElement('input');
                input.type = 'checkbox';
                input.checked = checked.has(label);

                input.addEventListener('change', () => {
                    if (input.checked) {
                        checked.add(label);
                    } else {
                        checked.delete(label);
                    }
                    saveState(category, checked);
                    updateCounter();
                });

                const text = document.createElement('span');
                text.textContent = label;

                row.appendChild(input);
                row.appendChild(text);
                list.appendChild(row);
            });

            card.appendChild(head);
            card.appendChild(list);
            container.appendChild(card);
            updateCounter();

            function updateCounter() {
                counter.textContent = `${checked.size}/${items.length}`;
            }
        });
    }

    function buildItems(category) {
        const preset = presets[category] || [];
        const merged = [...baseEssentials, ...preset];
        return Array.from(new Set(merged));
    }

    function hookReset(categoryList) {
        const resetBtn = document.getElementById('packlistResetDay');
        if (!resetBtn) return;
        resetBtn.addEventListener('click', () => {
            categoryList.forEach(cat => {
                try {
                    localStorage.removeItem(storageKey(cat));
                } catch {
                    // ignore
                }
            });
            renderChecklist(categoryList);
        });
    }
})();
