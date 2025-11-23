(function () {
    const STORAGE_KEY = 'pit2hi-preview-events';
    const STORAGE_CARD_KEY = 'pit2hi-preview-card';
    const LAYOUT_STORAGE_KEY = 'pit2hi-preview-layout';
    const FLOW_STORAGE_KEY = 'pit2hi-preview-flow';
    const FLOW_SUGGESTIONS = [
        { text: 'スマホ開く前に深呼吸', context: 'プライベート' },
        { text: '水を飲む', context: '健康' },
        { text: 'ベッドメイキング', context: 'プライベート' },
        { text: '歯磨き', context: '健康' },
        { text: 'カーテンを開ける', context: 'プライベート' },
        { text: '今から集中タイム開始', context: '仕事' },
        { text: '1on1 の準備メモを書く', context: '仕事' }
    ];
    const LEGACY_SAMPLE_TITLES = new Set([
        'スプリントレビュー',
        '歩いてランチ',
        'フォーカス: UI 最終調整',
        'モーニングリセット',
        '顧客ヒアリング',
        'パートナーとディナー',
        'セルフケアストレッチ',
        '夜のストレッチ',
        'セルフケアストレッチ',
        'セルフケアバッファ'
    ]);
    const CATEGORY_LABELS = {
        work: '仕事',
        personal: 'プライベート',
        habit: 'ルーティン',
        learning: '学習',
        focus: 'フォーカス'
    };
    const CATEGORY_COLORS = {
        work: '#6366f1',
        personal: '#ec4899',
        habit: '#8b5cf6',
        learning: '#0ea5e9',
        focus: '#14b8a6'
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (typeof FullCalendar === 'undefined') return;
        const calendarEl = document.getElementById('calendarPreview');
        if (!calendarEl) return;

        const boot = readBootEvents();
        const elements = {
            calendarPeriod: document.getElementById('calendarPeriod'),
            filterButtons: document.querySelectorAll('#calendarFilters .chip'),
            toggleFocusBlocks: document.getElementById('toggleFocusBlocks'),
            toggleCommuteBlocks: document.getElementById('toggleCommuteBlocks'),
            startInput: document.getElementById('quickStart'),
            endInput: document.getElementById('quickEnd'),
            titleInput: document.getElementById('quickTitle'),
            colorInput: document.getElementById('quickColor'),
            templateButtons: document.querySelectorAll('[data-template]'),
            focusJump: document.querySelector('[data-action="focus-jump"]'),
            focusDurations: document.querySelectorAll('#focusDurations .pill'),
            focusTimerDisplay: document.getElementById('focusTimerDisplay'),
            focusToggleBtn: document.getElementById('focusToggleBtn'),
            focusStateLabel: document.getElementById('focusStateLabel'),
            btnExport: document.getElementById('btnExport'),
            btnMockShare: document.getElementById('btnMockShare'),
            btnBlockCommute: document.getElementById('btnBlockCommute'),
            btnSuggestHabit: document.getElementById('btnSuggestHabit'),
            heroMetrics: {
                total: document.getElementById('metricTotalEvents'),
                focus: document.getElementById('metricFocusHours'),
                free: document.getElementById('metricFreeHours'),
                commute: document.getElementById('metricCommute')
            },
            digestMetrics: {
                active: document.getElementById('metricActiveHours'),
                focusRatio: document.getElementById('metricFocusRatio'),
                habit: document.getElementById('metricHabitCount'),
                buffer: document.getElementById('metricBuffer')
            },
            loadBars: document.getElementById('loadBars'),
            insightList: document.getElementById('insightList'),
            upcomingList: document.getElementById('upcomingList'),
            travelTitle: document.getElementById('travelTitle'),
            travelEta: document.getElementById('travelEta'),
            travelHints: document.getElementById('travelHints'),
            wellnessList: document.getElementById('wellnessList'),
            panelGrid: document.querySelector('.preview-grid'),
            layoutToggle: document.querySelector('[data-action="toggle-layout"]'),
            serverState: document.getElementById('serverSyncState'),
            cardUidInput: document.getElementById('icCardUid'),
            cardStateBadge: document.getElementById('cardStateBadge'),
            cardStatusText: document.getElementById('cardStatusText'),
            cardUidLabel: document.getElementById('cardUidLabel'),
            cardRegisterBtn: document.getElementById('btnCardRegister'),
            cardRemoveBtn: document.getElementById('btnCardRemove'),
            cardTemplates: document.querySelectorAll('[data-card-template]'),
            tapHistoryList: document.getElementById('tapHistory'),
            flowInput: document.getElementById('flowInput'),
            flowContext: document.getElementById('flowContext'),
            flowAddButton: document.getElementById('btnAddFlow'),
            flowList: document.getElementById('flowList'),
            flowSuggestionChips: document.getElementById('flowSuggestionChips'),
            flowCountToday: document.getElementById('flowCountToday'),
            flowCountWeek: document.getElementById('flowCountWeek'),
            flowStreak: document.getElementById('flowStreak'),
            flowFilterButtons: document.querySelectorAll('[data-flow-filter]')
        };

        const state = {
            events: stripLegacySamples(loadEvents()),
            serverEvents: boot.events,
            filter: 'all',
            showFocusBlocks: true,
            showCommuteBlocks: true,
            flowEntries: loadFlowEntries(),
            flowFilter: 'today',
            cardProfile: loadCardProfile(),
            layoutEditing: false,
            focusTimer: {
                running: false,
                duration: 25 * 60,
                remaining: 25 * 60,
                timerId: null
            }
        };

        hydrateDefaultForm(elements);
        wireColorSwatches();

        const calendar = new FullCalendar.Calendar(calendarEl, {
            locale: 'ja',
            timeZone: 'local',
            initialView: 'dayGridMonth',
            headerToolbar: false,
            height: 'auto',
            contentHeight: 'auto',
            expandRows: true,
            selectable: true,
            dayMaxEventRows: true,
            navLinks: false,
            selectMirror: true,
            eventTimeFormat: { hour: '2-digit', minute: '2-digit' },
            events: [],
            datesSet(arg) {
                if (elements.calendarPeriod) {
                    elements.calendarPeriod.textContent = arg.view.title;
                }
            },
            dateClick(info) {
                if (!elements.startInput || !elements.endInput) return;
                const start = new Date(info.date);
                const end = new Date(start.getTime() + 60 * 60 * 1000);
                elements.startInput.value = toInputValue(start);
                elements.endInput.value = toInputValue(end);
                elements.titleInput?.focus();
            },
            eventClick(info) {
                const ev = info.event;
                const props = ev.extendedProps || {};
                if (props.serverId) {
                    window.location.href = `/Events/Details?id=${encodeURIComponent(props.serverId)}`;
                    return;
                }
                const summary = [
                    `タイトル: ${ev.title}`,
                    `時間: ${info.event.allDay ? '終日' : `${ev.start?.toLocaleString('ja-JP')} - ${ev.end?.toLocaleString('ja-JP')}`}`,
                    props.location ? `場所: ${props.location}` : null,
                    props.tags?.length ? `タグ: ${props.tags.join(', ')}` : null,
                    props.note ? `メモ: ${props.note}` : null
                ].filter(Boolean).join('\n');
                alert(summary || 'プレビューイベント');
            },
            eventContent(arg) {
                const category = arg.event.extendedProps?.category || 'work';
                const icon = pickCategoryIcon(category);
                const timeText = arg.timeText ? `<span class="fc-time">${arg.timeText}</span>` : '';
                return {
                    html: `<span class="fc-dot" style="background:${CATEGORY_COLORS[category] || '#64748b'}"></span>${timeText}<span>${arg.event.title}</span><span class="fc-icon"><i class="${icon}"></i></span>`
                };
            }
        });

        calendar.render();
        refreshCalendar(calendar, state);
        updateUi(state, elements);

        bindFilters(elements.filterButtons, state, calendar);
        bindNavButtons(calendar);
        bindToggles(elements, state, calendar);
        bindTemplateButtons(elements.templateButtons, elements);
        bindForm(state, elements, calendar);
        bindFocusControls(state, elements, calendar);
        bindActions(elements, state, calendar);
        bindCardControls(state, elements, calendar);
        bindFlowFeature(state, elements);
        initLayoutEditor(state, elements);
        if (elements.serverState) {
            elements.serverState.textContent = boot.signedIn ? '最新' : 'サインインすると予定を表示できます';
            elements.serverState.classList.toggle('error', !boot.signedIn);
        }

        function bindFilters(buttons, currentState, cal) {
            buttons.forEach(btn => {
                btn.addEventListener('click', () => {
                    buttons.forEach(b => b.classList.remove('active'));
                    btn.classList.add('active');
                    currentState.filter = btn.dataset.filter || 'all';
                    refreshCalendar(cal, currentState);
                    updateUi(currentState, elements);
                });
            });
        }

        function bindNavButtons(cal) {
            document.querySelectorAll('[data-cal]').forEach(btn => {
                btn.addEventListener('click', () => {
                    const action = btn.getAttribute('data-cal');
                    if (action === 'prev') cal.prev();
                    if (action === 'next') cal.next();
                    if (action === 'today') cal.today();
                });
            });

            document.querySelectorAll('[data-view]').forEach(btn => {
                btn.addEventListener('click', () => {
                    document.querySelectorAll('[data-view]').forEach(b => b.classList.remove('active'));
                    btn.classList.add('active');
                    cal.changeView(btn.getAttribute('data-view'));
                });
            });
        }

        function bindToggles(els, currentState, cal) {
            els.toggleFocusBlocks?.addEventListener('change', () => {
                currentState.showFocusBlocks = !!els.toggleFocusBlocks.checked;
                refreshCalendar(cal, currentState);
            });
            els.toggleCommuteBlocks?.addEventListener('change', () => {
                currentState.showCommuteBlocks = !!els.toggleCommuteBlocks.checked;
                refreshCalendar(cal, currentState);
            });
        }

        function bindTemplateButtons(buttons, els) {
            buttons.forEach(btn => {
                btn.addEventListener('click', () => {
                    const template = btn.dataset.template;
                    applyTemplate(template, els);
                });
            });
        }

        function bindForm(currentState, els, cal) {
            const form = document.getElementById('quickEventForm');
            if (!form) return;
            form.addEventListener('submit', (event) => {
                event.preventDefault();
                const payload = readFormValues();
                if (!payload) return;
                currentState.events.push(payload);
                persistEvents(currentState.events);
                refreshCalendar(cal, currentState);
                updateUi(currentState, elements);
                form.reset();
                hydrateDefaultForm(els);
            });
        }

        function bindFocusControls(currentState, els, cal) {
            const focusButtons = els.focusDurations;
            focusButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    if (currentState.focusTimer.running) return;
                    focusButtons.forEach(b => b.classList.remove('active'));
                    btn.classList.add('active');
                    const minutes = parseInt(btn.dataset.min || '25', 10);
                    currentState.focusTimer.duration = minutes * 60;
                    currentState.focusTimer.remaining = minutes * 60;
                    updateFocusTimer(els.focusTimerDisplay, currentState.focusTimer.remaining);
                });
            });

            els.focusToggleBtn?.addEventListener('click', () => {
                toggleFocusTimer(currentState.focusTimer, els);
            });

            els.focusJump?.addEventListener('click', () => {
                const nextFocus = nextEvent(getAllEvents(currentState), ev => ev.focus || ev.category === 'focus');
                if (!nextFocus) {
                    alert('フォーカス予定が見つかりませんでした。');
                    return;
                }
                currentState.filter = 'focus';
                document.querySelectorAll('#calendarFilters .chip').forEach(btn => {
                    btn.classList.toggle('active', btn.dataset.filter === 'focus');
                });
                cal.gotoDate(new Date(nextFocus.start));
                cal.changeView('timeGridDay');
                refreshCalendar(cal, currentState);
                updateUi(currentState, elements);
            });
        }

        function bindActions(els, currentState, cal) {
            els.btnExport?.addEventListener('click', () => exportIcs(currentState.events));
            els.btnMockShare?.addEventListener('click', () => {
                const mockUrl = `https://preview.pit2hi022052.dev/share/${Date.now().toString(36)}`;
                navigator.clipboard?.writeText(mockUrl).catch(() => null);
                alert(`共有リンク(モック): ${mockUrl}`);
            });

            els.btnBlockCommute?.addEventListener('click', () => {
                const added = blockCommuteSlot(currentState, getAllEvents(currentState));
                if (added) {
                    persistEvents(currentState.events);
                    refreshCalendar(cal, currentState);
                    updateUi(currentState, elements);
                    alert('移動時間のホールドを追加しました。');
                } else {
                    alert('移動が必要な予定が見つかりません。');
                }
            });

            els.btnSuggestHabit?.addEventListener('click', () => {
                const newHabit = suggestHabitEvent(currentState.events);
                currentState.events.push(newHabit);
                persistEvents(currentState.events);
                refreshCalendar(cal, currentState);
                updateUi(currentState, elements);
                alert('習慣イベントをプレビューに追加しました。');
            });
        }

        function bindCardControls(currentState, els, cal) {
            els.cardRegisterBtn?.addEventListener('click', () => {
                const raw = els.cardUidInput?.value.trim();
                const nextUid = raw || generateMockUid();
                currentState.cardProfile.uid = nextUid;
                currentState.cardProfile.history = currentState.cardProfile.history || [];
                currentState.cardProfile.registeredAt = new Date().toISOString();
                persistCardProfile(currentState.cardProfile);
                updateCardUi(currentState, els);
                alert(`ICカード (${nextUid}) を登録しました。`);
            });

            els.cardRemoveBtn?.addEventListener('click', () => {
                currentState.cardProfile = { uid: '', history: [] };
                persistCardProfile(currentState.cardProfile);
                updateCardUi(currentState, els);
            });

            els.cardTemplates?.forEach(btn => {
                btn.addEventListener('click', () => {
                    const templateId = btn.getAttribute('data-card-template');
                    handleCardTap(templateId, currentState, els, cal);
                });
            });

            if (els.cardUidInput && currentState.cardProfile.uid) {
                els.cardUidInput.value = currentState.cardProfile.uid;
            }

            updateCardUi(currentState, els);
        }

        function readFormValues() {
            const title = document.getElementById('quickTitle')?.value.trim();
            const start = document.getElementById('quickStart')?.value;
            const end = document.getElementById('quickEnd')?.value;
            if (!title || !start || !end) {
                alert('必須項目が未入力です。');
                return null;
            }
            if (new Date(end) <= new Date(start)) {
                alert('終了時間は開始より後にしてください。');
                return null;
            }
            const category = document.getElementById('quickCategory')?.value || 'work';
            const energy = document.getElementById('quickEnergy')?.value || 'medium';
            const location = document.getElementById('quickLocation')?.value || '';
            const tagsRaw = document.getElementById('quickTags')?.value || '';
            const note = document.getElementById('quickNote')?.value || '';
            const color = document.getElementById('quickColor')?.value || CATEGORY_COLORS[category] || '#6366f1';
            const allDay = document.getElementById('quickAllDay')?.checked || false;
            const focusFlag = document.getElementById('quickFocus')?.checked || category === 'focus';

            return {
                id: uid(),
                title,
                start: new Date(start).toISOString(),
                end: new Date(end).toISOString(),
                allDay,
                color,
                category,
                energy,
                location,
                tags: tagsRaw.split(',').map(v => v.trim()).filter(Boolean),
                note,
                focus: focusFlag
            };
        }

        function refreshCalendar(cal, currentState) {
            cal.removeAllEvents();
            const filtered = getAllEvents(currentState).filter(ev => currentState.filter === 'all' ? true : ev.category === currentState.filter);
            filtered.forEach(ev => {
                cal.addEvent(formatForCalendar(ev));
            });
            virtualBlocks(currentState).forEach(block => cal.addEvent(block));
        }

        function updateUi(currentState, els) {
            const allEvents = getAllEvents(currentState);
            const metrics = calcMetrics(allEvents);
            applyMetrics(metrics, els);
            renderLoadBars(metrics, els.loadBars);
            renderInsights(metrics, els.insightList);
            renderTimeline(allEvents, els.upcomingList);
            updateTravelAssistant(allEvents, els);
            updateWellness(metrics, els);
            updateCardUi(currentState, els);
            renderFlowList(currentState, els);
            updateFocusState(currentState, els);
        }
    });

    function hydrateDefaultForm(els) {
        if (!els.startInput || !els.endInput) return;
        const now = new Date();
        now.setMinutes(now.getMinutes() < 30 ? 30 : 0, 0, 0);
        const end = new Date(now.getTime() + 60 * 60 * 1000);
        els.startInput.value = toInputValue(now);
        els.endInput.value = toInputValue(end);
    }

    function wireColorSwatches() {
        document.querySelectorAll('.swatches button[data-color]').forEach(btn => {
            btn.addEventListener('click', () => {
                const colorInput = document.getElementById('quickColor');
                if (!colorInput) return;
                colorInput.value = btn.dataset.color;
            });
        });
    }

    function loadEvents() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (raw) {
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed) && parsed.length) {
                    return parsed;
                }
            }
        } catch (err) {
            console.warn('preview: localStorage unavailable', err);
        }
        return [];
    }

    function readBootEvents() {
        const el = document.getElementById('home-events-json');
        if (!el) return { events: [], signedIn: false };
        const signedIn = el.dataset.signedIn === 'true';
        try {
            const parsed = JSON.parse(el.textContent || '[]');
            const mapped = Array.isArray(parsed) ? parsed.map(mapServerEvent).filter(Boolean) : [];
            return { events: mapped, signedIn };
        } catch {
            return { events: [], signedIn };
        }
    }

    function stripLegacySamples(list) {
        if (!Array.isArray(list) || !list.length) return [];
        const filtered = list.filter(ev => {
            if (!ev || !ev.title) return true;
            if (LEGACY_SAMPLE_TITLES.has(ev.title)) return false;
            if (typeof ev.note === 'string' && ev.note.includes('[ICカード')) return false;
            return true;
        });
        if (filtered.length !== list.length) {
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify(filtered));
            } catch {
                /* noop */
            }
        }
        return filtered;
    }

    function persistEvents(events) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(events));
        } catch {
            /* noop */
        }
    }

    function loadFlowEntries() {
        try {
            const raw = localStorage.getItem(FLOW_STORAGE_KEY);
            if (raw) {
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed)) return parsed;
            }
        } catch (err) {
            console.warn('preview: flow storage unavailable', err);
        }
        const now = new Date().toISOString();
        return [
            { id: 'flow-sample-1', text: 'スマホ開く前に深呼吸', context: 'プライベート', createdAt: now },
            { id: 'flow-sample-2', text: '水を飲んで再集中', context: '健康', createdAt: now }
        ];
    }

    function persistFlowEntries(entries) {
        try {
            localStorage.setItem(FLOW_STORAGE_KEY, JSON.stringify(entries));
        } catch {
            /* noop */
        }
    }

    function getAllEvents(state) {
        return mergeEvents(state.serverEvents || [], state.events || []);
    }

    function mergeEvents(server, local) {
        const merged = [];
        const seen = new Set();
        const add = (event) => {
            if (!event || !event.start) return;
            const key = event.id || `${event.start}-${event.title || ''}`;
            if (seen.has(key)) return;
            seen.add(key);
            merged.push(event);
        };
        server.forEach(add);
        local.forEach(add);
        return merged;
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

    function bindFlowFeature(currentState, els) {
        if (!els.flowList) return;

        const addEntry = (text, context) => {
            if (!text) return;
            currentState.flowEntries = Array.isArray(currentState.flowEntries) ? currentState.flowEntries : [];
            currentState.flowEntries.unshift({
                id: uid(),
                text,
                context: context || 'メモ',
                createdAt: new Date().toISOString()
            });
            persistFlowEntries(currentState.flowEntries);
            renderFlowList(currentState, els);
        };

        const handleAdd = () => {
            const text = (els.flowInput?.value || '').trim();
            if (!text) return;
            addEntry(text, els.flowContext?.value || 'メモ');
            if (els.flowInput) {
                els.flowInput.value = '';
                els.flowInput.focus();
            }
        };

        els.flowAddButton?.addEventListener('click', handleAdd);
        els.flowInput?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                handleAdd();
            }
        });

        els.flowFilterButtons?.forEach(btn => {
            btn.addEventListener('click', () => {
                els.flowFilterButtons.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                currentState.flowFilter = btn.dataset.flowFilter || 'today';
                renderFlowList(currentState, els);
            });
        });

        renderFlowSuggestions(els, addEntry);
        renderFlowList(currentState, els);
    }

    function renderFlowSuggestions(els, addEntry) {
        const container = els.flowSuggestionChips;
        if (!container) return;
        container.innerHTML = '';
        FLOW_SUGGESTIONS.forEach(item => {
            const chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'idea-chip';
            chip.textContent = `${item.text} (${item.context})`;
            chip.addEventListener('click', () => addEntry(item.text, item.context));
            container.appendChild(chip);
        });
    }

    function renderFlowList(currentState, els) {
        const listEl = els.flowList;
        if (!listEl) return;
        const entries = Array.isArray(currentState.flowEntries) ? currentState.flowEntries : [];
        const today = dateKey(new Date());
        const filtered = (currentState.flowFilter === 'all' ? entries : entries.filter(e => dateKey(e.createdAt) === today))
            .slice()
            .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

        if (!filtered.length) {
            listEl.innerHTML = '<li class="flow-empty">まだ記録がありません。思いついた瞬間に書き残してみてください。</li>';
        } else {
            listEl.innerHTML = filtered.map(entry => {
                const createdRaw = new Date(entry.createdAt || Date.now());
                const created = Number.isNaN(createdRaw.getTime()) ? new Date() : createdRaw;
                const dayLabel = dateKey(created) === today ? '今日' : `${created.getMonth() + 1}/${created.getDate()}`;
                return `
                    <li>
                        <div>
                            <div class="flow-text">${entry.text}</div>
                            <div class="flow-meta">
                                <span class="flow-context">${entry.context}</span>
                                <span>${dayLabel} ${formatTime(created)}</span>
                            </div>
                        </div>
                    </li>
                `;
            }).join('');
        }

        const stats = summarizeFlows(entries);
        if (els.flowCountToday) els.flowCountToday.textContent = stats.todayCount;
        if (els.flowCountWeek) els.flowCountWeek.textContent = stats.weekCount;
        if (els.flowStreak) els.flowStreak.textContent = stats.streak;
    }

    function summarizeFlows(entries) {
        const todayKey = dateKey(new Date());
        const weekAgo = startOfDay(new Date());
        weekAgo.setDate(weekAgo.getDate() - 6);
        const weekThreshold = weekAgo.getTime();
        const todayCount = entries.filter(e => dateKey(e.createdAt) === todayKey).length;
        const weekCount = entries.filter(e => startOfDay(e.createdAt).getTime() >= weekThreshold).length;
        return {
            todayCount,
            weekCount,
            streak: calcFlowStreak(entries)
        };
    }

    function calcFlowStreak(entries) {
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

    function toInputValue(date) {
        const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
        return local.toISOString().slice(0, 16);
    }

    function uid() {
        if (typeof crypto !== 'undefined' && crypto.randomUUID) {
            return crypto.randomUUID();
        }
        return 'evt-' + Math.random().toString(36).slice(2, 9);
    }

    function pickCategoryIcon(category) {
        switch (category) {
            case 'work': return 'fa-solid fa-briefcase';
            case 'personal': return 'fa-solid fa-heart';
            case 'habit': return 'fa-solid fa-repeat';
            case 'learning': return 'fa-solid fa-graduation-cap';
            case 'focus': return 'fa-solid fa-bullseye';
            default: return 'fa-solid fa-circle';
        }
    }

    function formatForCalendar(event) {
        return {
            id: event.id,
            title: event.title,
            start: event.start,
            end: event.end,
            allDay: event.allDay,
            backgroundColor: event.color || CATEGORY_COLORS[event.category] || '#64748b',
            borderColor: event.color || CATEGORY_COLORS[event.category] || '#64748b',
            textColor: '#fff',
            extendedProps: {
                category: event.category,
                energy: event.energy,
                location: event.location,
                tags: event.tags,
                note: event.note,
                focus: event.focus,
                serverId: event.serverId
            }
        };
    }

    function virtualBlocks(state) {
        const blocks = [];
        if (state.showFocusBlocks) {
            blocks.push(...generateFocusBlocks());
        }
        if (state.showCommuteBlocks) {
            blocks.push(...generateCommuteBlocks(getAllEvents(state)));
        }
        return blocks;
    }

    function generateFocusBlocks() {
        const blocks = [];
        const base = new Date();
        for (let i = 0; i < 5; i++) {
            const day = new Date(base);
            day.setDate(day.getDate() + i);
            const dow = day.getDay();
            if (dow === 0 || dow === 6) continue;
            const start = new Date(day);
            start.setHours(9, 0, 0, 0);
            const end = new Date(start.getTime() + 90 * 60000);
            blocks.push({
                id: `focus-block-${i}`,
                start,
                end,
                display: 'background',
                classNames: ['fc-focus-block']
            });
        }
        return blocks;
    }

    function generateCommuteBlocks(events) {
        return events
            .filter(ev => ev.location)
            .map(ev => {
                const start = new Date(ev.start);
                const minutes = ev.focus ? 10 : 25;
                const commuteStart = new Date(start.getTime() - minutes * 60000);
                return {
                    id: `commute-${ev.id}`,
                    start: commuteStart,
                    end: start,
                    display: 'background',
                    classNames: ['fc-commute-block']
                };
            });
    }

    function calcMetrics(events) {
        const metrics = {
            totalEvents: events.length,
            focusHours: 0,
            activeHours: 0,
            freeHours: 0,
            commuteMinutes: 0,
            habitHits: 0,
            byCategory: {
                work: 0,
                personal: 0,
                habit: 0,
                learning: 0,
                focus: 0
            }
        };
        const weekStart = startOfWeek(new Date());
        const weekEnd = new Date(weekStart);
        weekEnd.setDate(weekEnd.getDate() + 7);
        const targetHours = 40;

        events.forEach(ev => {
            const start = new Date(ev.start);
            if (start < weekStart || start >= weekEnd) return;
            const hours = eventDurationHours(ev);
            metrics.activeHours += hours;
            metrics.byCategory[ev.category] = (metrics.byCategory[ev.category] || 0) + hours;
            if (ev.focus || ev.category === 'focus') metrics.focusHours += hours;
            if (ev.category === 'habit') metrics.habitHits += 1;
            if (ev.location) metrics.commuteMinutes += 18;
        });

        metrics.freeHours = Math.max(0, targetHours - metrics.activeHours);
        metrics.buffer = Math.max(0, Math.round(6 - Math.max(0, metrics.activeHours - metrics.focusHours) * 0.3));
        metrics.focusRatio = metrics.activeHours ? Math.round((metrics.focusHours / metrics.activeHours) * 100) : 0;
        return metrics;
    }

    function applyMetrics(metrics, els) {
        if (els.heroMetrics.total) els.heroMetrics.total.textContent = metrics.totalEvents.toString();
        if (els.heroMetrics.focus) els.heroMetrics.focus.textContent = `${metrics.focusHours.toFixed(1)}h`;
        if (els.heroMetrics.free) els.heroMetrics.free.textContent = `${metrics.freeHours.toFixed(1)}h`;
        if (els.heroMetrics.commute) els.heroMetrics.commute.textContent = metrics.commuteMinutes ? `${metrics.commuteMinutes}分` : '--';

        els.digestMetrics.active.textContent = `${metrics.activeHours.toFixed(1)}h`;
        els.digestMetrics.focusRatio.textContent = `${metrics.focusRatio}%`;
        els.digestMetrics.habit.textContent = metrics.habitHits.toString();
        els.digestMetrics.buffer.textContent = `${metrics.buffer}h`;
    }

    function renderLoadBars(metrics, container) {
        if (!container) return;
        const total = metrics.activeHours || 1;
        container.innerHTML = Object.entries(metrics.byCategory).map(([key, hours]) => {
            const width = Math.min(100, Math.round((hours / total) * 100));
            return `<div class="bar">
                <span>${CATEGORY_LABELS[key] || key} ${hours.toFixed(1)}h</span>
                <div class="bar-track">
                    <div class="bar-fill" style="width:${width}%;background:${CATEGORY_COLORS[key] || '#0ea5e9'}"></div>
                </div>
            </div>`;
        }).join('');
    }

    function renderInsights(metrics, listEl) {
        if (!listEl) return;
        const messages = [];
        if (metrics.focusRatio < 30) {
            messages.push('集中率が目標未満です。フォーカス枠をもう 1 件追加してみましょう。');
        } else {
            messages.push('フォーカス時間は十分です。リカバリー枠を維持するとバランスが保てます。');
        }
        if (metrics.habitHits < 2) {
            messages.push('ルーティン実施が少ない週です。短いセルフケア枠を提案中。');
        }
        if (metrics.freeHours < 6) {
            messages.push('今週の余白が減っています。不要な会議を整理すると 4h 確保できます。');
        } else {
            messages.push('余白がしっかり確保できています。この調子で予定をブロックしましょう。');
        }
        listEl.innerHTML = messages.map(msg => `<li>${msg}</li>`).join('');
    }

    function renderTimeline(events, container) {
        if (!container) return;
        const now = new Date();
        const upcoming = [...events]
            .filter(ev => new Date(ev.end) >= now)
            .sort((a, b) => new Date(a.start) - new Date(b.start))
            .slice(0, 5);
        container.innerHTML = upcoming.map(ev => {
            const start = new Date(ev.start);
            const end = new Date(ev.end);
            const timeLabel = ev.allDay ? '終日' : `${formatTime(start)} - ${formatTime(end)}`;
            const tags = ev.tags && ev.tags.length ? `<span class="chip">${ev.tags.join(' / ')}</span>` : '';
            const location = ev.location ? `<span>${ev.location}</span>` : '';
            return `<li>
                <div class="time">${timeLabel}</div>
                <div class="card">
                    <strong>${ev.title}</strong>
                    <div class="meta">
                        <span>${CATEGORY_LABELS[ev.category] || ev.category}</span>
                        ${location}
                    </div>
                    ${tags}
                </div>
            </li>`;
        }).join('');
    }

    function updateTravelAssistant(events, els) {
        const next = nextEvent(events, ev => !!ev.location);
        if (!next || !els.travelTitle) {
            if (els.travelTitle) els.travelTitle.textContent = '移動予定は未計測';
            if (els.travelEta) els.travelEta.textContent = '--';
            if (els.travelHints) els.travelHints.innerHTML = '';
            return;
        }
        const travelMinutes = 25;
        els.travelTitle.textContent = `${next.title} への移動`;
        els.travelEta.textContent = `${travelMinutes}分 / ${next.location}`;
        if (els.travelHints) {
            els.travelHints.innerHTML = [
                `開始 ${travelMinutes} 分前にリマインダーを設定します。`,
                '交通 IC 残高チェックを提案済み。'
            ].map(text => `<li>${text}</li>`).join('');
        }
    }

    function updateWellness(metrics, els) {
        if (!els.wellnessList) return;
        const items = els.wellnessList.querySelectorAll('li');
        const rest = metrics.freeHours >= 8 ? 'リラックス枠は十分です。読書タイムを継続しましょう。' : '自由時間が不足気味。今週は 1 件会議を整理して休息を確保。';
        const social = metrics.byCategory.personal >= 3 ? '家族/友人イベントがバランス良く入っています。' : 'つながり時間が少なめです。短いカフェ予定を追加しましょう。';
        const learning = metrics.byCategory.learning >= 2 ? '学びの時間を継続できています。' : '1 コマの学習枠を追加すると習慣化できます。';
        items.forEach(li => {
            if (li.dataset.type === 'rest') li.textContent = `休息のチャンス: ${rest}`;
            if (li.dataset.type === 'social') li.textContent = `つながりポイント: ${social}`;
            if (li.dataset.type === 'learning') li.textContent = `学びリマインダー: ${learning}`;
        });
    }

    function updateFocusState(state, els) {
        const focusEvent = nextEvent(getAllEvents(state), ev => ev.focus || ev.category === 'focus');
        if (!focusEvent) {
            els.focusStateLabel.textContent = 'フォーカス枠は未設定です。';
            return;
        }
        const start = new Date(focusEvent.start);
        const diffMinutes = Math.max(0, Math.round((start - new Date()) / 60000));
        els.focusStateLabel.textContent = `次の集中枠まで ${diffMinutes} 分 · ${focusEvent.title}`;
        updateFocusTimer(els.focusTimerDisplay, state.focusTimer.remaining);
    }

    function toggleFocusTimer(timerState, els) {
        if (timerState.running) {
            clearInterval(timerState.timerId);
            timerState.running = false;
            els.focusToggleBtn.textContent = 'スタート';
            return;
        }
        timerState.running = true;
        const endTime = Date.now() + timerState.remaining * 1000;
        els.focusToggleBtn.textContent = 'ストップ';
        timerState.timerId = setInterval(() => {
            const remaining = Math.max(0, Math.round((endTime - Date.now()) / 1000));
            timerState.remaining = remaining;
            updateFocusTimer(els.focusTimerDisplay, remaining);
            if (remaining <= 0) {
                clearInterval(timerState.timerId);
                timerState.running = false;
                els.focusToggleBtn.textContent = 'スタート';
                alert('フォーカスセッションが完了しました。');
                timerState.remaining = timerState.duration;
            }
        }, 1000);
    }

    function updateFocusTimer(display, seconds) {
        if (!display) return;
        const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
        const ss = String(seconds % 60).padStart(2, '0');
        display.textContent = `${mm}:${ss}`;
    }

    function applyTemplate(template, els) {
        if (!template) return;
        const preset = templatePresets()[template];
        if (!preset) return;
        const start = preset.start();
        const end = new Date(start.getTime() + preset.duration * 60000);
        els.titleInput.value = preset.title;
        els.startInput.value = toInputValue(start);
        els.endInput.value = toInputValue(end);
        document.getElementById('quickCategory').value = preset.category;
        document.getElementById('quickEnergy').value = preset.energy;
        document.getElementById('quickLocation').value = preset.location || '';
        document.getElementById('quickTags').value = preset.tags.join(', ');
        document.getElementById('quickNote').value = preset.note;
        document.getElementById('quickColor').value = preset.color || CATEGORY_COLORS[preset.category] || '#6366f1';
        document.getElementById('quickFocus').checked = !!preset.focus;
        document.getElementById('quickAllDay').checked = !!preset.allDay;
    }

    function templatePresets() {
        const tomorrowMorning = () => {
            const d = new Date();
            d.setDate(d.getDate() + (d.getHours() >= 6 ? 1 : 0));
            d.setHours(6, 30, 0, 0);
            return d;
        };
        const nextSlot = (hour) => {
            const d = new Date();
            d.setHours(hour, 0, 0, 0);
            if (d < new Date()) d.setDate(d.getDate() + 1);
            return d;
        };
        return {
            'routine-morning': {
                title: 'モーニングリセット',
                duration: 30,
                category: 'habit',
                energy: 'low',
                location: '自宅',
                tags: ['朝活', '呼吸'],
                note: '白湯 + ジャーナル + ストレッチ',
                color: '#8b5cf6',
                focus: false,
                start: tomorrowMorning
            },
            'routine-deep': {
                title: 'Deep Work 90',
                duration: 90,
                category: 'focus',
                energy: 'high',
                location: 'Library Mode',
                tags: ['集中', '制作'],
                note: '通知オフ & 15分前にリマインダー',
                color: '#14b8a6',
                focus: true,
                start: () => nextSlot(10)
            },
            'routine-checkin': {
                title: 'PM チェックイン',
                duration: 30,
                category: 'work',
                energy: 'medium',
                location: 'Teams',
                tags: ['sync'],
                note: 'KPI とブロッカーを共有',
                color: '#6366f1',
                focus: false,
                start: () => nextSlot(16)
            },
            'routine-personal': {
                title: 'セルフケアバッファ',
                duration: 40,
                category: 'personal',
                energy: 'low',
                location: '自宅',
                tags: ['wellness'],
                note: '軽いヨガと読書',
                color: '#ec4899',
                focus: false,
                start: () => nextSlot(20)
            }
        };
    }

    function eventDurationHours(ev) {
        const start = new Date(ev.start);
        const end = ev.end ? new Date(ev.end) : new Date(start.getTime() + 30 * 60000);
        return (end - start) / 3600000;
    }

    function startOfWeek(date) {
        const d = new Date(date);
        const day = d.getDay();
        const diff = (day + 6) % 7;
        d.setDate(d.getDate() - diff);
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function formatTime(date) {
        return date.toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' });
    }

    function nextEvent(events, predicate) {
        const now = new Date();
        return events
            .filter(ev => predicate(ev) && new Date(ev.start) >= now)
            .sort((a, b) => new Date(a.start) - new Date(b.start))[0] || null;
    }

    function handleCardTap(templateId, state, els, cal) {
        if (!templateId) return;
        if (!state.cardProfile?.uid) {
            alert('先に IC カードを登録してください。');
            return;
        }
        const templates = cardTemplates();
        const template = templates[templateId];
        if (!template) return;

        const now = new Date();
        const event = {
            id: uid(),
            title: template.title,
            start: now.toISOString(),
            end: new Date(now.getTime() + template.duration * 60000).toISOString(),
            allDay: false,
            color: template.color,
            category: template.category,
            energy: template.energy,
            location: template.location || '',
            tags: template.tags || [],
            note: `[ICカード:${state.cardProfile.uid}] ${template.note || ''}`,
            focus: template.category === 'focus'
        };

        state.events.push(event);
        persistEvents(state.events);

        const historyEntry = {
            title: template.title,
            tappedAt: now.toISOString(),
            note: template.note || ''
        };
        const prevHistory = Array.isArray(state.cardProfile.history) ? state.cardProfile.history : [];
        state.cardProfile.history = [historyEntry, ...prevHistory].slice(0, 4);
        state.cardProfile.lastTemplate = templateId;
        state.cardProfile.lastTap = historyEntry.tappedAt;
        persistCardProfile(state.cardProfile);

        refreshCalendar(cal, state);
        updateUi(state, els);
        alert(`ICカードで「${template.title}」をカレンダーに追加しました。`);
    }

    function updateCardUi(state, els) {
        if (!els.cardStateBadge) return;
        const profile = state.cardProfile || { uid: '', history: [] };
        const connected = !!profile.uid;

        els.cardStateBadge.textContent = connected ? '接続中' : '未登録';
        els.cardStateBadge.dataset.state = connected ? 'connected' : 'disconnected';

        if (els.cardUidLabel) {
            els.cardUidLabel.textContent = connected ? profile.uid : '--';
        }

        if (els.cardStatusText) {
            if (!connected) {
                els.cardStatusText.textContent = 'ICカードは未登録です。UID を入力するか自動発行で連携できます。';
            } else if (profile.lastTap) {
                els.cardStatusText.textContent = `${formatHistoryTime(profile.lastTap)} に ${profile.history?.[0]?.title || 'カード'} を登録しました。`;
            } else {
                els.cardStatusText.textContent = 'ICカードが登録されました。テンプレートをタップして予定を作成できます。';
            }
        }

        if (els.cardUidInput && document.activeElement !== els.cardUidInput) {
            els.cardUidInput.value = profile.uid || '';
        }

        renderTapHistory(els.tapHistoryList, profile.history || []);
    }

    function renderTapHistory(listEl, history) {
        if (!listEl) return;
        if (!history.length) {
            listEl.innerHTML = '<li class="empty">まだ履歴がありません。</li>';
            return;
        }
        listEl.innerHTML = history
            .map(item => `<li><span>${item.title}</span><span class="tap-time">${formatHistoryTime(item.tappedAt)}</span></li>`)
            .join('');
    }

    function loadCardProfile() {
        try {
            const raw = localStorage.getItem(STORAGE_CARD_KEY);
            if (raw) {
                const parsed = JSON.parse(raw);
                if (typeof parsed === 'object' && parsed) {
                    return {
                        uid: parsed.uid || '',
                        history: Array.isArray(parsed.history) ? parsed.history : [],
                        registeredAt: parsed.registeredAt || null,
                        lastTemplate: parsed.lastTemplate || null,
                        lastTap: parsed.lastTap || null
                    };
                }
            }
        } catch (err) {
            console.warn('preview: card storage unavailable', err);
        }
        return { uid: '', history: [] };
    }

    function persistCardProfile(profile) {
        try {
            localStorage.setItem(STORAGE_CARD_KEY, JSON.stringify(profile));
        } catch {
            /* noop */
        }
    }

    function generateMockUid() {
        const block = Math.random().toString(16).slice(2, 6).toUpperCase();
        const suffix = Date.now().toString(16).slice(-4).toUpperCase();
        return `IC-${block}-${suffix}`;
    }

    function cardTemplates() {
        return {
            'work-start': {
                title: '出社チェックイン',
                duration: 60,
                category: 'work',
                energy: 'medium',
                tags: ['office', 'ic-card'],
                note: 'ゲート入場時刻を自動記録',
                color: '#6366f1',
                location: '本社ゲート'
            },
            'client-visit': {
                title: '訪問トラッカー',
                duration: 90,
                category: 'work',
                energy: 'medium',
                tags: ['visit', 'commute'],
                note: '移動 + 滞在時間をホールド',
                color: '#0ea5e9',
                location: '外出先'
            },
            'focus-home': {
                title: '帰宅後フォーカス',
                duration: 45,
                category: 'focus',
                energy: 'high',
                tags: ['evening', 'focus'],
                note: '帰宅タッチで集中枠を確保',
                color: '#14b8a6',
                location: 'Home Office'
            },
            'wellness-shift': {
                title: 'ウェルネスシフト',
                duration: 30,
                category: 'habit',
                energy: 'low',
                tags: ['wellness'],
                note: 'ジム入退館のセルフケア枠',
                color: '#ec4899',
                location: 'Wellness Club'
            }
        };
    }

    function formatHistoryTime(value) {
        if (!value) return '--';
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return '--';
        return d.toLocaleString('ja-JP', { weekday: 'short', hour: '2-digit', minute: '2-digit' });
    }

    function exportIcs(events) {
        const lines = [
            'BEGIN:VCALENDAR',
            'VERSION:2.0',
            'PRODID:-//Pit2Hi022052//Preview//JA'
        ];
        events.slice(0, 25).forEach(ev => {
            lines.push(
                'BEGIN:VEVENT',
                `UID:${ev.id}@pit2hi022052`,
                `DTSTAMP:${icsDate(new Date())}`,
                `DTSTART:${icsDate(new Date(ev.start))}`,
                `DTEND:${icsDate(new Date(ev.end))}`,
                `SUMMARY:${escapeIcs(ev.title)}`,
                `DESCRIPTION:${escapeIcs(ev.note || '')}`,
                ev.location ? `LOCATION:${escapeIcs(ev.location)}` : '',
                'END:VEVENT'
            );
        });
        lines.push('END:VCALENDAR');
        const blob = new Blob([lines.filter(Boolean).join('\r\n')], { type: 'text/calendar' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'pit2hi-preview.ics';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    function icsDate(date) {
        return date.toISOString().replace(/[-:]/g, '').split('.')[0] + 'Z';
    }

    function escapeIcs(text) {
        return text.replace(/,/g, '\\,').replace(/;/g, '\\;').replace(/\n/g, '\\n');
    }

    function blockCommuteSlot(state, combinedEvents) {
        const travel = nextEvent(combinedEvents, ev => !!ev.location);
        if (!travel) return false;
        const start = new Date(travel.start);
        const minutes = 20;
        const commuteStart = new Date(start.getTime() - minutes * 60000);
        state.events.push({
            id: uid(),
            title: `移動: ${travel.title}`,
            start: commuteStart.toISOString(),
            end: start.toISOString(),
            allDay: false,
            color: '#0ea5e9',
            category: 'personal',
            energy: 'low',
            location: '移動',
            tags: ['commute'],
            note: 'スマートアシストで追加',
            focus: false
        });
        return true;
    }

    function suggestHabitEvent() {
        const tonight = new Date();
        tonight.setHours(21, 0, 0, 0);
        if (tonight < new Date()) {
            tonight.setDate(tonight.getDate() + 1);
        }
        return {
            id: uid(),
            title: '夜のストレッチ',
            start: tonight.toISOString(),
            end: new Date(tonight.getTime() + 30 * 60000).toISOString(),
            allDay: false,
            color: '#8b5cf6',
            category: 'habit',
            energy: 'low',
            location: '自宅',
            tags: ['wellbeing'],
            note: 'スクリーンレス 30 分',
            focus: false
        };
    }

    function mapServerEvent(raw) {
        if (!raw || !raw.start) return null;
        const start = new Date(raw.start);
        if (Number.isNaN(start.getTime())) return null;
        const end = raw.end ? new Date(raw.end) : new Date(start.getTime() + 60 * 60 * 1000);
        const text = `${raw.title || ''} ${raw.description || ''}`;
        const category = detectCategory(text);
        return {
            id: `srv-${raw.id || start.toISOString()}`,
            title: raw.title || '(無題のイベント)',
            start: start.toISOString(),
            end: end.toISOString(),
            allDay: !!raw.allDay,
            color: CATEGORY_COLORS[category] || CATEGORY_COLORS.work,
            category,
            energy: 'medium',
            location: '',
            tags: [],
            note: raw.description || '',
            focus: category === 'focus',
            serverId: raw.id || ''
        };
    }

    function detectCategory(text) {
        if (!text) return 'work';
        const lower = text.toLowerCase();
        if (text.includes('フォーカス') || lower.includes('focus')) return 'focus';
        if (text.includes('ランチ') || text.includes('ディナー') || lower.includes('lunch') || lower.includes('dinner') || lower.includes('family')) return 'personal';
        if (text.includes('学習') || lower.includes('learning') || lower.includes('study')) return 'learning';
        if (text.includes('ルーティン') || text.includes('ストレッチ') || lower.includes('habit')) return 'habit';
        return 'work';
    }

    function initLayoutEditor(state, els) {
        const container = els.panelGrid;
        if (!container) return;

        applySavedLayout(container);

        const panelList = () => Array.from(container.querySelectorAll('[data-panel-id]'));
        let draggingPanel = null;

        const updateToggleVisual = () => {
            if (!els.layoutToggle) return;
            els.layoutToggle.classList.toggle('active', state.layoutEditing);
            els.layoutToggle.setAttribute('aria-pressed', state.layoutEditing.toString());
            const label = els.layoutToggle.querySelector('.btn-text');
            if (label) {
                const off = label.dataset.labelOff || label.textContent || '';
                const on = label.dataset.labelOn || off;
                label.textContent = state.layoutEditing ? on : off;
            }
        };

        const setEditing = (enabled) => {
            state.layoutEditing = enabled;
            container.classList.toggle('layout-editing', enabled);
            panelList().forEach(panel => {
                panel.draggable = enabled;
                if (!enabled) panel.classList.remove('dragging');
            });
            if (!enabled && draggingPanel) draggingPanel = null;
            updateToggleVisual();
            if (!enabled) persistLayoutOrder(container);
        };

        const attachPanelEvents = (panel) => {
            panel.addEventListener('dragstart', (event) => {
                if (!state.layoutEditing) {
                    event.preventDefault();
                    return;
                }
                draggingPanel = panel;
                panel.classList.add('dragging');
                if (event.dataTransfer) {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', panel.dataset.panelId || '');
                }
            });

            panel.addEventListener('dragend', () => {
                if (draggingPanel === panel) {
                    panel.classList.remove('dragging');
                    draggingPanel = null;
                    persistLayoutOrder(container);
                }
            });
        };

        panelList().forEach(attachPanelEvents);

        container.addEventListener('dragover', (event) => {
            if (!state.layoutEditing || !draggingPanel) return;
            event.preventDefault();
            const target = event.target.closest('[data-panel-id]');
            if (!target || target === draggingPanel) return;

            const rect = target.getBoundingClientRect();
            const before = event.clientY < rect.top + rect.height / 2;
            if (before) {
                container.insertBefore(draggingPanel, target);
            } else {
                container.insertBefore(draggingPanel, target.nextSibling);
            }
        });

        els.layoutToggle?.addEventListener('click', () => {
            setEditing(!state.layoutEditing);
        });

        setEditing(false);
    }

    function applySavedLayout(container) {
        const saved = loadLayoutOrder();
        if (!saved.length) return;
        const panelMap = new Map();
        container.querySelectorAll('[data-panel-id]').forEach(panel => {
            panelMap.set(panel.dataset.panelId, panel);
        });

        saved.forEach(id => {
            const panel = panelMap.get(id);
            if (panel) {
                container.appendChild(panel);
                panelMap.delete(id);
            }
        });

        panelMap.forEach(panel => container.appendChild(panel));
    }

    function loadLayoutOrder() {
        try {
            const raw = localStorage.getItem(LAYOUT_STORAGE_KEY);
            if (raw) {
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed)) return parsed;
            }
        } catch (err) {
            console.warn('preview: layout storage unavailable', err);
        }
        return [];
    }

    function persistLayoutOrder(container) {
        const order = Array.from(container.querySelectorAll('[data-panel-id]'))
            .map(panel => panel.dataset.panelId)
            .filter(Boolean);
        try {
            localStorage.setItem(LAYOUT_STORAGE_KEY, JSON.stringify(order));
        } catch {
            /* noop */
        }
    }
})();
