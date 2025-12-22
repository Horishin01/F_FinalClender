// patterns.js
// Patterns ページ向け。埋め込まれた JSON シードを読み込んでカード UI を生成し、簡易インタラクションを提供する。

(() => {
    const seedEl = document.getElementById('pattern-seed');
    if (!seedEl) return;

    let seed = {};
    try {
        seed = JSON.parse(seedEl.textContent || '{}');
    } catch (err) {
        console.warn('pattern-seed の読み込みに失敗しました。', err);
    }

    const STORAGE_KEY = 'patterns:v1';
    const weekdays = Array.isArray(seed.weekdays) ? seed.weekdays : [];
    const defaultPatterns = seed.defaultPatterns || {};

    let patterns = loadStored() || defaultPatterns || {};
    let selectedDay = weekdays[0]?.key || Object.keys(patterns)[0] || 'Mon';
    let editingId = null;

    const dayListEl = document.getElementById('patternDayList');
    const blocksEl = document.getElementById('patternBlocks');
    const form = document.getElementById('patternForm');
    const newBtn = document.getElementById('patternNewBlock');
    const resetBtn = document.getElementById('patternReset');
    const deleteBtn = document.getElementById('patternDelete');

    renderDays();
    renderBlocks();
    resetForm();

    if (form) {
        form.addEventListener('submit', e => {
            e.preventDefault();
            saveBlock();
        });
    }

    if (newBtn) {
        newBtn.addEventListener('click', () => {
            editingId = null;
            resetForm();
            scrollToForm();
        });
    }

    if (resetBtn) {
        resetBtn.addEventListener('click', () => {
            editingId = null;
            resetForm();
        });
    }

    if (deleteBtn) {
        deleteBtn.addEventListener('click', () => {
            if (!editingId) return;
            const list = ensureDay(selectedDay);
            const updated = list.filter(b => b.id !== editingId);
            patterns[selectedDay] = updated;
            persist();
            editingId = null;
            renderBlocks();
            resetForm();
        });
    }

    function renderDays() {
        if (!dayListEl) return;
        dayListEl.innerHTML = '';

        const dayData = weekdays.length
            ? weekdays
            : [
                  { key: 'Mon', label: 'Mon' },
                  { key: 'Tue', label: 'Tue' },
                  { key: 'Wed', label: 'Wed' },
                  { key: 'Thu', label: 'Thu' },
                  { key: 'Fri', label: 'Fri' },
                  { key: 'Sat', label: 'Sat' },
                  { key: 'Sun', label: 'Sun' }
              ];

        dayData.forEach(day => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `pattern-day${day.key === selectedDay ? ' active' : ''}`;
            btn.textContent = day.label || day.key;
            btn.addEventListener('click', () => {
                selectedDay = day.key;
                editingId = null;
                renderDays();
                renderBlocks();
                resetForm();
            });
            dayListEl.appendChild(btn);
        });
    }

    function renderBlocks() {
        if (!blocksEl) return;
        blocksEl.innerHTML = '';

        const list = ensureDay(selectedDay).sort((a, b) => (a.start || '').localeCompare(b.start || ''));
        if (!list.length) {
            const empty = document.createElement('div');
            empty.className = 'pattern-block';
            empty.textContent = 'まだブロックがありません。右側フォームから追加してください。';
            blocksEl.appendChild(empty);
            return;
        }

        list.forEach(block => {
            const item = document.createElement('div');
            item.className = 'pattern-block';

            const main = document.createElement('div');
            main.className = 'pattern-block-main';

            const time = document.createElement('div');
            time.className = 'pattern-block-time';
            time.textContent = `${block.start || '--:--'} - ${block.end || '--:--'}`;

            const title = document.createElement('div');
            title.className = 'pattern-block-title';

            const color = document.createElement('span');
            color.className = 'pattern-color';
            color.style.background = block.color || '#6366f1';

            const name = document.createElement('strong');
            name.textContent = block.title || '無題';

            title.appendChild(color);
            title.appendChild(name);

            const category = document.createElement('div');
            category.className = 'pattern-block-category';
            category.textContent = block.category || '未分類';

            const notes = document.createElement('div');
            notes.className = 'pattern-block-notes';
            notes.textContent = block.notes || '';

            main.appendChild(time);
            main.appendChild(title);
            main.appendChild(category);
            if (block.notes) main.appendChild(notes);

            const tag = document.createElement('span');
            tag.className = 'pattern-tag';
            tag.textContent = '編集';

            item.appendChild(main);
            item.appendChild(tag);
            item.addEventListener('click', () => {
                editingId = block.id;
                fillForm(block);
                scrollToForm();
            });

            blocksEl.appendChild(item);
        });
    }

    function fillForm(block) {
        if (!form) return;
        form.querySelector('#patternId').value = block.id || '';
        form.querySelector('#patternTitle').value = block.title || '';
        form.querySelector('#patternCategory').value = block.category || '';
        form.querySelector('#patternColor').value = block.color || '#4f46e5';
        form.querySelector('#patternStart').value = block.start || '';
        form.querySelector('#patternEnd').value = block.end || '';
        form.querySelector('#patternNotes').value = block.notes || '';
    }

    function resetForm() {
        if (!form) return;
        form.reset();
        form.querySelector('#patternId').value = '';
        form.querySelector('#patternColor').value = '#4f46e5';
        form.querySelector('#patternStart').value = '09:00';
        form.querySelector('#patternEnd').value = '10:00';
    }

    function saveBlock() {
        if (!form) return;
        const id = form.querySelector('#patternId').value || `block-${Date.now()}`;
        const title = form.querySelector('#patternTitle').value.trim();
        const category = form.querySelector('#patternCategory').value.trim();
        const color = form.querySelector('#patternColor').value || '#6366f1';
        const start = form.querySelector('#patternStart').value;
        const end = form.querySelector('#patternEnd').value;
        const notes = form.querySelector('#patternNotes').value.trim();

        if (!title || !start || !end) return;

        const list = ensureDay(selectedDay);
        const next = list.filter(b => b.id !== id);
        next.push({ id, title, category, color, start, end, notes });

        patterns[selectedDay] = next.sort((a, b) => (a.start || '').localeCompare(b.start || ''));
        persist();
        editingId = id;
        renderBlocks();
    }

    function ensureDay(dayKey) {
        if (!patterns[dayKey]) patterns[dayKey] = [];
        return patterns[dayKey];
    }

    function loadStored() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            return parsed && typeof parsed === 'object' ? parsed : null;
        } catch {
            return null;
        }
    }

    function persist() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(patterns));
        } catch {
            // ignore storage errors
        }
    }

    function scrollToForm() {
        const editor = document.querySelector('.pattern-editor');
        if (editor && editor.scrollIntoView) {
            editor.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
})();
