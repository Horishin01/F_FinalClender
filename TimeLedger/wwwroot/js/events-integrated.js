// events-integrated.js
// 統合カレンダー UI (Events/Index) のメインスクリプト。FullCalendar の描画、サイドバーのフィルター/統計/検索、同期ステータス表示をまとめて管理。
// ビューポート判定は app-viewport.js に依存し、:root[data-viewport] と連携する。
(function () {
    const qs = (sel, root = document) => root.querySelector(sel);
    const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));
    const viewport = window.appViewport || createViewportFallback();
    const root = document.documentElement;
    const APP_TIMEZONE = resolveTimeZone(document.body?.dataset?.appTimezone || 'Asia/Tokyo');
    const APP_LOCALE = root?.lang ? (root.lang === 'ja' ? 'ja-JP' : root.lang) : 'ja-JP';
    let hasAutoFocusedCalendar = false;
    const CAL_VIEW_STORAGE_KEYS = {
        view: 'events:lastView',
        date: 'events:lastDate'
    };

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
    const getViewportMode = () => {
        if (typeof viewport?.current === 'function') return viewport.current();
        return isMobileMode() ? 'mobile' : 'desktop';
    };

    function resolveTimeZone(timeZoneId) {
        if (!timeZoneId) return 'UTC';
        try {
            new Intl.DateTimeFormat('en-US', { timeZone: timeZoneId }).format(new Date());
            return timeZoneId;
        } catch {
            return 'UTC';
        }
    }

    const isMobileLike = (mode) => mode === 'mobile' || mode === 'tablet';

    function loadCalendarViewState() {
        try {
            const view = localStorage.getItem(CAL_VIEW_STORAGE_KEYS.view) || null;
            const dateStr = localStorage.getItem(CAL_VIEW_STORAGE_KEYS.date) || null;
            const date = dateStr ? new Date(dateStr) : null;
            return {
                view,
                date: date && !Number.isNaN(date.getTime()) ? date : null
            };
        } catch {
            return { view: null, date: null };
        }
    }

    function saveCalendarViewState(view, date) {
        try {
            if (view) localStorage.setItem(CAL_VIEW_STORAGE_KEYS.view, view);
            if (date instanceof Date && !Number.isNaN(date.getTime())) {
                localStorage.setItem(CAL_VIEW_STORAGE_KEYS.date, date.toISOString());
            }
        } catch {
            /* storage が使えない場合は無視 */
        }
    }

    function normalizeViewForMode(view, mode) {
        if (!view) return null;
        const mobile = isMobileLike(mode);
        const weekViews = ['timeGridWeek', 'listWeek'];
        const dayViews = ['timeGridDay', 'listDay'];
        if (view === 'dayGridMonth') return 'dayGridMonth';
        if (weekViews.includes(view)) return mobile ? 'listWeek' : 'timeGridWeek';
        if (dayViews.includes(view)) return 'timeGridDay';
        return null;
    }

    const recurrenceLabelMap = {
        None: '',
        Daily: '毎日',
        Weekly: '毎週',
        Biweekly: '隔週',
        Monthly: '毎月'
    };

    const MS_PER_DAY = 24 * 60 * 60 * 1000;
    const ISO_NO_TZ_RE = /^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?)?$/;
    const APP_PARTS_FORMATTER = new Intl.DateTimeFormat(APP_LOCALE, {
        timeZone: APP_TIMEZONE,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    });

    const pad2 = (v) => String(v).padStart(2, '0');

    function hasExplicitOffset(value) {
        if (typeof value !== 'string') return false;
        const tPos = value.indexOf('T');
        if (tPos < 0) return false;
        if (value.endsWith('Z') || value.endsWith('z')) return true;
        const tail = value.slice(tPos + 1);
        if (tail.includes('+')) return true;
        const lastDash = tail.lastIndexOf('-');
        return lastDash > tail.indexOf(':');
    }

    function getTimeZoneOffsetMinutes(date) {
        const parts = APP_PARTS_FORMATTER.formatToParts(date).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});
        const asUtc = Date.UTC(
            Number(parts.year),
            Number(parts.month) - 1,
            Number(parts.day),
            Number(parts.hour),
            Number(parts.minute),
            Number(parts.second)
        );
        return (asUtc - date.getTime()) / 60000;
    }

    function getZonedPartStrings(value) {
        const d = parseZonedDate(value);
        if (!d) return null;
        return APP_PARTS_FORMATTER.formatToParts(d).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});
    }

    function getZonedNumericParts(value) {
        const parts = getZonedPartStrings(value);
        if (!parts) return null;
        return {
            year: Number(parts.year),
            month: Number(parts.month),
            day: Number(parts.day),
            hour: Number(parts.hour),
            minute: Number(parts.minute),
            second: Number(parts.second)
        };
    }

    function getZonedWeekday(value) {
        const parts = getZonedNumericParts(value);
        if (!parts) return new Date(value).getDay();
        return new Date(Date.UTC(parts.year, parts.month - 1, parts.day)).getUTCDay();
    }

    function getZonedDayNumber(value) {
        const parts = getZonedNumericParts(value);
        if (!parts) return null;
        return Math.floor(Date.UTC(parts.year, parts.month - 1, parts.day) / MS_PER_DAY);
    }

    function zonedTimeToUtc(parts) {
        const year = Number(parts.year);
        const month = Number(parts.month) - 1;
        const day = Number(parts.day);
        const hour = Number(parts.hour || 0);
        const minute = Number(parts.minute || 0);
        const second = Number(parts.second || 0);
        const millisecond = Number(parts.millisecond || 0);
        const utcGuess = Date.UTC(year, month, day, hour, minute, second, millisecond);
        let offset = getTimeZoneOffsetMinutes(new Date(utcGuess));
        let utcMs = utcGuess - offset * 60000;
        const offset2 = getTimeZoneOffsetMinutes(new Date(utcMs));
        if (offset2 !== offset) {
            offset = offset2;
            utcMs = utcGuess - offset * 60000;
        }
        return new Date(utcMs);
    }

    // 指定タイムゾーンを基準に「タイムゾーンなし」を解釈して時刻ずれを防ぐ
    function parseZonedDate(value) {
        if (value instanceof Date) return new Date(value.getTime());
        if (typeof value === 'number') {
            const d = new Date(value);
            return Number.isNaN(d.getTime()) ? null : d;
        }
        if (typeof value !== 'string') return null;
        const raw = value.trim();
        if (!raw) return null;
        if (hasExplicitOffset(raw)) {
            let normalized = raw;
            const fracMatch = raw.match(/\.(\d{3})\d+(?=[+-]|Z|z)/);
            if (fracMatch) normalized = raw.replace(/\.(\d{3})\d+(?=[+-]|Z|z)/, `.${fracMatch[1]}`);
            const d = new Date(normalized);
            return Number.isNaN(d.getTime()) ? null : d;
        }
        const match = ISO_NO_TZ_RE.exec(raw);
        if (match) {
            const year = Number(match[1]);
            const month = Number(match[2]);
            const day = Number(match[3]);
            const hour = match[4] ? Number(match[4]) : 0;
            const minute = match[5] ? Number(match[5]) : 0;
            const second = match[6] ? Number(match[6]) : 0;
            const frac = match[7] ? match[7].slice(0, 3).padEnd(3, '0') : '000';
            const ms = Number(frac);
            return zonedTimeToUtc({
                year,
                month,
                day,
                hour,
                minute,
                second,
                millisecond: ms
            });
        }
        const d = new Date(raw);
        return Number.isNaN(d.getTime()) ? null : d;
    }

    function stripOffsetSuffix(value) {
        if (typeof value !== 'string') return value;
        return value.replace(/([+-]\d{2}:\d{2}|Z)$/i, '');
    }

    function parseCalendarWallTime(value) {
        if (value instanceof Date) return new Date(value.getTime());
        if (typeof value === 'string') {
            const raw = stripOffsetSuffix(value.trim());
            return parseZonedDate(raw);
        }
        return parseZonedDate(value);
    }

    function formatZonedIso(date) {
        const d = parseZonedDate(date);
        if (!d) return '';
        const parts = getZonedPartStrings(d);
        if (!parts) return '';
        const offsetMinutes = getTimeZoneOffsetMinutes(d);
        const sign = offsetMinutes >= 0 ? '+' : '-';
        const abs = Math.abs(offsetMinutes);
        const offHours = pad2(Math.floor(abs / 60));
        const offMinutes = pad2(abs % 60);
        return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}${sign}${offHours}:${offMinutes}`;
    }

    function createZonedDateFromParts(parts) {
        return zonedTimeToUtc(parts);
    }

    function addDaysInZone(date, days) {
        const parts = getZonedNumericParts(date);
        if (!parts) return new Date(date);
        return zonedTimeToUtc({
            year: parts.year,
            month: parts.month,
            day: parts.day + days,
            hour: parts.hour,
            minute: parts.minute,
            second: parts.second
        });
    }

    function addMonthsPreserveDayInZone(date, months, anchorDay) {
        const parts = getZonedNumericParts(date);
        if (!parts) return new Date(date);
        const targetDay = anchorDay ?? parts.day;
        const baseMonth = parts.month - 1;
        let totalMonths = (parts.year * 12 + baseMonth) + months;
        let year = Math.floor(totalMonths / 12);
        let monthIndex = totalMonths % 12;
        if (monthIndex < 0) {
            monthIndex += 12;
            year -= 1;
        }
        const month = monthIndex + 1;
        const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
        const day = Math.min(targetDay, lastDay);
        return createZonedDateFromParts({
            year,
            month,
            day,
            hour: parts.hour,
            minute: parts.minute,
            second: parts.second
        });
    }

    function normalizeRangeForCreate(start, end, isAllDay) {
        const startDate = parseCalendarWallTime(start) ?? new Date(start);
        let endDate = end ? (parseCalendarWallTime(end) ?? new Date(end)) : null;

        if (isAllDay) {
            const startDay = startOfDay(startDate);
            let endDay = endDate ? startOfDay(endDate) : null;
            if (!endDay || endDay <= startDay) {
                endDay = addDaysInZone(startDay, 1);
            }
            return { start: startDay, end: endDay };
        }

        if (!endDate || endDate <= startDate) {
            endDate = new Date(startDate.getTime() + 60 * 60 * 1000);
        }

        return { start: startDate, end: endDate };
    }

    function buildCreateUrl(start, end, isAllDay) {
        const { start: normalizedStart, end: normalizedEnd } = normalizeRangeForCreate(start, end, isAllDay);
        const startDate = new Date(normalizedStart);
        const endDate = new Date(normalizedEnd);
        const offsetMinutes = -getTimeZoneOffsetMinutes(startDate);
        const startStr = formatZonedIso(startDate) || startDate.toISOString();
        const endStr = formatZonedIso(endDate) || endDate.toISOString();
        const allDayFlag = isAllDay ? '&allDay=true' : '';
        const startTicks = startDate.getTime();
        const endTicks = endDate.getTime();
        return `/Events/Create?startDate=${encodeURIComponent(startStr)}&endDate=${encodeURIComponent(endStr)}&startTicks=${startTicks}&endTicks=${endTicks}&offsetMinutes=${offsetMinutes}${allDayFlag}`;
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
        return toZonedDateKey(val);
    }

    function toZonedDateKey(val) {
        const d = parseZonedDate(val);
        if (!d) return '';
        const parts = getZonedNumericParts(d);
        if (!parts) return '';
        return `${parts.year}-${pad2(parts.month)}-${pad2(parts.day)}`;
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
                end: addDaysInZone(date, 1),
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
        return toZonedDateKey(date);
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
            const parts = getZonedNumericParts(start);
            const dayIndex = parts
                ? new Date(Date.UTC(parts.year, parts.month - 1, parts.day)).getUTCDay()
                : start.getDay();
            const month = parts ? parts.month : (start.getMonth() + 1);
            const day = parts ? parts.day : start.getDate();
            const dayLabel = `${month}/${day} (${dayNames[dayIndex]})`;
            const timeLabel = item.allDay ? '終日' : `${formatTime(start)} - ${end ? formatTime(end) : ''}`.trim();
            const badges = renderBadges(item.event, true);
            const metaBase = [item.event.source || '', item.event.type || ''].filter(Boolean).join(' / ');
            const metaLine = badges ? `${metaBase ? `${metaBase} / ` : ''}${badges}` : metaBase;
            li.innerHTML = `
                <div class="date">${dayLabel} ${timeLabel}</div>
                <div class="ttl">${item.event.title}</div>
                <div class="meta">${metaLine}</div>
            `;
            list.appendChild(li);
        });
    }

    function setText(selector, value) {
        const el = qs(selector);
        if (el) el.textContent = value;
    }

    function parseDate(val) {
        const d = parseZonedDate(val);
        return d ?? new Date();
    }

    function parseDateStrict(val) {
        return parseZonedDate(val);
    }

    function startOfDay(date) {
        const parts = getZonedNumericParts(date);
        if (!parts) {
            const d = new Date(date);
            d.setHours(0, 0, 0, 0);
            return d;
        }
        return zonedTimeToUtc({
            year: parts.year,
            month: parts.month,
            day: parts.day,
            hour: 0,
            minute: 0,
            second: 0,
            millisecond: 0
        });
    }

    function startOfWeek(date) {
        const day = getZonedWeekday(date);
        const d = startOfDay(date);
        return addDaysInZone(d, -day);
    }

    function endOfWeek(date) {
        const d = addDaysInZone(startOfWeek(date), 6);
        return endOfDay(d);
    }

    function endOfDay(date) {
        const parts = getZonedNumericParts(date);
        if (!parts) {
            const d = startOfDay(date);
            d.setHours(23, 59, 59, 999);
            return d;
        }
        const d = zonedTimeToUtc({
            year: parts.year,
            month: parts.month,
            day: parts.day,
            hour: 23,
            minute: 59,
            second: 59,
            millisecond: 0
        });
        d.setMilliseconds(999);
        return d;
    }

    function formatTime(date) {
        return date.toLocaleTimeString(APP_LOCALE, { timeZone: APP_TIMEZONE, hour: '2-digit', minute: '2-digit' });
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
        return toZonedDateKey(a) === toZonedDateKey(b);
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

        const pushOccurrence = (evt, startDate, durationMs, hasEnd) => {
            const endDate = hasEnd ? new Date(startDate.getTime() + durationMs) : null;
            const key = getDayKey(startDate);
            const skipList = evt.recurrenceExceptions || [];
            if (shouldSkipDate(skipList, key)) return;
            const startOut = formatZonedIso(startDate);
            if (!startOut) return;
            const endOut = endDate ? formatZonedIso(endDate) : null;
            expanded.push({
                ...evt,
                id: `${evt.id || 'evt'}__r${expanded.length}`,
                baseId: evt.baseId || evt.id,
                start: startOut,
                end: endOut
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
            const hasEnd = !!anchorEnd;
            const durationMs = anchorEnd ? (anchorEnd.getTime() - anchorStart.getTime()) : 0;

            if (recurrence === 'Daily' || recurrence === 'Weekly' || recurrence === 'Biweekly') {
                const interval = recurrence === 'Daily' ? 1 : (recurrence === 'Weekly' ? 7 : 14);
                let current = new Date(anchorStart);
                if (current < start) {
                    const startDay = getZonedDayNumber(start);
                    const currentDay = getZonedDayNumber(current);
                    if (startDay !== null && currentDay !== null) {
                        const diffDays = startDay - currentDay;
                        const steps = Math.floor(diffDays / interval);
                        current = addDaysInZone(current, steps * interval);
                        while (current < start) current = addDaysInZone(current, interval);
                    } else {
                        const diffDays = Math.floor((start.getTime() - current.getTime()) / MS_PER_DAY);
                        const steps = Math.floor(diffDays / interval);
                        current = addDaysInZone(current, steps * interval);
                        while (current < start) current = addDaysInZone(current, interval);
                    }
                }
                while (current <= end) {
                    pushOccurrence(evt, current, durationMs, hasEnd);
                    current = addDaysInZone(current, interval);
                }
                continue;
            }

            if (recurrence === 'Monthly') {
                let current = new Date(anchorStart);
                const anchorParts = getZonedNumericParts(anchorStart);
                const anchorDay = anchorParts ? anchorParts.day : anchorStart.getDate();
                while (current < start) {
                    current = addMonthsPreserveDayInZone(current, 1, anchorDay);
                }
                while (current <= end) {
                    pushOccurrence(evt, current, durationMs, hasEnd);
                    current = addMonthsPreserveDayInZone(current, 1, anchorDay);
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

    function getDefaultCreateRange(isAllDay = false) {
        const baseDate = state.calendar?.getDate?.() ?? new Date();
        const baseParts = getZonedNumericParts(baseDate);
        const nowParts = getZonedNumericParts(new Date());
        let start;
        if (baseParts && nowParts) {
            start = zonedTimeToUtc({
                year: baseParts.year,
                month: baseParts.month,
                day: baseParts.day,
                hour: nowParts.hour,
                minute: nowParts.minute,
                second: 0
            });
        } else {
            const now = new Date();
            start = new Date(baseDate);
            start.setHours(now.getHours(), now.getMinutes(), 0, 0);
        }
        const end = isAllDay ? addDaysInZone(start, 1) : new Date(start.getTime() + 60 * 60 * 1000);
        return { start, end };
    }

    function bindCreateButtons() {
        const goCreate = (isAllDay = false) => {
            const { start, end } = getDefaultCreateRange(isAllDay);
            const url = buildCreateUrl(start, end, isAllDay);
            window.location.href = url;
        };

        qs('#createBtn')?.addEventListener('click', (e) => {
            e.preventDefault();
            goCreate(false);
        });

        qsa('[data-action="create-event"]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                goCreate(false);
                closeMobilePanels();
            });
        });
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
                const firstDate = first?.start ? parseZonedDate(first.start) : null;
                if (firstDate) state.calendar.gotoDate(firstDate);
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
        const fallbackStart = addDaysInZone(startOfWeek(today), -30);
        const fallbackEnd = addDaysInZone(endOfWeek(today), 180);
        const baseStart = state.viewRange?.start ?? fallbackStart;
        const baseEnd = state.viewRange?.end ?? fallbackEnd;
        return {
            start: baseStart,
            end: addDaysInZone(baseEnd, 60) // ビュー範囲より先まで少し広げて直近予定を埋める
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
        const mode = getViewportMode();
        const persisted = loadCalendarViewState();
        const initialView = normalizeViewForMode(persisted.view, mode) || (isMobileMode() ? 'listWeek' : 'dayGridMonth');
        const initialDate = persisted.date && !Number.isNaN(persisted.date.getTime()) ? persisted.date : new Date();
        const now = new Date();
        const nowParts = getZonedNumericParts(now);
        const scrollTime = nowParts
            ? `${pad2(nowParts.hour)}:${pad2(nowParts.minute)}:00`
            : `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}:00`;
        const calendar = new FullCalendar.Calendar(calEl, {
            headerToolbar: false,
            locale: 'ja',
            timeZone: APP_TIMEZONE,
            buttonText: { month: '月', week: '週', day: '日' },
            nowIndicator: true,
            scrollTime,
            height: getCalendarHeight(),
            expandRows: false,
            dayMaxEvents: true,
            initialView,
            initialDate,
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
                const isHoliday = !!props.isHoliday;
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
                const sourceIcon = (!isHoliday && props.source)
                    ? `<span class="ev-mini-icon source src-${srcKey}" title="${props.source}">${srcIcons[srcKey] ?? ''}</span>`
                    : '';
                const recurrenceIcon = (!isHoliday && props.recurrence && props.recurrence !== 'None')
                    ? '<span class="ev-mini-icon repeat" title="繰り返し"><i class="fa-solid fa-arrows-rotate"></i></span>'
                    : '';
                const reminderIcon = (!isHoliday && normalizeReminderValue(props.reminder) !== null)
                    ? '<span class="ev-mini-icon reminder" title="リマインダー"><i class="fa-solid fa-bell"></i></span>'
                    : '';
                const metaIcons = [sourceIcon, recurrenceIcon, reminderIcon].filter(Boolean).join('');
                const metaLine = metaIcons ? `<div class="ev-meta-inline">${metaIcons}</div>` : '';
                if (isListView) {
                    if (isHoliday) {
                        return { html: `<div class="ev-row wrap list-view"><span class="ev-title">${arg.event.title}</span></div>` };
                    }
                    return { html: `<div class="ev-row wrap list-view"><span class="ev-title">${arg.event.title}</span>${metaLine}</div>` };
                }
                if (isHoliday) {
                    return { html: `<div class="ev-row wrap"><span class="ev-title">${arg.event.title}</span></div>` };
                }
                return { html: `<div class="ev-row wrap">${prioTag}${time}<span class="ev-title">${arg.event.title}</span></div>${metaLine}` };
            },
            datesSet(info) {
                if (period) period.textContent = info.view.title;
                const currentDate = state.calendar?.getDate?.() || info.start;
                saveCalendarViewState(info.view.type, currentDate);
                setActiveViewButton(info.view.type);
                if (updateViewRange(info.start, info.end)) {
                    rerender(true);
                }
            },
            dateClick(info) {
                const viewType = info.view?.type || '';
                const isListView = viewType.startsWith('list');
                const isMonthView = viewType === 'dayGridMonth';
                const isAllDay = info.allDay && !isMonthView && !isListView;

                let startDate = parseCalendarWallTime(info.dateStr || info.date) ?? new Date(info.date);
                let endDate;
                if (isAllDay) {
                    startDate = startOfDay(startDate);
                    endDate = addDaysInZone(startDate, 1);
                } else if (isMonthView || isListView) {
                    const nowParts = getZonedNumericParts(new Date());
                    const baseParts = getZonedNumericParts(startDate);
                    if (nowParts && baseParts) {
                        startDate = zonedTimeToUtc({
                            year: baseParts.year,
                            month: baseParts.month,
                            day: baseParts.day,
                            hour: nowParts.hour,
                            minute: nowParts.minute,
                            second: 0
                        });
                    } else {
                        const now = new Date();
                        startDate.setHours(now.getHours(), now.getMinutes(), 0, 0);
                    }
                    endDate = new Date(startDate.getTime() + 2 * 60 * 60 * 1000);
                } else {
                    endDate = new Date(startDate.getTime() + 60 * 60 * 1000);
                }

                window.location.href = buildCreateUrl(startDate, endDate, isAllDay);
            },
            eventClick(info) {
                const props = info.event.extendedProps || {};
                if (props.source === 'Holiday') {
                    info.jsEvent?.preventDefault();
                    return;
                }
                const baseId = props.baseId || info.event.id;
                const occ = formatZonedIso(info.event.start) || info.event.startStr;
                if (baseId) window.location.href = `/Events/Details?id=${encodeURIComponent(baseId)}&occurrence=${encodeURIComponent(occ)}`;
            },
            selectable: true,
            select(info) {
                const isAllDay = info.allDay === true;
                const start = parseCalendarWallTime(info.startStr || info.start) ?? info.start;
                const end = parseCalendarWallTime(info.endStr || info.end) ?? info.end;
                window.location.href = buildCreateUrl(start, end, isAllDay);
            }
        });
        calendar.render();
        state.calendar = calendar;
        setActiveViewButton(initialView);

        const handleMobileView = (mode) => {
            if (!state.calendar) return;
            const isMobile = isMobileLike(mode);
            const currentType = state.calendar.view?.type;
            if (isMobile) {
                const target = normalizeViewForMode(currentType, mode) || 'listWeek';
                if (currentType !== target) {
                    state.calendar.changeView(target);
                    setActiveViewButton(target);
                }
            } else {
                if (currentType === 'listWeek') {
                    state.calendar.changeView('timeGridWeek');
                    setActiveViewButton('timeGridWeek');
                }
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
        bindCreateButtons();
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
