// flow.js
// Flow デモページ用。入力済みフローを localStorage に保存・復元し、簡易的な提案チップやスタッツを描画する。

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
    const CONTEXT_PRESETS = [
        { value: '仕事', label: '仕事', color: '#2563eb', hint: '集中タイム開始' },
        { value: '健康', label: '健康', color: '#16a34a', hint: '水を飲んだ/歩いた' },
        { value: 'プライベート', label: '暮らし', color: '#f97316', hint: '家事/ひと息' },
        { value: '学び', label: '学び', color: '#8b5cf6', hint: '読んだ/試した' },
        { value: 'メモ', label: 'メモ', color: '#0f172a', hint: '気づき/メモ' }
    ];
    const DEFAULT_CONTEXTS = ['仕事', 'プライベート', '健康', '学び', 'メモ'];
    const TODAY_GOAL = 5;

    const CONTEXT_COLOR_MAP = CONTEXT_PRESETS.reduce((acc, cur) => {
        acc[cur.value] = cur.color;
        return acc;
    }, {});

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
            reset: document.getElementById('flowResetData'),
            contextChips: document.getElementById('flowContextChips'),
            progressValue: document.getElementById('flowProgressValue'),
            progressLabel: document.getElementById('flowProgressLabel'),
            topContexts: document.getElementById('flowTopContexts'),
            heatBars: document.getElementById('flowHeatBars')
        };

        const state = {
            entries: loadEntries(),
            filter: 'today',
            context: 'all',
            search: '',
            lastContext: '仕事'
        };
        if (els.context?.value) state.lastContext = els.context.value;

        const addEntry = (text, context) => {
            if (!text) return;
            const ctx = context || state.lastContext || 'メモ';
            state.entries.unshift({
                id: uid(),
                text,
                context: ctx,
                createdAt: new Date().toISOString()
            });
            state.lastContext = ctx;
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
            state.lastContext = '仕事';
            syncContextOptions();
            resetControls();
            render();
        });

        renderSuggestions(els.suggestions, addEntry);
        renderContextChips();
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
                        const color = CONTEXT_COLOR_MAP[entry.context] || '#475569';
                        li.innerHTML = `
                            <div class="flow-entry-main">
                                <div class="flow-text">${escapeHtml(entry.text)}</div>
                                <div class="flow-entry-meta">
                                    <span class="flow-badge" style="color:${color}"><span class="dot"></span>${escapeHtml(entry.context)}</span>
                                    <span class="flow-time">${dayLabel} ${formatTime(created)}</span>
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
            updateProgress(stats.today);
            renderInsights(state.entries);
        }

        function filterEntries() {
            const todayKey = dateKey(new Date());
            const weekStart = startOfWeek(new Date());
            const weekEnd = startOfDay(new Date());
            weekEnd.setDate(weekEnd.getDate() + 1);
            return state.entries
                .filter(e => {
                    if (state.filter === 'all') return true;
                    if (state.filter === 'today') return dateKey(e.createdAt) === todayKey;
                    if (state.filter === 'week') {
                        const d = startOfDay(e.createdAt);
                        return d >= weekStart && d < weekEnd;
                    }
                    return true;
                })
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
            chip.addEventListener('mouseenter', () => {
                if (typeof item.context === 'string' && container?.dataset?.prefill !== 'off' && typeof document !== 'undefined') {
                    const input = document.getElementById('flowInput');
                    if (input && !input.value) input.placeholder = `例: ${item.text}`;
                }
            });
            container.appendChild(chip);
        });
    }

    function renderContextChips() {
        const container = document.getElementById('flowContextChips');
        const input = document.getElementById('flowInput');
        const select = document.getElementById('flowContext');
        if (!container || !select) return;
        container.innerHTML = '';
        CONTEXT_PRESETS.forEach(preset => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'flow-context-chip';
            btn.innerHTML = `<span class="dot" style="color:${preset.color}"></span><span>${preset.label}</span>`;
            btn.addEventListener('click', () => {
                select.value = preset.value;
                state.lastContext = preset.value;
                if (input) {
                    if (!input.value) input.value = preset.hint || '';
                    input.focus();
                    input.selectionStart = input.value.length;
                    input.selectionEnd = input.value.length;
                }
            });
            container.appendChild(btn);
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

    function startOfWeek(date) {
        const d = startOfDay(date);
        const diff = d.getDay(); // Sun=0
        d.setDate(d.getDate() - diff);
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

    function updateProgress(todayCount) {
        const bar = document.getElementById('flowProgressValue');
        const label = document.getElementById('flowProgressLabel');
        const ratio = Math.max(0, Math.min(1, todayCount / TODAY_GOAL));
        if (bar) bar.style.width = `${ratio * 100}%`;
        if (label) label.textContent = `${todayCount}/${TODAY_GOAL}`;
    }

    function renderInsights(entries) {
        renderTopContexts(entries);
        renderHeatBars(entries);
    }

    function renderTopContexts(entries) {
        const container = document.getElementById('flowTopContexts');
        if (!container) return;
        container.innerHTML = '';
        const threshold = startOfDay(new Date());
        threshold.setDate(threshold.getDate() - 6);
        const recent = entries.filter(e => startOfDay(e.createdAt) >= threshold);
        if (!recent.length) {
            container.innerHTML = '<li class="flow-empty">まだ記録がありません。</li>';
            return;
        }
        const counts = {};
        recent.forEach(e => {
            const key = e.context || 'メモ';
            counts[key] = (counts[key] || 0) + 1;
        });
        const sorted = Object.entries(counts).sort((a, b) => b[1] - a[1]).slice(0, 3);
        sorted.forEach(([ctx, count]) => {
            const li = document.createElement('li');
            li.innerHTML = `
                <span class="label"><span class="dot" style="color:${CONTEXT_COLOR_MAP[ctx] || '#475569'}"></span>${escapeHtml(ctx)}</span>
                <span class="count">${count}</span>
            `;
            container.appendChild(li);
        });
    }

    function renderHeatBars(entries) {
        const wrap = document.getElementById('flowHeatBars');
        if (!wrap) return;
        wrap.innerHTML = '';
        const today = startOfDay(new Date());
        const days = [];
        for (let i = 6; i >= 0; i--) {
            const d = new Date(today);
            d.setDate(d.getDate() - i);
            days.push(d);
        }
        const max = Math.max(1, ...days.map(d => entries.filter(e => dateKey(e.createdAt) === dateKey(d)).length));
        let hasData = false;
        days.forEach(d => {
            const key = dateKey(d);
            const count = entries.filter(e => dateKey(e.createdAt) === key).length;
            if (count > 0) hasData = true;
            const row = document.createElement('div');
            row.className = 'heat-row';
            row.innerHTML = `
                <span class="heat-label">${key.slice(5)}</span>
                <div class="heat-bar"><span style="width:${Math.min(1, count / max) * 100}%"></span></div>
                <span class="heat-count">${count}</span>
            `;
            wrap.appendChild(row);
        });
        if (!hasData) {
            wrap.innerHTML = '<p class="annotation">直近7日の記録がありません。</p>';
        }
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
