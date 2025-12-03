// 統合カレンダー風 UI (Events/Index 用). FullCalendar + サイドバー/統計/検索
(function () {
    const qs = (sel, root = document) => root.querySelector(sel);
    const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    const state = {
        allEvents: [],
        filtered: [],
        filteredBase: [],
        filters: {
            source: 'all',
            category: 'all',
            query: ''
        },
        focusStat: null,
        calendar: null
    };

    async function fetchEvents() {
        const res = await fetch('/Events/GetEvents');
        if (!res.ok) throw new Error('イベント取得に失敗しました');
        const json = await res.json();
        state.allEvents = json.map(e => ({
            ...e,
            source: (e.source || 'Local'),
            type: (e.type || '未分類'),
            categoryId: e.categoryId || '',
            categoryIcon: e.categoryIcon || '',
            categoryColor: e.categoryColor || '',
            priority: (e.priority || 'Normal')
        }));
    }

    function applyFilters() {
        const { source, category, query } = state.filters;
        const term = query.trim().toLowerCase();
        const base = state.allEvents.filter(e => {
            const sOk = source === 'all' || (e.source || '').toLowerCase() === source.toLowerCase();
            const cOk = category === 'all' || (e.categoryId || e.type || '').toLowerCase() === category.toLowerCase();
            const qOk = !term || `${e.title} ${e.description || ''} ${e.location || ''}`.toLowerCase().includes(term);
            return sOk && cOk && qOk;
        });
        state.filteredBase = base;

        let focused = base;
        const today = startOfDay(new Date());
        const startWeek = startOfWeek(new Date());
        const endWeek = endOfWeek(new Date());
        if (state.focusStat === 'today') {
            focused = base.filter(e => isSameDay(parseDate(e.start), today));
        } else if (state.focusStat === 'week') {
            focused = base.filter(e => {
                const d = parseDate(e.start);
                return d >= startWeek && d <= endWeek;
            });
        } else if (state.focusStat === 'dup') {
            const dupIds = findDuplicateIds(base);
            focused = base.filter(e => dupIds.has(e.id));
        }

        state.filtered = focused;
    }

    function updateStats() {
        const today = new Date();
        const startWeek = startOfWeek(today);
        const endWeek = endOfWeek(today);

        const total = state.filteredBase.length;
        const todayCount = state.filteredBase.filter(e => isSameDay(parseDate(e.start), today)).length;
        const weekCount = state.filteredBase.filter(e => {
            const d = parseDate(e.start);
            return d >= startWeek && d <= endWeek;
        }).length;
        const dup = detectDup(state.filteredBase);

        setText('#statTotal', total);
        setText('#statToday', todayCount);
        setText('#statWeek', weekCount);
        setText('#statDup', dup);

        // counts
        const bySource = countBy(state.filtered, e => e.source);
        const byCat = countBy(state.filtered, e => e.categoryId || e.type);
        setText('#srcAll', total); setText('#srcGoogle', bySource.Google || 0); setText('#srcICloud', bySource.ICloud || 0);
        setText('#srcOutlook', bySource.Outlook || 0); setText('#srcWork', bySource.Work || 0); setText('#srcLocal', bySource.Local || 0);
        setText('#catAll', total);
        renderCategoryFilters();
    }

    function updateUpcoming() {
        const list = qs('#icUpcoming');
        if (!list) return;
        list.innerHTML = '';

        const pool = state.filtered.length ? state.filtered : state.allEvents;
        const todayAnchor = startOfDay(new Date());
        const now = new Date();

        const upcoming = pool
            .map(evt => normalizeEventRange(evt))
            .filter(Boolean)
            .filter(item => item.start >= todayAnchor || item.end >= now)
            .sort((a, b) => a.start - b.start)
            .slice(0, 4);

        if (upcoming.length === 0) {
            const empty = document.createElement('li');
            empty.className = 'ic-upcoming-empty';
            empty.textContent = '直近の予定はありません';
            list.appendChild(empty);
            return;
        }

        const dayNames = ['日', '月', '火', '水', '木', '金', '土'];
        upcoming.forEach(item => {
            const li = document.createElement('li');
            const start = item.start;
            const end = item.end;
            const dayLabel = `${start.getMonth() + 1}/${start.getDate()} (${dayNames[start.getDay()]})`;
            const timeLabel = item.allDay ? '終日' : `${formatTime(start)} - ${end ? formatTime(end) : ''}`.trim();
            li.innerHTML = `
                <div class="date">${dayLabel} ${timeLabel}</div>
                <div class="ttl">${item.event.title}</div>
                <div class="meta">${item.event.source || ''} / ${item.event.type || ''}</div>
            `;
            list.appendChild(li);
        });
    }

    function setText(selector, value) {
        const el = qs(selector);
        if (el) el.textContent = value;
    }

    function parseDate(val) {
        if (!val) return new Date();
        const d = new Date(val);
        return isNaN(d) ? new Date() : d;
    }

    function parseDateStrict(val) {
        if (!val) return null;
        const d = new Date(val);
        return isNaN(d) ? null : d;
    }

    function startOfDay(date) {
        const d = new Date(date);
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function startOfWeek(date) {
        const d = startOfDay(date);
        const day = d.getDay();
        d.setDate(d.getDate() - day);
        return d;
    }

    function endOfWeek(date) {
        const d = startOfWeek(date);
        d.setDate(d.getDate() + 6);
        d.setHours(23, 59, 59, 999);
        return d;
    }

    function formatTime(date) {
        return date.toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' });
    }

    function normalizeEventRange(evt) {
        const start = parseDateStrict(evt.start) ?? parseDateStrict(evt.end);
        if (!start) return null;
        const end = parseDateStrict(evt.end) ?? start;
        return {
            event: evt,
            start,
            end,
            allDay: !!evt.allDay
        };
    }

    function isSameDay(a, b) {
        return a.getFullYear() === b.getFullYear() &&
            a.getMonth() === b.getMonth() &&
            a.getDate() === b.getDate();
    }

    function detectDup(events) {
        const seen = new Set(); let dup = 0;
        events.forEach(e => {
            const key = `${(e.start || '').split('T')[0]}-${(e.title || '').toLowerCase()}`;
            if (seen.has(key)) dup += 1;
            seen.add(key);
        });
        return dup;
    }

    function findDuplicateIds(events) {
        const map = new Map();
        events.forEach(e => {
            const key = `${(e.start || '').split('T')[0]}-${(e.title || '').toLowerCase()}`;
            if (!map.has(key)) map.set(key, []);
            map.get(key).push(e.id);
        });
        const dup = new Set();
        map.forEach(ids => { if (ids.length > 1) ids.forEach(id => dup.add(id)); });
        return dup;
    }

    function countBy(arr, fn) {
        return arr.reduce((acc, x) => {
            const k = fn(x) || '';
            acc[k] = (acc[k] || 0) + 1;
            return acc;
        }, {});
    }

    function renderCategoryFilters() {
        const list = qs('#icCategoryList');
        if (!list) return;
        const base = state.allEvents.filter(e => {
            const sOk = state.filters.source === 'all' || (e.source || '').toLowerCase() === state.filters.source.toLowerCase();
            const term = state.filters.query.trim().toLowerCase();
            const qOk = !term || `${e.title} ${e.description || ''} ${e.location || ''}`.toLowerCase().includes(term);
            return sOk && qOk;
        });
        const categories = new Map();
        base.forEach(e => {
            const key = (e.categoryId || e.type || 'uncat').toLowerCase();
            if (!categories.has(key)) {
                categories.set(key, {
                    id: e.categoryId || key,
                    name: e.type || '未分類',
                    color: e.categoryColor || '#e5e7eb',
                    icon: e.categoryIcon || 'fa-shapes'
                });
            }
        });

        const counts = countBy(base, e => (e.categoryId || e.type || 'uncat').toLowerCase());
        list.innerHTML = '';

        const makeBtn = (label, key, color, icon, count) => {
            const btn = document.createElement('button');
            btn.className = 'ic-list-item';
            btn.dataset.category = key;
            btn.innerHTML = `
                <span class="ic-list-label">
                    <span class="ic-list-icon" style="background:${color}; color:#fff;"><i class="fa-solid ${icon}"></i></span>
                    <span>${label}</span>
                </span>
                <span class="count">${count}</span>
            `;
            if (state.filters.category === key) btn.classList.add('active');
            return btn;
        };

        const totalBtn = makeBtn('すべて', 'all', '#e5e7eb', 'fa-shapes', state.filtered.length);
        list.appendChild(totalBtn);

        categories.forEach((cat, key) => {
            const count = counts[key] || 0;
            list.appendChild(makeBtn(cat.name, key, cat.color, cat.icon, count));
        });
    }

    function bindFilters() {
        const sourceList = qs('#icSourceList');
        const catList = qs('#icCategoryList');
        sourceList?.addEventListener('click', (e) => {
            const btn = e.target.closest('button[data-source]');
            if (!btn) return;
            qsa('#icSourceList .ic-list-item').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            state.filters.source = btn.dataset.source || 'all';
            // 絞り込みを戻したときに統計フォーカスが効いたままにならないようにリセット
            state.focusStat = null;
            qsa('.ic-stat').forEach(el => el.classList.remove('active'));
            rerender();
        });
        catList?.addEventListener('click', (e) => {
            const btn = e.target.closest('button[data-category]');
            if (!btn) return;
            qsa('#icCategoryList .ic-list-item').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            state.filters.category = btn.dataset.category || 'all';
            state.focusStat = null;
            qsa('.ic-stat').forEach(el => el.classList.remove('active'));
            rerender();
        });
        const search = qs('#icSearch');
        search?.addEventListener('input', (e) => {
            state.filters.query = e.target.value || '';
            state.focusStat = null;
            qsa('.ic-stat').forEach(el => el.classList.remove('active'));
            rerender();
        });
    }

    function bindCalendarNav(calendar) {
        qs('#calPrev')?.addEventListener('click', () => calendar.prev());
        qs('#calNext')?.addEventListener('click', () => calendar.next());
        qs('#calToday')?.addEventListener('click', () => calendar.today());

        const views = { viewMonth: 'dayGridMonth', viewWeek: 'timeGridWeek', viewDay: 'timeGridDay' };
        Object.entries(views).forEach(([id, v]) => {
            qs('#' + id)?.addEventListener('click', () => {
                calendar.changeView(v);
                Object.keys(views).forEach(k => qs('#' + k)?.classList.remove('active'));
                qs('#' + id)?.classList.add('active');
            });
        });
    }

    function setActiveViewButton(viewName) {
        const mapping = { dayGridMonth: 'viewMonth', timeGridWeek: 'viewWeek', timeGridDay: 'viewDay', listWeek: null };
        Object.values(mapping).forEach(id => id && qs('#' + id)?.classList.remove('active'));
        const btnId = mapping[viewName];
        if (btnId) qs('#' + btnId)?.classList.add('active');
    }

    function focusCalendar(kind) {
        if (!state.calendar) return;
        const now = new Date();
        if (kind === 'today') {
            state.calendar.gotoDate(now);
            state.calendar.changeView('timeGridDay');
            setActiveViewButton('timeGridDay');
        } else if (kind === 'week') {
            state.calendar.gotoDate(now);
            state.calendar.changeView('timeGridWeek');
            setActiveViewButton('timeGridWeek');
        } else if (kind === 'dup') {
            const dupIds = findDuplicateIds(state.filteredBase);
            if (dupIds.size > 0) {
                const first = state.filteredBase.find(e => dupIds.has(e.id));
                if (first?.start) state.calendar.gotoDate(new Date(first.start));
            }
            state.calendar.changeView('dayGridMonth');
            setActiveViewButton('dayGridMonth');
        } else {
            state.calendar.changeView('dayGridMonth');
            setActiveViewButton('dayGridMonth');
        }
    }

    let syncVisual;

    function getSyncVisual() {
        if (!syncVisual) syncVisual = setupSyncVisual();
        return syncVisual;
    }

    async function bindSync() {
        const btn = qs('#syncBtn');
        const visual = getSyncVisual();
        if (!btn) return;
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        btn.addEventListener('click', async () => {
            btn.disabled = true;
            btn.classList.add('is-loading');
            visual.setState('running', 'すべて同期中…');
            try {
                const res = await fetch('/Events/Sync', { method: 'POST', headers: { 'RequestVerificationToken': token } });
                if (!res.ok) {
                    visual.setState('error', '同期に失敗しました');
                    alert('同期に失敗しました');
                    return;
                }
                const payload = await res.json().catch(() => null);
                const saved = payload?.saved ?? '?';
                const scanned = payload?.scanned ?? '?';
                visual.setState('done', `同期完了: 保存 ${saved} / 取得 ${scanned}`);
                await fetchEvents();
                rerender(true);
            } catch (e) {
                visual.setState('error', '同期でエラーが発生しました');
                alert('同期エラー');
            }
            finally {
                btn.disabled = false;
                btn.classList.remove('is-loading');
                setTimeout(() => visual.setState('idle', ''), 1600);
            }
        });
    }

    function setupSyncVisual() {
        const root = qs('#syncVisual');
        const status = qs('#syncStatusText');
        const meter = qs('#syncMeterFill');
        const setState = (state, message) => {
            if (!root) return;
            root.dataset.state = state;
            if (status && message !== undefined) status.textContent = message;
            if (meter) {
                if (state === 'idle') {
                    meter.style.transform = 'scaleX(0)';
                } else if (state === 'done' || state === 'error') {
                    meter.style.transform = 'scaleX(1)';
                } else {
                    meter.style.transform = 'scaleX(1)';
                }
            }
        };
        setState('idle', '');
        return { setState };
    }

    function bindExternalSyncButtons() {
        const visual = getSyncVisual();
        qsa('button[data-sync-provider]').forEach(btn => {
            btn.addEventListener('click', () => {
                const provider = btn.dataset.syncProvider || '外部';
                visual.setState('running', `${provider} 同期中…`);
                btn.classList.add('is-loading');
            });
        });
    }

    function bindStats() {
        const wrap = qs('#icStats');
        if (!wrap) return;
        wrap.addEventListener('click', (e) => {
            const btn = e.target.closest('.ic-stat[data-stat]');
            if (!btn) return;
            const kind = btn.dataset.stat;
            state.focusStat = state.focusStat === kind ? null : kind;
            qsa('.ic-stat').forEach(el => el.classList.toggle('active', el.dataset.stat === state.focusStat));
            applyFilters();
            rerender(true);
            focusCalendar(state.focusStat);
        });
    }

    function mapToFc(events) {
        return events.map(e => ({
            id: e.id,
            title: e.title,
            start: e.start,
            end: e.end,
            allDay: e.allDay,
            extendedProps: {
                source: e.source,
                type: e.type,
                categoryId: e.categoryId,
                categoryIcon: e.categoryIcon,
                categoryColor: e.categoryColor,
                priority: e.priority,
                location: e.location
            }
        }));
    }

    function rerender(skipFetch) {
        if (!skipFetch) applyFilters();
        updateStats();
        updateUpcoming();
        if (state.calendar) {
            state.calendar.removeAllEventSources();
            state.calendar.addEventSource(mapToFc(state.filtered));
            state.calendar.render();
            const period = qs('#calPeriod');
            if (period) period.textContent = state.calendar.view?.title || '----';
        }
    }

    function initCalendar() {
        const calEl = qs('#calendar');
        const period = qs('#calPeriod');
        const mobileMq = window.matchMedia('(max-width: 768px)');
        const initialView = mobileMq.matches ? 'listWeek' : 'dayGridMonth';
        const now = new Date();
        const scrollTime = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}:00`;
        const calendar = new FullCalendar.Calendar(calEl, {
            headerToolbar: false,
            locale: 'ja',
            timeZone: 'local',
            buttonText: { month: '月', week: '週', day: '日' },
            nowIndicator: true,
            scrollTime,
            height: '80vh',
            expandRows: false,
            dayMaxEvents: true,
            initialView,
            events: mapToFc(state.filtered),
            eventClassNames(arg) {
                const props = arg.event.extendedProps || {};
                const prio = (props.priority || '').toString().toLowerCase();
                const classes = [];
                if (prio) classes.push(`prio-${prio}`);
                const src = (props.source || '').toString().toLowerCase();
                if (src) classes.push(`src-${src}`);
                const cat = (props.type || '').toString().toLowerCase();
                if (cat) classes.push(`cat-${cat}`);
                return classes;
            },
            eventContent(arg) {
                const props = arg.event.extendedProps || {};
                const prioKey = (props.priority || '').toString().toLowerCase();
                const prioLabel = { high: '高', normal: '通常', low: '低' }[prioKey] || '通常';
                const prioTag = `<span class="ev-prio-dot prio-${prioKey || 'normal'}" title="優先度: ${prioLabel}" aria-label="優先度: ${prioLabel}"></span>`;
                const time = arg.timeText ? `<span class="ev-time">${arg.timeText}</span>` : '';
                const srcKey = (props.source || '').toString().toLowerCase();
                const srcIcons = {
                    google: '<i class="fa-brands fa-google"></i>',
                    icloud: '<i class="fa-solid fa-cloud"></i>',
                    outlook: '<i class="fa-brands fa-microsoft"></i>',
                    work: '<i class="fa-solid fa-building"></i>',
                    local: '<i class="fa-solid fa-database"></i>'
                };
                const source = props.source ? `<span class="ev-badge ev-source">${srcIcons[srcKey] ?? ''}</span>` : '';
                return { html: `<div class="ev-row wrap">${prioTag}${time}<span class="ev-title">${arg.event.title}</span>${source}</div>` };
            },
            datesSet(info) { if (period) period.textContent = info.view.title; },
            dateClick(info) {
                let startDate;
                let endDate;
                if (info.view.type === 'dayGridMonth') {
                    const now = new Date();
                    startDate = new Date(info.date);
                    startDate.setHours(now.getHours(), now.getMinutes(), 0, 0);
                    endDate = new Date(startDate.getTime() + 2 * 60 * 60 * 1000);
                } else {
                    startDate = new Date(info.date);
                    endDate = new Date(startDate.getTime() + 60 * 60 * 1000);
                }
                const start = startDate.toISOString();
                const end = endDate.toISOString();
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            },
            eventClick(info) {
                if (info.event.id) window.location.href = `/Events/Details?id=${encodeURIComponent(info.event.id)}`;
            },
            selectable: true,
            select(info) {
                const start = info.startStr;
                const end = info.endStr;
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            }
        });
        calendar.render();
        state.calendar = calendar;

        const handleMobileView = (e) => {
            if (!state.calendar) return;
            if (e.matches) {
                state.calendar.changeView('listWeek');
            } else if (state.focusStat !== 'week' && state.focusStat !== 'today') {
                state.calendar.changeView('dayGridMonth');
                setActiveViewButton('dayGridMonth');
            }
        };
        mobileMq.addEventListener('change', handleMobileView);
        handleMobileView(mobileMq);
    }

    function bindMobileToggles() {
        const wrap = qs('.mobile-quick-actions');
        if (!wrap) return;
        const sidebarLeft = qs('#icSidebarLeft');
        const sidebarRight = qs('#icSidebarRight');
        wrap.addEventListener('click', (e) => {
            const btn = e.target.closest('[data-action]');
            if (!btn) return;
            const action = btn.dataset.action;
            if (action === 'scroll-calendar') {
                qs('#calendar')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            if (action === 'toggle-filters') {
                sidebarLeft?.classList.toggle('is-open');
            }
            if (action === 'toggle-insights') {
                sidebarRight?.classList.toggle('is-open');
            }
        });
    }

    async function init() {
        bindFilters();
        await fetchEvents();
        applyFilters();
        initCalendar();
        updateStats();
        updateUpcoming();
        bindCalendarNav(state.calendar);
        bindSync();
        bindExternalSyncButtons();
        bindStats();
        bindMobileToggles();
        startNowClock();
    }

    document.addEventListener('DOMContentLoaded', () => {
        init().catch(err => console.error(err));
    });

    function startNowClock() {
        const root = qs('#icNowClock');
        if (!root) return;
        const timeEl = qs('.time', root);
        const update = () => {
            const now = new Date();
            const opts = { hour: '2-digit', minute: '2-digit' };
            if (timeEl) timeEl.textContent = now.toLocaleTimeString('ja-JP', opts);
        };
        update();
        setInterval(update, 30 * 1000);
    }

})();
