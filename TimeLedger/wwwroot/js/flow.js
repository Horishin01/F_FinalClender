// flow.js
// Flow リストのフロント専用機能。localStorage へ保存し、フィルタや統計を描画する。

(function () {
    const STORAGE_KEY = 'timeledger_flows';
    const LEGACY_STORAGE_KEY = 'pit2hi-flow-page';
    const CONTEXT_PRESETS = [
        { value: '仕事', label: '仕事', color: '#2563eb', hint: '集中タイム開始' },
        { value: '健康', label: '健康', color: '#16a34a', hint: '水を飲んだ・歩いた' },
        { value: 'プライベート', label: '暮らし', color: '#f97316', hint: '家事・ひと息' },
        { value: '学び', label: '学び', color: '#8b5cf6', hint: '読んだ・試した' },
        { value: 'メモ', label: 'メモ', color: '#0f172a', hint: '気づき・メモ' }
    ];
    const DEFAULT_CONTEXTS = ['仕事', 'プライベート', '健康', '学び', 'メモ'];
    const TODAY_GOAL = 5;

    const CONTEXT_COLOR_MAP = CONTEXT_PRESETS.reduce((acc, cur) => {
        acc[cur.value] = cur.color;
        return acc;
    }, {});

    const state = {
        entries: [],
        filter: 'day',
        context: 'all',
        search: '',
        lastContext: '仕事',
        anchorDate: startOfDay(new Date())
    };

    const charts = {
        context: null,
        time: null
    };

    const els = {};

    document.addEventListener('DOMContentLoaded', () => {
        Object.assign(els, {
            input: document.getElementById('flowInput'),
            context: document.getElementById('flowContext'),
            add: document.getElementById('btnAddFlow'),
            list: document.getElementById('flowList'),
            countToday: document.getElementById('flowCountToday'),
            countWeek: document.getElementById('flowCountWeek'),
            countMonth: document.getElementById('flowCountMonth'),
            countTotal: document.getElementById('flowCountTotal'),
            countAvg: document.getElementById('flowCountAvg'),
            streak: document.getElementById('flowStreak'),
            heroCount: document.getElementById('flowHeroCount'),
            heroStreak: document.getElementById('flowHeroStreak'),
            filterButtons: document.querySelectorAll('[data-filter]'),
            filterContext: document.getElementById('flowFilterContext'),
            search: document.getElementById('flowSearch'),
            reset: document.getElementById('flowResetData'),
            export: document.getElementById('flowExportData'),
            contextChips: document.getElementById('flowContextChips'),
            progressValue: document.getElementById('flowProgressValue'),
            progressLabel: document.getElementById('flowProgressLabel'),
            topContexts: document.getElementById('flowTopContexts'),
            heatBars: document.getElementById('flowHeatBars'),
            periodLabel: document.getElementById('flowPeriodLabel'),
            prevPeriod: document.getElementById('flowPrevPeriod'),
            nextPeriod: document.getElementById('flowNextPeriod'),
            contextChart: document.getElementById('flowContextChart'),
            timeChart: document.getElementById('flowTimeChart')
        });

        state.entries = loadEntries();
        state.anchorDate = startOfDay(new Date());
        const activeFilter = document.querySelector('[data-filter].active');
        if (activeFilter?.dataset?.filter) {
            state.filter = activeFilter.dataset.filter;
        }
        if (els.context?.value) {
            state.lastContext = els.context.value;
        }

        bindEvents();
        renderContextChips(state, els);
        syncContextOptions();
        setActiveFilterButtons();
        updatePeriodLabel();
        render();
    });

    function bindEvents() {
        els.add?.addEventListener('click', handleAdd);
        els.input?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                handleAdd();
            }
        });

        els.filterButtons?.forEach(btn => {
            btn.addEventListener('click', () => {
                const next = btn.dataset.filter || 'day';
                if (state.filter !== next) {
                    state.filter = next;
                    setActiveFilterButtons();
                    updatePeriodLabel();
                    render();
                }
            });
        });

        els.prevPeriod?.addEventListener('click', () => shiftPeriod(-1));
        els.nextPeriod?.addEventListener('click', () => shiftPeriod(1));

        els.filterContext?.addEventListener('change', (event) => {
            state.context = event.target.value || 'all';
            render();
        });

        els.search?.addEventListener('input', (event) => {
            state.search = (event.target.value || '').trim().toLowerCase();
            render();
        });

        els.reset?.addEventListener('click', () => {
            if (!state.entries.length) return;
            const ok = confirm('すべての Flow を削除してよろしいですか？');
            if (!ok) return;
            state.entries = [];
            persistEntries(state.entries);
            state.context = 'all';
            state.search = '';
            if (els.search) els.search.value = '';
            if (els.filterContext) els.filterContext.value = 'all';
            syncContextOptions();
            render();
        });

        els.export?.addEventListener('click', () => exportEntries(state.entries));
    }

    function handleAdd() {
        const text = (els.input?.value || '').trim();
        if (!text) return;
        addEntry(text, els.context?.value || 'メモ');
        if (els.input) {
            els.input.value = '';
            els.input.focus();
        }
    }

    function addEntry(text, context) {
        if (!text) return;
        const ctx = normalizeContext(context || state.lastContext || 'メモ');
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
    }

    function removeEntry(id) {
        state.entries = state.entries.filter(entry => entry.id !== id);
        persistEntries(state.entries);
        syncContextOptions();
        render();
    }

    function shiftPeriod(step) {
        if (state.filter === 'all') return;
        if (state.filter === 'day') {
            state.anchorDate = addDays(state.anchorDate, step);
        } else if (state.filter === 'week') {
            state.anchorDate = addDays(state.anchorDate, step * 7);
        } else if (state.filter === 'month') {
            state.anchorDate = addMonths(state.anchorDate, step);
        }
        updatePeriodLabel();
        render();
    }

    function render() {
        const filtered = filterEntries();
        renderList(filtered);
        updateStats();
        renderInsights(state.entries);
        updateCharts(filtered);
    }

    function renderList(entries) {
        if (!els.list) return;
        els.list.innerHTML = '';
        if (!entries.length) {
            const empty = document.createElement('li');
            empty.className = 'flow-empty';
            empty.textContent = 'まだ記録がありません。まずは1件記録してみてください。';
            els.list.appendChild(empty);
            return;
        }

        const todayKey = dateKey(new Date());
        const yesterdayKey = dateKey(addDays(new Date(), -1));
        let currentDay = '';

        entries.forEach(entry => {
            const created = safeDate(entry.createdAt);
            const dayKey = dateKey(created);
            if (dayKey !== currentDay) {
                const dayLi = document.createElement('li');
                dayLi.className = 'flow-day';
                if (dayKey === todayKey) {
                    dayLi.textContent = '今日';
                } else if (dayKey === yesterdayKey) {
                    dayLi.textContent = '昨日';
                } else {
                    dayLi.textContent = formatShortDate(created);
                }
                els.list.appendChild(dayLi);
                currentDay = dayKey;
            }

            const li = document.createElement('li');
            li.className = 'flow-entry';
            const dayLabel = dayKey === todayKey ? '今日' : formatShortDate(created);
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

    function filterEntries() {
        const activeRange = getActiveRange();
        return state.entries
            .filter(entry => {
                const created = safeDate(entry.createdAt);
                if (activeRange && (created < activeRange.start || created >= activeRange.end)) return false;
                if (state.context !== 'all' && normalizeContext(entry.context) !== state.context) return false;
                if (!state.search) return true;
                const hay = `${entry.text} ${entry.context || ''}`.toLowerCase();
                return hay.includes(state.search);
            })
            .slice()
            .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
    }

    function getActiveRange() {
        if (state.filter === 'all') return null;
        if (state.filter === 'day') {
            const start = startOfDay(state.anchorDate);
            return { start, end: addDays(start, 1) };
        }
        if (state.filter === 'week') {
            const start = startOfWeek(state.anchorDate);
            return { start, end: addDays(start, 7) };
        }
        if (state.filter === 'month') {
            const start = startOfMonth(state.anchorDate);
            return { start, end: addMonths(start, 1) };
        }
        return null;
    }

    function syncContextOptions() {
        if (!els.filterContext) return;
        const contexts = Array.from(new Set([
            ...DEFAULT_CONTEXTS,
            ...state.entries.map(e => normalizeContext(e.context))
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
            const isActive = (btn.dataset.filter || 'day') === state.filter;
            if (isActive) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    function updatePeriodLabel() {
        if (!els.periodLabel) return;
        if (state.filter === 'all') {
            els.periodLabel.textContent = '全期間';
            togglePeriodButtons(true);
            return;
        }
        togglePeriodButtons(false);
        if (state.filter === 'day') {
            els.periodLabel.textContent = formatDateLong(state.anchorDate);
            return;
        }
        if (state.filter === 'week') {
            const start = startOfWeek(state.anchorDate);
            const end = addDays(start, 6);
            els.periodLabel.textContent = formatRange(start, end);
            return;
        }
        if (state.filter === 'month') {
            els.periodLabel.textContent = `${state.anchorDate.getFullYear()}年${state.anchorDate.getMonth() + 1}月`;
        }
    }

    function togglePeriodButtons(disabled) {
        if (els.prevPeriod) els.prevPeriod.disabled = disabled;
        if (els.nextPeriod) els.nextPeriod.disabled = disabled;
    }

    function updateStats() {
        const stats = summarize(state.entries);
        setText(els.countToday, stats.today);
        setText(els.countWeek, stats.week);
        setText(els.countMonth, stats.month);
        setText(els.countTotal, stats.total);
        setText(els.countAvg, stats.avg ? stats.avg.toFixed(1) : '0');
        setText(els.streak, stats.streak);
        setText(els.heroCount, `${stats.today}件`);
        setText(els.heroStreak, `連続${stats.streak}日目`);
        updateProgress(stats.today);
    }

    function summarize(entries) {
        const today = startOfDay(new Date());
        const todayKey = dateKey(today);
        const weekStart = startOfWeek(today);
        const weekEnd = addDays(weekStart, 7);
        const monthStart = startOfMonth(today);
        const monthEnd = addMonths(monthStart, 1);
        const todayCount = entries.filter(e => dateKey(e.createdAt) === todayKey).length;
        const weekCount = entries.filter(e => {
            const d = startOfDay(e.createdAt);
            return d >= weekStart && d < weekEnd;
        }).length;
        const monthCount = entries.filter(e => {
            const d = safeDate(e.createdAt);
            return d >= monthStart && d < monthEnd;
        }).length;
        const total = entries.length;
        const avg = total ? (total / countRangeDays(entries, today)) : 0;
        return {
            today: todayCount,
            week: weekCount,
            month: monthCount,
            total,
            avg,
            streak: calcStreak(entries)
        };
    }

    function countRangeDays(entries, today) {
        if (!Array.isArray(entries) || !entries.length) return 1;
        const earliest = entries.reduce((min, entry) => {
            const d = safeDate(entry.createdAt);
            return d < min ? d : min;
        }, safeDate(entries[0].createdAt));
        const diff = startOfDay(today).getTime() - startOfDay(earliest).getTime();
        return Math.max(1, Math.floor(diff / 86400000) + 1);
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

    function updateProgress(todayCount) {
        const ratio = Math.max(0, Math.min(1, todayCount / TODAY_GOAL));
        if (els.progressValue) els.progressValue.style.width = `${ratio * 100}%`;
        if (els.progressLabel) els.progressLabel.textContent = `${todayCount}/${TODAY_GOAL}`;
    }

    function renderInsights(entries) {
        renderTopContexts(entries);
        renderHeatBars(entries);
    }

    function renderTopContexts(entries) {
        if (!els.topContexts) return;
        els.topContexts.innerHTML = '';
        const threshold = startOfDay(new Date());
        threshold.setDate(threshold.getDate() - 6);
        const recent = entries.filter(e => startOfDay(e.createdAt) >= threshold);
        if (!recent.length) {
            els.topContexts.innerHTML = '<li class="flow-empty">直近7日のデータがありません。</li>';
            return;
        }
        const counts = {};
        recent.forEach(e => {
            const key = normalizeContext(e.context);
            counts[key] = (counts[key] || 0) + 1;
        });
        const sorted = Object.entries(counts)
            .sort((a, b) => b[1] - a[1])
            .slice(0, 3);
        sorted.forEach(([ctx, count]) => {
            const li = document.createElement('li');
            li.innerHTML = `
                <span class="label"><span class="dot" style="color:${CONTEXT_COLOR_MAP[ctx] || '#475569'}"></span>${escapeHtml(ctx)}</span>
                <span class="count">${count}</span>
            `;
            els.topContexts.appendChild(li);
        });
    }

    function renderHeatBars(entries) {
        if (!els.heatBars) return;
        els.heatBars.innerHTML = '';
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
            els.heatBars.appendChild(row);
        });
        if (!hasData) {
            els.heatBars.innerHTML = '<p class="annotation">直近7日の記録はありません。</p>';
        }
    }

    function updateCharts(entries) {
        if (typeof Chart === 'undefined') return;
        renderContextChart(entries);
        renderTimeChart(entries);
    }

    function renderContextChart(entries) {
        if (!els.contextChart) return;
        const counts = {};
        entries.forEach(entry => {
            const ctx = normalizeContext(entry.context);
            counts[ctx] = (counts[ctx] || 0) + 1;
        });
        const allContexts = Object.keys(counts);
        const ordered = CONTEXT_PRESETS.map(p => p.value).filter(v => allContexts.includes(v));
        allContexts
            .filter(ctx => !ordered.includes(ctx))
            .sort()
            .forEach(ctx => ordered.push(ctx));
        const labels = ordered.length ? ordered : ['データなし'];
        const data = ordered.length ? ordered.map(ctx => counts[ctx]) : [1];
        const colors = ordered.length
            ? ordered.map(ctx => CONTEXT_COLOR_MAP[ctx] || '#94a3b8')
            : ['#e2e8f0'];
        if (charts.context) charts.context.destroy();
        charts.context = new Chart(els.contextChart, {
            type: 'doughnut',
            data: {
                labels,
                datasets: [{
                    data,
                    backgroundColor: colors,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom'
                    }
                }
            }
        });
    }

    function renderTimeChart(entries) {
        if (!els.timeChart) return;
        const counts = new Array(24).fill(0);
        entries.forEach(entry => {
            const d = safeDate(entry.createdAt);
            counts[d.getHours()] += 1;
        });
        const labels = counts.map((_, idx) => `${idx}時`);
        if (charts.time) charts.time.destroy();
        charts.time = new Chart(els.timeChart, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    data: counts,
                    backgroundColor: 'rgba(99, 102, 241, 0.6)',
                    borderRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            precision: 0
                        }
                    }
                }
            }
        });
    }

    function renderContextChips(currentState, elements) {
        const container = elements.contextChips;
        const input = elements.input;
        const select = elements.context;
        if (!container || !select) return;
        container.innerHTML = '';
        CONTEXT_PRESETS.forEach(preset => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'flow-context-chip';
            btn.innerHTML = `<span class="dot" style="color:${preset.color}"></span><span>${preset.label}</span>`;
            btn.addEventListener('click', () => {
                select.value = preset.value;
                currentState.lastContext = preset.value;
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

    function exportEntries(entries) {
        if (!Array.isArray(entries) || !entries.length) {
            alert('エクスポートするデータがありません。');
            return;
        }
        const payload = entries
            .slice()
            .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
            .map(entry => ({
                id: entry.id,
                text: entry.text,
                context: entry.context,
                createdAt: entry.createdAt
            }));
        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `flow-export-${compactDate(new Date())}.json`;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    function loadEntries() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY) || localStorage.getItem(LEGACY_STORAGE_KEY);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) return [];
            return parsed
                .map(item => {
                    const text = String(item.text || '').trim();
                    const context = normalizeContext(item.context);
                    const createdAt = safeDate(item.createdAt || item.timestamp || Date.now());
                    if (!text) return null;
                    return {
                        id: item.id || uid(),
                        text,
                        context,
                        createdAt: createdAt.toISOString()
                    };
                })
                .filter(Boolean);
        } catch (err) {
            console.warn('flow: storage unavailable', err);
            return [];
        }
    }

    function persistEntries(entries) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
        } catch {
            /* noop */
        }
    }

    function normalizeContext(value) {
        const text = String(value || '').trim();
        return text || 'メモ';
    }

    function startOfDay(date) {
        const d = new Date(date);
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function startOfWeek(date) {
        const d = startOfDay(date);
        const day = d.getDay();
        const diff = day === 0 ? -6 : 1 - day;
        d.setDate(d.getDate() + diff);
        return d;
    }

    function startOfMonth(date) {
        const d = startOfDay(date);
        d.setDate(1);
        return d;
    }

    function addDays(date, amount) {
        const d = new Date(date);
        d.setDate(d.getDate() + amount);
        return d;
    }

    function addMonths(date, amount) {
        const d = new Date(date);
        d.setMonth(d.getMonth() + amount);
        return d;
    }

    function dateKey(value) {
        const d = startOfDay(value);
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    function formatTime(date) {
        const d = new Date(date);
        const h = String(d.getHours()).padStart(2, '0');
        const m = String(d.getMinutes()).padStart(2, '0');
        return `${h}:${m}`;
    }

    function formatShortDate(date) {
        return `${String(date.getMonth() + 1).padStart(2, '0')}/${String(date.getDate()).padStart(2, '0')}`;
    }

    function formatDateLong(date) {
        return `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日`;
    }

    function formatRange(start, end) {
        if (start.getFullYear() === end.getFullYear()) {
            return `${formatDateLong(start)} - ${end.getMonth() + 1}月${end.getDate()}日`;
        }
        return `${formatDateLong(start)} - ${formatDateLong(end)}`;
    }

    function compactDate(date) {
        const d = new Date(date);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}${m}${day}`;
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
