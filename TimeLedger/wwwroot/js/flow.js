(function () {
    const STORAGE_KEY = 'pit2hi-flow-page';
    const SUGGESTIONS = [
        { text: 'スマホ開く前に深呼吸', context: 'プライベート' },
        { text: '水を飲む', context: '健康' },
        { text: 'ベッドメイキング', context: 'プライベート' },
        { text: '歯磨き', context: '健康' },
        { text: 'カーテンを開けた', context: 'プライベート' },
        { text: '今から集中タイム開始', context: '仕事' },
        { text: '1on1の準備メモを書く', context: '仕事' }
    ];
    const DEFAULT_CONTEXTS = ['仕事', 'プライベート', '健康', '学び', 'メモ'];

    document.addEventListener('DOMContentLoaded', () => {
        const els = {
            input: document.getElementById('flowInput'),
            context: document.getElementById('flowContext'),
            add: document.getElementById('btnAddFlow'),
            list: document.getElementById('flowList'),
            suggestions: document.getElementById('flowSuggestions'),
            countToday: document.getElementById('flowCountToday'),
            countWeek: document.getElementById('flowCountWeek'),
            streak: document.getElementById('flowStreak'),
            heroCount: document.getElementById('flowHeroCount'),
            heroStreak: document.getElementById('flowHeroStreak'),
            filterButtons: document.querySelectorAll('[data-filter]'),
            filterContext: document.getElementById('flowFilterContext'),
            search: document.getElementById('flowSearch'),
            reset: document.getElementById('flowResetData')
        };

        const state = {
            entries: loadEntries(),
            filter: 'today',
            context: 'all',
            search: ''
        };

        const addEntry = (text, context) => {
            if (!text) return;
            state.entries.unshift({
                id: uid(),
                text,
                context: context || 'メモ',
                createdAt: new Date().toISOString()
            });
            persistEntries(state.entries);
            syncContextOptions();
            render();
        };

        const removeEntry = (id) => {
            state.entries = state.entries.filter(e => e.id !== id);
            persistEntries(state.entries);
            syncContextOptions();
            render();
        };

        els.add?.addEventListener('click', () => {
            const text = (els.input?.value || '').trim();
            if (!text) return;
            addEntry(text, els.context?.value || 'メモ');
            if (els.input) {
                els.input.value = '';
                els.input.focus();
            }
        });

        els.input?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                els.add?.click();
            }
        });

        els.filterButtons?.forEach(btn => {
            btn.addEventListener('click', () => {
                state.filter = btn.dataset.filter || 'today';
                setActiveFilterButtons();
                render();
            });
        });

        els.filterContext?.addEventListener('change', (event) => {
            state.context = event.target.value || 'all';
            render();
        });

        els.search?.addEventListener('input', (event) => {
            state.search = (event.target.value || '').trim().toLowerCase();
            render();
        });

        els.reset?.addEventListener('click', () => {
            state.entries = generateSampleData();
            persistEntries(state.entries);
            state.filter = 'today';
            state.context = 'all';
            state.search = '';
            syncContextOptions();
            resetControls();
            render();
        });

        renderSuggestions(els.suggestions, addEntry);
        syncContextOptions();
        setActiveFilterButtons();
        render();

        function render() {
            const filtered = filterEntries();
            const todayKey = dateKey(new Date());

            if (els.list) {
                els.list.innerHTML = '';
                if (!filtered.length) {
                    const empty = document.createElement('li');
                    empty.className = 'flow-empty';
                    empty.textContent = 'まだ記録がありません。提案チップから試してみてください。';
                    els.list.appendChild(empty);
                } else {
                    let currentDay = '';
                    filtered.forEach(entry => {
                        const created = safeDate(entry.createdAt);
                        const dayKey = dateKey(created);
                        if (dayKey !== currentDay) {
                            const dayLi = document.createElement('li');
                            dayLi.className = 'flow-day';
                            dayLi.textContent = dayKey === todayKey ? '今日' : formatDay(created);
                            els.list.appendChild(dayLi);
                            currentDay = dayKey;
                        }

                        const li = document.createElement('li');
                        li.className = 'flow-entry';
                        const dayLabel = dayKey === todayKey ? '今日' : formatDay(created);
                        li.innerHTML = `
                            <div class="flow-entry-main">
                                <div class="flow-text">${escapeHtml(entry.text)}</div>
                                <div class="flow-meta">
                                    <span class="flow-context">${escapeHtml(entry.context)}</span>
                                    <span>${dayLabel} ${formatTime(created)}</span>
                                </div>
                            </div>
                            <button type="button" class="flow-delete" data-remove="${entry.id}" aria-label="この記録を削除">削除</button>
                        `;
                        const removeBtn = li.querySelector('[data-remove]');
                        removeBtn?.addEventListener('click', () => removeEntry(entry.id));
                        els.list.appendChild(li);
                    });
                }
            }

            const stats = summarize(state.entries);
            setText(els.countToday, stats.today);
            setText(els.countWeek, stats.week);
            setText(els.streak, stats.streak);
            setText(els.heroCount, `${stats.today} 件`);
            setText(els.heroStreak, `連続 ${stats.streak} 日目`);
        }

        function filterEntries() {
            const todayKey = dateKey(new Date());
            return state.entries
                .filter(e => state.filter === 'all' || dateKey(e.createdAt) === todayKey)
                .filter(e => state.context === 'all' || (e.context || 'メモ') === state.context)
                .filter(e => {
                    if (!state.search) return true;
                    const hay = `${e.text} ${e.context || ''}`.toLowerCase();
                    return hay.includes(state.search);
                })
                .slice()
                .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
        }

        function syncContextOptions() {
            if (!els.filterContext) return;
            const contexts = Array.from(new Set([
                ...DEFAULT_CONTEXTS,
                ...SUGGESTIONS.map(s => s.context),
                ...state.entries.map(e => e.context || 'メモ')
            ]));
            const previous = state.context;
            els.filterContext.innerHTML = '<option value="all">すべてのコンテキスト</option>';
            contexts.sort().forEach(ctx => {
                const option = document.createElement('option');
                option.value = ctx;
                option.textContent = ctx;
                els.filterContext.appendChild(option);
            });
            if (previous && contexts.includes(previous)) {
                els.filterContext.value = previous;
            } else {
                els.filterContext.value = 'all';
                state.context = 'all';
            }
        }

        function setActiveFilterButtons() {
            els.filterButtons?.forEach(btn => {
                const isActive = (btn.dataset.filter || 'today') === state.filter;
                if (isActive) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        }

        function resetControls() {
            setActiveFilterButtons();
            if (els.search) els.search.value = '';
            if (els.filterContext) els.filterContext.value = 'all';
        }
    });

    function renderSuggestions(container, addEntry) {
        if (!container) return;
        container.innerHTML = '';
        SUGGESTIONS.forEach(item => {
            const chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'idea-chip';
            chip.textContent = item.text;
            chip.dataset.context = item.context;
            chip.addEventListener('click', () => addEntry(item.text, item.context));
            container.appendChild(chip);
        });
    }

    function summarize(entries) {
        const todayKey = dateKey(new Date());
        const weekAgo = startOfDay(new Date());
        weekAgo.setDate(weekAgo.getDate() - 6);
        const weekThreshold = weekAgo.getTime();
        const today = entries.filter(e => dateKey(e.createdAt) === todayKey).length;
        const week = entries.filter(e => startOfDay(e.createdAt).getTime() >= weekThreshold).length;
        return { today, week, streak: calcStreak(entries) };
    }

    function calcStreak(entries) {
        if (!Array.isArray(entries) || !entries.length) return 0;
        const keys = new Set(entries.map(e => dateKey(e.createdAt)));
        let streak = 0;
        const cursor = startOfDay(new Date());
        while (keys.has(dateKey(cursor))) {
            streak += 1;
            cursor.setDate(cursor.getDate() - 1);
        }
        return streak;
    }

    function loadEntries() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (raw) {
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed) && parsed.length > 0) return parsed;
            }
        } catch (err) {
            console.warn('flow: storage unavailable', err);
        }
        // No valid data found, return generated samples
        return generateSampleData();
    }

    function generateSampleData() {
        const samples = [];
        const add = (text, context, daysAgo = 0) => {
            const d = new Date();
            d.setDate(d.getDate() - daysAgo);
            d.setHours(d.getHours() - Math.random() * 5, Math.random() * 60, Math.random() * 60);
            samples.push({ id: uid(), text, context, createdAt: d.toISOString() });
        };

        // Today's entries
        add('最初のFlowを記録した', '仕事', 0);
        add('UI改善のアイデアをメモ', '仕事', 0);
        add('コーヒーを淹れて一息', 'プライベート', 0);
        add('15分の散歩', '健康', 0);

        // Yesterday's entries
        add('昨日やったことのレビュー', '仕事', 1);
        add('新しいCSSプロパティを試した', '仕事', 1);
        add('部屋の掃除', 'プライベート', 1);

        // Two days ago
        add('DB設計のドキュメント作成', '仕事', 2);
        add('定期的なバックアップを確認', '仕事', 2);

        // A week ago
        add('来週の計画を立てる', '仕事', 7);
        add('新しいライブラリの調査', 'プライベート', 7);

        return samples.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
    }

    function persistEntries(entries) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
        } catch {
            /* noop */
        }
    }

    function startOfDay(date) {
        const d = new Date(date);
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function dateKey(value) {
        const d = startOfDay(value);
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    function formatTime(date) {
        const h = String(date.getHours()).padStart(2, '0');
        const m = String(date.getMinutes()).padStart(2, '0');
        return `${h}:${m}`;
    }

    function formatDay(date) {
        return `${String(date.getMonth() + 1).padStart(2, '0')}/${String(date.getDate()).padStart(2, '0')}`;
    }

    function safeDate(input) {
        const d = new Date(input);
        if (Number.isNaN(d.getTime())) return new Date();
        return d;
    }

    function uid() {
        if (typeof crypto !== 'undefined' && crypto.randomUUID) return crypto.randomUUID();
        return 'flow-' + Math.random().toString(36).slice(2, 9);
    }

    function setText(el, value) {
        if (el) el.textContent = value;
    }

    function escapeHtml(str) {
        return (str || '').replace(/[&<>"']/g, (c) => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c] || c));
    }
})();
