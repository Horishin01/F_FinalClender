// 統合カレンダー風 UI (Events/Index 用). FullCalendar + サイドバー/統計/検索
(function () {
    const qs = (sel, root = document) => root.querySelector(sel);
    const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    const state = {
        allEvents: [],
        filtered: [],
        filters: {
            source: 'all',
            category: 'all',
            query: ''
        },
        calendar: null
    };

    async function fetchEvents() {
        const res = await fetch('/Events/GetEvents');
        if (!res.ok) throw new Error('イベント取得に失敗しました');
        const json = await res.json();
        state.allEvents = json.map(e => ({
            ...e,
            source: (e.source || 'Local'),
            type: (e.type || 'Personal'),
            priority: (e.priority || 'Normal')
        }));
    }

    function applyFilters() {
        const { source, category, query } = state.filters;
        const term = query.trim().toLowerCase();
        state.filtered = state.allEvents.filter(e => {
            const sOk = source === 'all' || (e.source || '').toLowerCase() === source.toLowerCase();
            const cOk = category === 'all' || (e.type || '').toLowerCase() === category.toLowerCase();
            const qOk = !term || `${e.title} ${e.description || ''} ${e.location || ''}`.toLowerCase().includes(term);
            return sOk && cOk && qOk;
        });
    }

    function updateStats() {
        const today = new Date();
        const startWeek = new Date(today);
        startWeek.setDate(today.getDate() - today.getDay());
        const endWeek = new Date(startWeek);
        endWeek.setDate(startWeek.getDate() + 6);

        const total = state.filtered.length;
        const todayCount = state.filtered.filter(e => isSameDay(parseDate(e.start), today)).length;
        const weekCount = state.filtered.filter(e => {
            const d = parseDate(e.start);
            return d >= startWeek && d <= endWeek;
        }).length;
        const dup = detectDup(state.filtered);

        setText('#statTotal', total);
        setText('#statToday', todayCount);
        setText('#statWeek', weekCount);
        setText('#statDup', dup);

        // counts
        const bySource = countBy(state.filtered, e => e.source);
        const byCat = countBy(state.filtered, e => e.type);
        setText('#srcAll', total); setText('#srcGoogle', bySource.Google || 0); setText('#srcICloud', bySource.ICloud || 0);
        setText('#srcOutlook', bySource.Outlook || 0); setText('#srcWork', bySource.Work || 0); setText('#srcLocal', bySource.Local || 0);
        setText('#catAll', total); setText('#catWork', byCat.Work || 0); setText('#catMeeting', byCat.Meeting || 0);
        setText('#catPersonal', byCat.Personal || 0); setText('#catDeadline', byCat.Deadline || 0); setText('#catStudy', byCat.Study || 0);
    }

    function updateUpcoming() {
        const list = qs('#icUpcoming');
        if (!list) return;
        list.innerHTML = '';
        const now = new Date();
        const upcoming = state.filtered
            .map(e => ({ e, d: parseDate(e.start) }))
            .filter(x => x.d >= now)
            .sort((a, b) => a.d - b.d)
            .slice(0, 5);
        for (const item of upcoming) {
            const li = document.createElement('li');
            li.innerHTML = `
                <div class="date">${item.d.toLocaleString()}</div>
                <div class="ttl">${item.e.title}</div>
                <div class="meta">${item.e.source || ''} / ${item.e.type || ''}</div>
            `;
            list.appendChild(li);
        }
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

    function countBy(arr, fn) {
        return arr.reduce((acc, x) => {
            const k = fn(x) || '';
            acc[k] = (acc[k] || 0) + 1;
            return acc;
        }, {});
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
            rerender();
        });
        catList?.addEventListener('click', (e) => {
            const btn = e.target.closest('button[data-category]');
            if (!btn) return;
            qsa('#icCategoryList .ic-list-item').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            state.filters.category = btn.dataset.category || 'all';
            rerender();
        });
        const search = qs('#icSearch');
        search?.addEventListener('input', (e) => {
            state.filters.query = e.target.value || '';
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

    async function bindSync() {
        const btn = qs('#syncBtn');
        if (!btn) return;
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        btn.addEventListener('click', async () => {
            btn.disabled = true;
            try {
                const res = await fetch('/Events/Sync', { method: 'POST', headers: { 'RequestVerificationToken': token } });
                if (!res.ok) { alert('同期に失敗しました'); return; }
                await fetchEvents();
                rerender(true);
            } catch (e) { alert('同期エラー'); }
            finally { btn.disabled = false; }
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
        const calendar = new FullCalendar.Calendar(calEl, {
            headerToolbar: false,
            locale: 'ja',
            timeZone: 'local',
            buttonText: { month: '月', week: '週', day: '日' },
            height: 'auto',
            contentHeight: 'auto',
            expandRows: true,
            dayMaxEvents: true,
            events: mapToFc(state.filtered),
            datesSet(info) { if (period) period.textContent = info.view.title; },
            dateClick(info) {
                const start = info.dateStr;
                const end = new Date(info.date.getTime() + 60 * 60 * 1000).toISOString();
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            },
            eventClick(info) {
                if (info.event.id) window.location.href = `/Events/Details?id=${encodeURIComponent(info.event.id)}`;
            }
        });
        calendar.render();
        state.calendar = calendar;
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
    }

    document.addEventListener('DOMContentLoaded', () => {
        init().catch(err => console.error(err));
    });
})();
