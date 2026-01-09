// events-integrated.js
// 統合カレンダー UI (Events/Index) のメインスクリプト。FullCalendar の描画、サイドバーのフィルター/統計/検索、同期ステータス表示をまとめて管理。
// ビューポート判定は app-viewport.js に依存し、:root[data-viewport] と連携する。
(function () {
    const qs = (sel, root = document) => root.querySelector(sel);
    const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));
    const viewport = window.appViewport || createViewportFallback();
    const root = document.documentElement;
    const JAPAN_TIMEZONE = 'Asia/Tokyo';
    let hasAutoFocusedCalendar = false;

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
        calendar: null,
        viewRange: null,
        holidayDates: new Set()
    };

    function createViewportFallback() {
        const listeners = new Set();
        const mobileMq = window.matchMedia('(max-width: 767px)');
        const tabletMq = window.matchMedia('(max-width: 1024px)');
        const compute = () => {
            if (mobileMq.matches) return 'mobile';
            if (tabletMq.matches) return 'tablet';
            return 'desktop';
        };
        let current = compute();
        const notify = () => {
            const next = compute();
            if (next === current) return;
            current = next;
            listeners.forEach(fn => fn(current));
        };
        const bind = (mq) => {
            if (!mq) return;
            if (mq.addEventListener) {
                mq.addEventListener('change', notify);
            } else if (mq.addListener) {
                mq.addListener(notify);
            }
        };
        bind(mobileMq);
        bind(tabletMq);
        return {
            breakpoints: { mobile: 768, tablet: 1024 },
            current: () => current,
            isMobile: () => current === 'mobile',
            isTablet: () => current === 'tablet',
            isDesktop: () => current === 'desktop',
            subscribe(cb) {
                if (typeof cb !== 'function') return () => { };
                listeners.add(cb);
                cb(current);
                return () => listeners.delete(cb);
            }
        };
    }

    const isDesktopMode = () => viewport.isDesktop();
    const isMobileMode = () => viewport.isMobile() || (viewport.isTablet && viewport.isTablet());

    const recurrenceLabelMap = {
        None: '',
        Daily: '毎日',
        Weekly: '毎週',
        Biweekly: '隔週',
        Monthly: '毎月'
    };

    const MS_PER_DAY = 24 * 60 * 60 * 1000;

    const pad2 = (v) => String(v).padStart(2, '0');

    function formatLocalDateTime(value) {
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return '';
        const formatter = new Intl.DateTimeFormat('ja-JP', {
            timeZone: JAPAN_TIMEZONE,
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false
        });
        const parts = formatter.formatToParts(d).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});
        return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`;
    }

    function addDays(date, days) {
        const d = new Date(date);
        d.setDate(d.getDate() + days);
        return d;
    }

    function addMonthsPreserveDay(date, months, anchorDay) {
        const d = new Date(date);
        const targetDay = anchorDay ?? d.getDate();
        d.setDate(1);
        d.setMonth(d.getMonth() + months);
        const lastDay = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
        if (targetDay > lastDay) {
            // 月末までしかない場合はその月の最終日に合わせる
            d.setDate(lastDay);
        } else {
            d.setDate(targetDay);
        }
        return d;
    }

    function normalizeReminderValue(value) {
        if (value === null || value === undefined || value === '') return null;
        const num = Number(value);
        return Number.isNaN(num) ? null : num;
    }

    function getRecurrenceLabel(recurrence) {
        const key = (recurrence || '').toString();
        if (!key || key === 'None') return '';
        return recurrenceLabelMap[key] || key;
    }

    function getReminderLabel(minutes) {
        const value = normalizeReminderValue(minutes);
        if (value === null) return '';
        if (value === 0) return '開始時';
        if (value < 60) return `${value}分前`;
        const hours = Math.floor(value / 60);
        const mins = value % 60;
        if (mins === 0) return `${hours}時間前`;
        return `${hours}時間${mins}分前`;
    }

    function getDayKey(val) {
        const d = parseDate(val);
        return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
    }

    function renderBadges(evt, compact = false) {
        const chips = [];
        const recurrenceLabel = getRecurrenceLabel(evt.recurrence);
        if (recurrenceLabel) {
            chips.push(`<span class="ev-badge ${compact ? 'compact' : ''} repeat" title="繰り返し: ${recurrenceLabel}"><i class="fa-solid fa-arrows-rotate"></i></span>`);
        }
        const reminderLabel = getReminderLabel(evt.reminder);
        if (reminderLabel) {
            chips.push(`<span class="ev-badge ${compact ? 'compact' : ''} reminder" title="リマインダー: ${reminderLabel}"><i class="fa-solid fa-bell"></i>${reminderLabel}</span>`);
        }
        return chips.join('');
    }

    async function fetchEvents() {
        const res = await fetch('/Events/GetEvents');
        if (!res.ok) throw new Error('イベント取得に失敗しました');
        const json = await res.json();
        const holidays = await fetchHolidayEvents();
        state.holidayDates = new Set(holidays.map(h => toDateKey(h.start)));
        state.allEvents = json.map(e => ({
            ...e,
            source: (e.source || 'Local'),
            type: (e.type || '未分類'),
            categoryId: e.categoryId || '',
            categoryIcon: e.categoryIcon || '',
            categoryColor: e.categoryColor || '',
            priority: (e.priority || 'Normal'),
            recurrence: e.recurrence || 'None',
            reminder: normalizeReminderValue(e.reminder),
            recurrenceExceptions: parseExceptionList(e.recurrenceExceptions)
        })).concat(holidays);
    }

    async function fetchHolidayEvents() {
        try {
            const res = await fetch('https://holidays-jp.github.io/api/v1/date.json');
            if (!res.ok) throw new Error('祝日APIにアクセスできませんでした');
            const data = await res.json();
            return Object.entries(data).map(([date, name]) => ({
                id: `holiday-${date}`,
                title: `${name}`,
                start: date,
                end: addDays(date, 1),
                allDay: true,
                source: 'Holiday',
                type: '',
                categoryId: '',
                categoryIcon: 'fa-flag',
                categoryColor: '#ef4444',
                priority: 'Low',
                recurrence: 'None',
                reminder: null,
                recurrenceExceptions: []
            }));
        } catch (err) {
            console.warn('祝日情報の取得に失敗しました', err);
            return [];
        }
    }

    function parseExceptionList(raw) {
        if (!raw) return [];
        return raw.split(',')
            .map(x => x.trim())
            .filter(Boolean);
    }

    function toDateKey(date) {
        if (typeof date === 'string' && /^\d{4}-\d{2}-\d{2}/.test(date)) {
            return date.slice(0, 10);
        }
        const d = new Date(date);
        return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
    }

    function isHolidayDate(date) {
        return state.holidayDates.has(toDateKey(date));
    }

    function shouldSkipDate(skipList, dateKey) {
        if (!skipList || !skipList.length) return false;
        for (const token of skipList) {
            if (token.startsWith('>=')) {
                const boundary = token.replace('>=', '').trim();
                if (boundary && dateKey >= boundary) return true;
            } else if (token === dateKey) {
                return true;
            }
        }
        return false;
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
            const endToday = endOfDay(today);
            focused = base.filter(e => hasOccurrenceInRange(e, today, endToday));
        } else if (state.focusStat === 'week') {
            focused = base.filter(e => hasOccurrenceInRange(e, startWeek, endWeek));
        } else if (state.focusStat === 'dup') {
            const dupIds = findDuplicateIds(base);
            focused = base.filter(e => dupIds.has(e.id));
        }

        state.filtered = focused;
    }

    function updateStats(events) {
        const today = new Date();
        const startWeek = startOfWeek(today);
        const endWeek = endOfWeek(today);

        const pool = events || state.filteredBase;
        const total = pool.length;
        const todayCount = pool.filter(e => isSameDay(parseDate(e.start), today)).length;
        const weekCount = pool.filter(e => {
            const d = parseDate(e.start);
            return d >= startWeek && d <= endWeek;
        }).length;
        const dup = detectDup(pool);

        setText('#statToday', todayCount);
        setText('#statWeek', weekCount);
        setText('#statDup', dup);

        // counts
        const bySource = countBy(pool, e => e.source);
        const byCat = countBy(pool, e => e.categoryId || e.type);
        setText('#srcAll', total);
        setText('#srcGoogle', bySource.Google || 0);
        setText('#srcICloud', bySource.ICloud || 0);
        setText('#srcOutlook', bySource.Outlook || 0);
        setText('#srcWork', bySource.Work || 0);
        setText('#srcLocal', bySource.Local || 0);
        setText('#catAll', total);
        renderCategoryFilters();
    }

    function updateUpcoming(sourceEvents) {
        const list = qs('#icUpcoming');
        if (!list) return;
        list.innerHTML = '';

        const pool = sourceEvents && sourceEvents.length
            ? sourceEvents
            : (state.filtered.length ? state.filtered : state.allEvents);
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
            const badges = renderBadges(item.event, true);
            li.innerHTML = `
                <div class="date">${dayLabel} ${timeLabel}</div>
                <div class="ttl">${item.event.title}</div>
                <div class="meta">${item.event.source || ''} / ${item.event.type || ''}</div>
                ${badges ? `<div class="ic-badges">${badges}</div>` : ''}
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

    function endOfDay(date) {
        const d = startOfDay(date);
        d.setHours(23, 59, 59, 999);
        return d;
    }

    function formatTime(date) {
        return date.toLocaleTimeString('ja-JP', { timeZone: JAPAN_TIMEZONE, hour: '2-digit', minute: '2-digit' });
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
            const key = `${getDayKey(e.start)}-${(e.title || '').toLowerCase()}`;
            if (seen.has(key)) dup += 1;
            seen.add(key);
        });
        return dup;
    }

    function findDuplicateIds(events) {
        const map = new Map();
        events.forEach(e => {
            const key = `${getDayKey(e.start)}-${(e.title || '').toLowerCase()}`;
            if (!map.has(key)) map.set(key, []);
            map.get(key).push(e.id);
        });
        const dup = new Set();
        map.forEach(ids => { if (ids.length > 1) ids.forEach(id => dup.add(id)); });
        return dup;
    }

    function expandRecurringEvents(events, rangeStart, rangeEnd) {
        if (!rangeStart || !rangeEnd) return events;
        const start = new Date(rangeStart);
        const end = new Date(rangeEnd);
        const expanded = [];

        const pushOccurrence = (evt, startDate, durationMs) => {
            const endDate = durationMs ? new Date(startDate.getTime() + durationMs) : (evt.end ? new Date(evt.end) : null);
            const key = getDayKey(startDate);
            const skipList = evt.recurrenceExceptions || [];
            if (shouldSkipDate(skipList, key)) return;
            expanded.push({
                ...evt,
                id: `${evt.id || 'evt'}__r${expanded.length}`,
                baseId: evt.baseId || evt.id,
                start: new Date(startDate),
                end: endDate
            });
        };

        for (const evt of events) {
            const recurrence = (evt.recurrence || 'None').toString();
            if (!recurrence || recurrence === 'None') {
                expanded.push(evt);
                continue;
            }

            const anchorStart = parseDateStrict(evt.start) ?? parseDateStrict(evt.end);
            if (!anchorStart) {
                expanded.push(evt);
                continue;
            }
            const anchorEnd = parseDateStrict(evt.end);
            const durationMs = anchorEnd ? (anchorEnd.getTime() - anchorStart.getTime()) : 0;

            if (recurrence === 'Daily' || recurrence === 'Weekly' || recurrence === 'Biweekly') {
                const interval = recurrence === 'Daily' ? 1 : (recurrence === 'Weekly' ? 7 : 14);
                let current = new Date(anchorStart);
                if (current < start) {
                    const diffDays = Math.floor((start.getTime() - current.getTime()) / MS_PER_DAY);
                    const steps = Math.floor(diffDays / interval);
                    current = addDays(current, steps * interval);
                    while (current < start) current = addDays(current, interval);
                }
                while (current <= end) {
                    pushOccurrence(evt, current, durationMs);
                    current = addDays(current, interval);
                }
                continue;
            }

            if (recurrence === 'Monthly') {
                let current = new Date(anchorStart);
                const anchorDay = anchorStart.getDate();
                while (current < start) {
                    current = addMonthsPreserveDay(current, 1, anchorDay);
                }
                while (current <= end) {
                    pushOccurrence(evt, current, durationMs);
                    current = addMonthsPreserveDay(current, 1, anchorDay);
                }
                continue;
            }

            expanded.push(evt);
        }
        return expanded;
    }

    function hasOccurrenceInRange(evt, rangeStart, rangeEnd) {
        if (!evt || !rangeStart || !rangeEnd) return false;
        const start = new Date(rangeStart);
        const end = new Date(rangeEnd);
        const occurrences = expandRecurringEvents([evt], start, end);
        return occurrences.some(o => {
            const s = parseDate(o.start);
            return s >= start && s <= end;
        });
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
        const isUncategorized = (evt) => {
            const type = (evt.type || '').trim();
            return !evt.categoryId && (!type || type === '未分類');
        };
        const base = state.allEvents
            .filter(e => e.source !== 'Holiday')
            .filter(e => {
                const sOk = state.filters.source === 'all' || (e.source || '').toLowerCase() === state.filters.source.toLowerCase();
                const term = state.filters.query.trim().toLowerCase();
                const qOk = !term || `${e.title} ${e.description || ''} ${e.location || ''}`.toLowerCase().includes(term);
                return sOk && qOk;
            });
        const categories = new Map();
        base.forEach(e => {
            if (isUncategorized(e)) return;
            const key = (e.categoryId || e.type || '').toLowerCase();
            if (!categories.has(key)) {
                categories.set(key, {
                    id: e.categoryId || key,
                    name: e.type || '未分類',
                    color: e.categoryColor || '#e5e7eb',
                    icon: e.categoryIcon || 'fa-shapes'
                });
            }
        });

        const counts = countBy(base.filter(e => !isUncategorized(e)), e => (e.categoryId || e.type || '').toLowerCase());
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
            if (isMobileMode()) closeMobilePanels();
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
            if (isMobileMode()) closeMobilePanels();
        });
        const search = qs('#icSearch');
        search?.addEventListener('input', (e) => {
            state.filters.query = e.target.value || '';
            state.focusStat = null;
            qsa('.ic-stat').forEach(el => el.classList.remove('active'));
            rerender();
            if (isMobileMode()) closeMobilePanels();
        });
    }

    const mobilePanels = {
        filters: () => qs('#icSidebarLeft'),
        insights: () => qs('#icSidebarRight')
    };

    function getMobileOverlay() {
        return qs('#mobileOverlay');
    }

    function closeMobilePanels() {
        Object.values(mobilePanels).forEach(get => get()?.classList.remove('is-open'));
        getMobileOverlay()?.classList.remove('is-active');
        document.body.classList.remove('mobile-lock');
    }

    function toggleMobilePanel(kind) {
        if (isDesktopMode()) return;
        const target = mobilePanels[kind]?.();
        if (!target) return;
        const willOpen = !target.classList.contains('is-open');
        closeMobilePanels();
        if (willOpen) {
            target.classList.add('is-open');
            getMobileOverlay()?.classList.add('is-active');
            document.body.classList.add('mobile-lock');
        }
    }

    function bindCalendarNav(calendar) {
        qs('#calPrev')?.addEventListener('click', () => calendar.prev());
        qs('#calNext')?.addEventListener('click', () => calendar.next());
        qs('#calToday')?.addEventListener('click', () => calendar.today());

        const views = {
            viewMonth: { desktop: 'dayGridMonth', mobile: 'dayGridMonth' },
            viewWeek: { desktop: 'timeGridWeek', mobile: 'listWeek' },
            viewDay: { desktop: 'timeGridDay', mobile: 'timeGridDay' }
        };
        Object.entries(views).forEach(([id, viewSet]) => {
            qs('#' + id)?.addEventListener('click', () => {
                const targetView = isMobileMode() ? viewSet.mobile : viewSet.desktop;
                calendar.changeView(targetView);
                Object.keys(views).forEach(k => qs('#' + k)?.classList.remove('active'));
                qs('#' + id)?.classList.add('active');
                setActiveViewButton(targetView);
            });
        });
    }

    function setActiveViewButton(viewName) {
        const mapping = { dayGridMonth: 'viewMonth', timeGridWeek: 'viewWeek', timeGridDay: 'viewDay', listWeek: 'viewWeek', listDay: 'viewDay' };
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
            const view = isMobileMode() ? 'listWeek' : 'timeGridWeek';
            state.calendar.changeView(view);
            setActiveViewButton(view);
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
                rerender();
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
            display: e.source === 'Holiday' ? 'block' : 'auto',
            extendedProps: {
                baseId: e.baseId || e.id,
                source: e.source,
                type: e.type,
                categoryId: e.categoryId,
                categoryIcon: e.categoryIcon,
                categoryColor: e.categoryColor,
                priority: e.priority,
                location: e.location,
                recurrence: e.recurrence,
                reminder: e.reminder,
                recurrenceExceptions: e.recurrenceExceptions,
                isHoliday: e.source === 'Holiday'
            }
        }));
    }

    function getExpansionRange() {
        const today = startOfDay(new Date());
        const fallbackStart = addDays(startOfWeek(today), -30);
        const fallbackEnd = addDays(endOfWeek(today), 180);
        const baseStart = state.viewRange?.start ?? fallbackStart;
        const baseEnd = state.viewRange?.end ?? fallbackEnd;
        return {
            start: baseStart,
            end: addDays(baseEnd, 60) // ビュー範囲より先まで少し広げて直近予定を埋める
        };
    }

    function updateViewRange(start, end) {
        const next = { start: new Date(start), end: new Date(end) };
        const changed = !state.viewRange
            || state.viewRange.start.getTime() !== next.start.getTime()
            || state.viewRange.end.getTime() !== next.end.getTime();
        state.viewRange = next;
        return changed;
    }

    function rerender(skipFetch) {
        if (!skipFetch) applyFilters();
        const { start, end } = getExpansionRange();
        const expanded = expandRecurringEvents(state.filtered, start, end);
        updateStats(expanded);
        updateUpcoming(expanded);
        if (state.calendar) {
            state.calendar.removeAllEventSources();
            state.calendar.addEventSource(mapToFc(expanded));
            state.calendar.render();
            const period = qs('#calPeriod');
            if (period) period.textContent = state.calendar.view?.title || '----';
        }
    }

    function getCalendarHeight() {
        return isMobileMode() ? 'auto' : '80vh';
    }

    function refreshCalendarHeight() {
        if (state.calendar) state.calendar.setOption('height', getCalendarHeight());
    }

    function initCalendar() {
        const calEl = qs('#calendar');
        const period = qs('#calPeriod');
        const initialView = isMobileMode() ? 'listWeek' : 'dayGridMonth';
        const now = new Date();
        const scrollTime = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}:00`;
        const calendar = new FullCalendar.Calendar(calEl, {
            headerToolbar: false,
            locale: 'ja',
            timeZone: JAPAN_TIMEZONE,
            buttonText: { month: '月', week: '週', day: '日' },
            nowIndicator: true,
            scrollTime,
            height: getCalendarHeight(),
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
                if (props.isHoliday) classes.push('is-holiday');
                const cat = (props.type || '').toString().toLowerCase();
                if (cat) classes.push(`cat-${cat}`);
                if (props.recurrence && props.recurrence !== 'None') classes.push('has-recurrence');
                if (normalizeReminderValue(props.reminder) !== null) classes.push('has-reminder');
                return classes;
            },
            dayCellClassNames(arg) {
                return isHolidayDate(arg.date) ? ['fc-day-holiday'] : [];
            },
            eventContent(arg) {
                const props = arg.event.extendedProps || {};
                const prioKey = (props.priority || '').toString().toLowerCase();
                const prioLabel = { high: '高', normal: '通常', low: '低' }[prioKey] || '通常';
                const prioTag = `<span class="ev-prio-dot prio-${prioKey || 'normal'}" title="優先度: ${prioLabel}" aria-label="優先度: ${prioLabel}"></span>`;
                const isListView = (arg.view?.type || '').toString().startsWith('list');
                // リストビューでは FullCalendar が左列に時間を表示するため、重複を避けて時間表示を省く
                const time = !isListView && arg.timeText ? `<span class="ev-time">${arg.timeText}</span>` : '';
                const srcKey = (props.source || '').toString().toLowerCase();
                const srcIcons = {
                    google: '<i class="fa-brands fa-google"></i>',
                    icloud: '<i class="fa-solid fa-cloud"></i>',
                    outlook: '<i class="fa-brands fa-microsoft"></i>',
                    work: '<i class="fa-solid fa-building"></i>',
                    local: '<i class="fa-solid fa-database"></i>'
                };
                const source = props.source ? `<span class="ev-badge ev-source">${srcIcons[srcKey] ?? ''}</span>` : '';
                const badges = renderBadges(props);
                const badgeRow = badges ? `<div class="ev-meta-row">${badges}</div>` : '';
                if (isListView) {
                    return { html: `<div class="ev-row wrap list-view"><span class="ev-title">${arg.event.title}</span>${source}</div>${badgeRow}` };
                }
                return { html: `<div class="ev-row wrap">${prioTag}${time}<span class="ev-title">${arg.event.title}</span>${source}</div>${badgeRow}` };
            },
            datesSet(info) {
                if (period) period.textContent = info.view.title;
                if (updateViewRange(info.start, info.end)) {
                    rerender(true);
                }
            },
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
                const start = formatLocalDateTime(startDate) || startDate.toISOString();
                const end = formatLocalDateTime(endDate) || endDate.toISOString();
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            },
            eventClick(info) {
                const props = info.event.extendedProps || {};
                if (props.source === 'Holiday') {
                    info.jsEvent?.preventDefault();
                    return;
                }
                const baseId = props.baseId || info.event.id;
                const occ = info.event.startStr;
                if (baseId) window.location.href = `/Events/Details?id=${encodeURIComponent(baseId)}&occurrence=${encodeURIComponent(occ)}`;
            },
            selectable: true,
            select(info) {
                const start = formatLocalDateTime(info.start) || info.startStr;
                const end = formatLocalDateTime(info.end) || info.endStr;
                window.location.href = `/Events/Create?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
            }
        });
        calendar.render();
        state.calendar = calendar;
        setActiveViewButton(initialView);

        const handleMobileView = (mode) => {
            if (!state.calendar) return;
            const isMobile = mode === 'mobile';
            if (isMobile) {
                state.calendar.changeView('listWeek');
                setActiveViewButton('listWeek');
            } else if (state.focusStat !== 'week' && state.focusStat !== 'today') {
                state.calendar.changeView('dayGridMonth');
                setActiveViewButton('dayGridMonth');
            }
            if (!isMobile) closeMobilePanels();
            refreshCalendarHeight();
        };
        viewport.subscribe(handleMobileView);
    }

    function bindMobileToggles() {
        const wrap = qs('.mobile-quick-actions');
        if (!wrap) return;
        const overlay = getMobileOverlay();
        wrap.addEventListener('click', (e) => {
            const btn = e.target.closest('[data-action]');
            if (!btn) return;
            const action = btn.dataset.action;
            if (action === 'scroll-calendar') {
                closeMobilePanels();
                qs('#calendar')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            if (action === 'toggle-filters') {
                toggleMobilePanel('filters');
            }
            if (action === 'toggle-insights') {
                toggleMobilePanel('insights');
            }
        });
        overlay?.addEventListener('click', closeMobilePanels);
        qsa('[data-mobile-close]').forEach(btn => btn.addEventListener('click', closeMobilePanels));
    }

    function autoFocusCalendarOnLoad() {
        if (hasAutoFocusedCalendar || !isMobileMode()) return;
        const cal = qs('#calendar');
        if (!cal) return;
        // レイアウト描画後に少し遅らせてスクロール
        setTimeout(() => {
            const top = cal.getBoundingClientRect().top + window.scrollY - 18;
            window.scrollTo({ top: Math.max(top, 0), behavior: 'smooth' });
            hasAutoFocusedCalendar = true;
        }, 280);
    }

    async function init() {
        bindFilters();
        await fetchEvents();
        applyFilters();
        initCalendar();
        rerender(true);
        bindCalendarNav(state.calendar);
        bindSync();
        bindExternalSyncButtons();
        bindStats();
        bindMobileToggles();
        window.addEventListener('resize', refreshCalendarHeight);
        autoFocusCalendarOnLoad();
    }

    document.addEventListener('DOMContentLoaded', () => {
        init().catch(err => console.error(err));
    });

})();
