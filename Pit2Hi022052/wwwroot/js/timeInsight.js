(() => {
    const dataEl = document.getElementById('timeInsight-data');
    if (!dataEl) return;

    let seed = {};
    try {
        seed = JSON.parse(dataEl.textContent || '{}');
    } catch (err) {
        console.warn('timeInsight JSON の読み込みに失敗しました。', err);
    }

    const categories = Array.isArray(seed.categories) ? seed.categories : [];
    const weekLabel = seed.weekLabel || '';

    renderSummary(categories, weekLabel);
    renderCategoryCards(categories);

    function renderSummary(list, label) {
        const total = list.reduce((sum, c) => sum + (Number(c.actual) || 0), 0);
        const avg = total / 7;
        const top = list.reduce(
            (best, c) => (Number(c.actual || 0) > Number(best.actual || 0) ? c : best),
            { name: '-', actual: 0 }
        );

        const totalEl = document.getElementById('tiTotalHours');
        const avgEl = document.getElementById('tiAvgPerDay');
        const topEl = document.getElementById('tiTopCategory');
        const rangeEl = document.getElementById('tiWeekRange');

        if (totalEl) totalEl.textContent = total.toFixed(1);
        if (avgEl) avgEl.textContent = avg.toFixed(1);
        if (topEl) topEl.textContent = top?.name || '-';
        if (rangeEl) rangeEl.textContent = label;

        renderBars(list);
    }

    function renderBars(list) {
        const barWrap = document.getElementById('tiBarGraph');
        if (!barWrap) return;
        barWrap.innerHTML = '';

        if (!list.length) {
            barWrap.textContent = 'データがありません。後で API から読み込む想定です。';
            return;
        }

        const maxActual = Math.max(...list.map(c => Number(c.actual) || 0), 1);

        list.forEach(cat => {
            const width = Math.max(
                4,
                Math.min(100, Math.round(((Number(cat.actual) || 0) / maxActual) * 100))
            );

            const row = document.createElement('div');
            row.className = 'ti-bar-row';

            const label = document.createElement('div');
            label.className = 'ti-bar-label';
            label.textContent = cat.name || '未分類';

            const track = document.createElement('div');
            track.className = 'ti-bar-track';

            const fill = document.createElement('span');
            fill.className = 'ti-bar-fill';
            fill.style.width = `${width}%`;
            fill.style.background = `linear-gradient(90deg, ${cat.color || '#6366f1'}, #a78bfa)`;
            track.appendChild(fill);

            const value = document.createElement('div');
            value.className = 'ti-bar-value';
            value.textContent = `${(Number(cat.actual) || 0).toFixed(1)}h`;

            row.appendChild(label);
            row.appendChild(track);
            row.appendChild(value);
            barWrap.appendChild(row);
        });
    }

    function renderCategoryCards(list) {
        const wrap = document.getElementById('tiCategoryList');
        if (!wrap) return;
        wrap.innerHTML = '';

        if (!list.length) {
            wrap.textContent = 'カテゴリデータがありません。';
            return;
        }

        list.forEach(cat => {
            const actual = Number(cat.actual) || 0;
            const target = Number(cat.target) || 0;
            const percent = target > 0 ? Math.min(100, Math.round((actual / target) * 100)) : 100;
            const gap = (actual - target).toFixed(1);

            const card = document.createElement('div');
            card.className = 'ti-category-card';

            const head = document.createElement('div');
            head.className = 'ti-category-head';

            const name = document.createElement('div');
            name.className = 'ti-category-name';

            const dot = document.createElement('span');
            dot.className = 'ti-dot';
            dot.style.background = cat.color || '#6366f1';

            const title = document.createElement('span');
            title.textContent = cat.name || '未分類';

            name.appendChild(dot);
            name.appendChild(title);

            const hours = document.createElement('div');
            hours.className = 'ti-hours';
            hours.textContent = `${actual.toFixed(1)}h`;

            head.appendChild(name);
            head.appendChild(hours);

            const progress = document.createElement('div');
            progress.className = 'ti-progress';

            const bar = document.createElement('div');
            bar.className = 'ti-progress-bar';
            bar.style.width = `${percent}%`;
            bar.style.background = cat.color || '#6366f1';

            progress.appendChild(bar);

            const gapRow = document.createElement('div');
            gapRow.className = 'ti-gap';
            gapRow.innerHTML = `<span>目標 ${target.toFixed(1)}h</span><span>${gap.startsWith('-') ? '▲' : '＋'}${gap.replace('-', '')}h</span>`;

            card.appendChild(head);
            card.appendChild(progress);
            card.appendChild(gapRow);

            wrap.appendChild(card);
        });
    }
})();
