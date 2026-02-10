/*!
 * calendar-ui-backend.js
 * 目的: FullCalendar を “バックエンド連携版(UI固定)” で初期化
 * 流れ:
 *  1) DOMContentLoaded → FullCalendar生成（height:auto, contentHeight:'auto', expandRows:true）
 *  2) 期間ラベル/ビュー切替/前後・今日のナビを外部UIにバインド
 *  3) /Events/GetEvents から予定取得（既存コントローラ）
 *  4) クリック: 日付→Create へ遷移, 予定→Details へ遷移
 *  5) 同期ボタン→ /Events/Sync POST → 再読込
 */

(function () {
    const APP_TIMEZONE = resolveTimeZone(document.body?.dataset?.appTimezone || 'Asia/Tokyo');
    const APP_LOCALE = document.documentElement?.lang ? (document.documentElement.lang === 'ja' ? 'ja-JP' : document.documentElement.lang) : 'ja-JP';
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

    function resolveTimeZone(timeZoneId) {
        if (!timeZoneId) return 'UTC';
        try {
            new Intl.DateTimeFormat('en-US', { timeZone: timeZoneId }).format(new Date());
            return timeZoneId;
        } catch {
            return 'UTC';
        }
    }

    function $(s, r = document) { return r.querySelector(s); }
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

    function parseZonedDate(value) {
        if (value instanceof Date) return new Date(value.getTime());
        if (typeof value === 'number') {
            const d = new Date(value);
            return isNaN(d.getTime()) ? null : d;
        }
        if (typeof value !== 'string') return null;
        const raw = value.trim();
        if (!raw) return null;
        if (hasExplicitOffset(raw)) {
            let normalized = raw;
            const fracMatch = raw.match(/\.(\d{3})\d+(?=[+-]|Z|z)/);
            if (fracMatch) normalized = raw.replace(/\.(\d{3})\d+(?=[+-]|Z|z)/, `.${fracMatch[1]}`);
            const d = new Date(normalized);
            return isNaN(d.getTime()) ? null : d;
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
        return isNaN(d.getTime()) ? null : d;
    }

    function getZonedNumericParts(value) {
        const d = parseZonedDate(value);
        if (!d) return null;
        const parts = APP_PARTS_FORMATTER.formatToParts(d).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});
        return {
            year: Number(parts.year),
            month: Number(parts.month),
            day: Number(parts.day),
            hour: Number(parts.hour),
            minute: Number(parts.minute),
            second: Number(parts.second)
        };
    }

    function formatLocalDateTime(value) {
        const d = parseZonedDate(value);
        if (!d) return '';
        const parts = APP_PARTS_FORMATTER.formatToParts(d).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});
        return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`;
    }

    function formatZonedIso(date) {
        const d = parseZonedDate(date);
        if (!d) return '';
        const formatted = formatLocalDateTime(d);
        if (!formatted) return '';
        const offsetMinutes = getTimeZoneOffsetMinutes(d);
        const sign = offsetMinutes >= 0 ? '+' : '-';
        const abs = Math.abs(offsetMinutes);
        const offHours = pad2(Math.floor(abs / 60));
        const offMinutes = pad2(abs % 60);
        return `${formatted}${sign}${offHours}:${offMinutes}`;
    }

    // α版の暫定対応: Create遷移URLを最新フォーマットに揃える（ticks + offset付き）
    function buildCreateUrl(start, end, isAllDay = false) {
        const offsetMinutes = -getTimeZoneOffsetMinutes(parseZonedDate(start) || new Date());
        const startDate = new Date(start);
        const endDate = new Date(end);
        const startStr = formatZonedIso(startDate) || startDate.toISOString();
        const endStr = formatZonedIso(endDate) || endDate.toISOString();
        const startTicks = startDate.getTime();
        const endTicks = endDate.getTime();
        const allDayFlag = isAllDay ? '&allDay=true' : '';
        return `/Events/Create?startDate=${encodeURIComponent(startStr)}&endDate=${encodeURIComponent(endStr)}&startTicks=${startTicks}&endTicks=${endTicks}&offsetMinutes=${offsetMinutes}${allDayFlag}`;
    }

    document.addEventListener('DOMContentLoaded', function () {
        const el = $('#calendar');
        const period = $('#calPeriod');

        // FullCalendar 本体
        const cal = new FullCalendar.Calendar(el, {
            initialView: 'dayGridMonth',
            headerToolbar: false,          // 内蔵ヘッダは使わない
            locale: 'ja',
            timeZone: APP_TIMEZONE,
            buttonText: { today: '今日', month: '月', week: '週', day: '日' },

            // ← “親の高さ”に依存しないよう auto を採用
            height: 'auto',
            contentHeight: 'auto',
            expandRows: true,
            dayMaxEvents: true,

            slotMinTime: '00:00:00',
            slotMaxTime: '24:00:00',

            events: '/Events/GetEvents',   // 既存のバックエンド
            eventClassNames: function (arg) {
                // 種別/ソース/優先度に応じてクラス付与（色分けに使用）
                const props = arg.event.extendedProps || {};
                const classes = [];
                const cat = (props.type || '').toString().toLowerCase();
                const src = (props.source || '').toString().toLowerCase();
                const prio = (props.priority || '').toString().toLowerCase();
                if (cat) classes.push('cat-' + cat);
                if (src) classes.push('src-' + src);
                if (prio) classes.push('prio-' + prio);
                if (arg.event.allDay) classes.push('all-day');
                return classes;
            },
            eventContent: function (arg) {
                const props = arg.event.extendedProps || {};
                const prioKey = (props.priority || '').toString().toLowerCase();
                const prioLabel = { high: '高', normal: '通常', low: '低' }[prioKey] || props.priority || '';
                const prioTag = prioLabel ? `<span class="ev-prio-tag"><span class="dot"></span>${prioLabel}</span>` : '';
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
                return {
                    html: `
                        <div class="ev-row wrap">
                            ${prioTag}${time}<span class="ev-title">${arg.event.title}</span>${source}
                        </div>
                    `
                };
            },

            // UI 同期
            datesSet(info) { period.textContent = cal.view.title; },

            // クリック挙動
            dateClick(info) {
                let startDate;
                let endDate;
                if (info.view.type === 'dayGridMonth') {
                    startDate = new Date(info.date);
                    const baseParts = getZonedNumericParts(startDate);
                    const nowParts = getZonedNumericParts(new Date());
                    if (baseParts && nowParts) {
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
                    startDate = new Date(info.date);
                    endDate = new Date(startDate.getTime() + 60 * 60 * 1000);
                }
                const start = formatLocalDateTime(startDate) || startDate.toISOString();
                const end = formatLocalDateTime(endDate) || endDate.toISOString();
                window.location.href = buildCreateUrl(start, end, false);
            },
            eventClick(info) {
                const id = info.event.id;
                if (id) window.location.href = `/Events/Details?id=${encodeURIComponent(id)}`;
            },
            selectable: true,
            select(info) {
                const start = formatLocalDateTime(info.start) || info.startStr;
                const end = formatLocalDateTime(info.end) || info.endStr;
                window.location.href = buildCreateUrl(start, end, info.allDay);
            }
        });

        cal.render(); // ← レンダリング

        // 外部UIバインド
        $('#calPrev').onclick = () => cal.prev();
        $('#calNext').onclick = () => cal.next();
        $('#calToday').onclick = () => cal.today();

        const viewBtns = { viewMonth: 'dayGridMonth', viewWeek: 'timeGridWeek', viewDay: 'timeGridDay' };
        Object.entries(viewBtns).forEach(([id, v]) => {
            const btn = $('#' + id);
            btn.onclick = () => {
                cal.changeView(v);
                // active 表示
                Object.keys(viewBtns).forEach(k => $('#' + k).classList.remove('active'));
                btn.classList.add('active');
            };
        });

        // 同期
        const syncBtn = $('#syncBtn');
        if (syncBtn) {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            syncBtn.addEventListener('click', async () => {
                syncBtn.disabled = true;
                try {
                    const res = await fetch('/Events/Sync', {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': token }
                    });
                    if (res.status === 429) { alert('同期は60秒に1回までです。'); }
                    else if (res.ok) {
                        const r = await res.json().catch(() => ({ saved: '?', scanned: '?' }));
                        alert(`同期完了: 保存 ${r.saved} 件 / 取得 ${r.scanned} 件`);
                        cal.refetchEvents();
                    } else {
                        alert('同期に失敗しました。');
                    }
                } catch (e) {
                    alert('同期でエラーが発生しました。');
                } finally {
                    syncBtn.disabled = false;
                }
            });
        }

        // FAB: 新規モーダルの代わりに Create へ
        const fab = $('#fab');
        if (fab) {
            fab.addEventListener('click', (e) => {
                e.preventDefault();
                const now = new Date();
                const end = new Date(now.getTime() + 60 * 60 * 1000);
                window.location.href = buildCreateUrl(now, end, false);
            });
        }
    });
})();
