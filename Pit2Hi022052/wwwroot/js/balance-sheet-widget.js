(function (global) {
    const STORAGE_VERSION = 1;

    function init(options) {
        const root = options?.root;
        if (!root) return;
        const storageKey = options.storageKey || 'balance-sheet-state';
        root.dataset.storageKey = storageKey;
        const seed = Array.isArray(options.seed) ? options.seed : [];
        const state = loadState(storageKey, seed);
        render(root, state);
        bind(root, state, storageKey);
    }

    function loadState(key, seed) {
        const stored = readStorage(key);
        if (stored && stored.version === STORAGE_VERSION) {
            return stored;
        }
        return {
            version: STORAGE_VERSION,
            assets: seed.filter(i => i.category === 0 || i.category === 'asset'),
            liabilities: seed.filter(i => i.category === 1 || i.category === 'liability')
        };
    }

    function readStorage(key) {
        try {
            const raw = localStorage.getItem(key);
            if (!raw) return null;
            return JSON.parse(raw);
        } catch {
            return null;
        }
    }

    function writeStorage(key, state) {
        try {
            localStorage.setItem(key, JSON.stringify(state));
        } catch {
            /* noop */
        }
    }

    function bind(root, state, storageKey) {
        const form = root.querySelector('[data-role="add-form"]');
        form?.addEventListener('submit', (e) => {
            e.preventDefault();
            const data = new FormData(form);
            const category = data.get('category') === 'liability' ? 'liabilities' : 'assets';
            const name = (data.get('name') || '').toString().trim();
            const tag = (data.get('tag') || '').toString().trim();
            const amount = Number(data.get('amount'));
            if (!name || Number.isNaN(amount)) return;
            state[category].push({
                id: `custom-${Date.now()}`,
                name,
                tag,
                amount,
                category: category === 'assets' ? 0 : 1
            });
            writeStorage(storageKey, state);
            form.reset();
            render(root, state);
        });

        root.querySelectorAll('[data-role="scenario"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const type = btn.dataset.scenario;
                applyScenario(state, type);
                writeStorage(storageKey, state);
                render(root, state);
            });
        });

        root.querySelectorAll('[data-role="download-chart"]').forEach(btn => {
            btn.addEventListener('click', () => downloadChart(root));
        });

        root.querySelectorAll('[data-role="download-chart"]').forEach(btn => {
            btn.addEventListener('click', () => downloadChart(root));
        });
    }

    function applyScenario(state, type) {
        if (type === 'cash') {
            bump(state.assets, 0.05);
        } else if (type === 'debt') {
            bump(state.liabilities, -0.05);
        } else if (type === 'aggressive') {
            bump(state.assets, 0.08);
        }
    }

    function bump(list, ratio) {
        list.forEach(item => {
            item.amount = Math.max(0, item.amount + item.amount * ratio);
        });
    }

    function render(root, state) {
        const assetsBody = root.querySelector('[data-table="assets"]');
        const liabilitiesBody = root.querySelector('[data-table="liabilities"]');

        const assetsTotal = sum(state.assets);
        const liabilitiesTotal = sum(state.liabilities);
        const net = assetsTotal - liabilitiesTotal;
        const ratio = assetsTotal === 0 ? 0 : Math.min(100, Math.round((liabilitiesTotal / assetsTotal) * 100));

        setText(root, '[data-role="summary-assets"]', formatCurrency(assetsTotal));
        setText(root, '[data-role="summary-liabilities"]', formatCurrency(liabilitiesTotal));
        setText(root, '[data-role="summary-net"]', formatCurrency(net));
        setText(root, '[data-role="summary-ratio"]', `${ratio}%`);

        setWidth(root, '[data-role="progress-assets"]', Math.min(100, Math.round((assetsTotal / (assetsTotal + liabilitiesTotal || 1)) * 100)));
        setWidth(root, '[data-role="progress-liabilities"]', ratio);

        if (assetsBody) assetsBody.innerHTML = renderRows(state.assets);
        if (liabilitiesBody) liabilitiesBody.innerHTML = renderRows(state.liabilities);

        root.querySelectorAll('[data-action="remove"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const category = btn.dataset.category;
                const id = btn.dataset.id;
                if (!category || !id) return;
                state[category] = state[category].filter(item => item.id !== id);
                writeStorage(root.dataset.storageKey || 'balance-sheet-state', state);
                render(root, state);
            });
        });

        drawChart(root, assetsTotal, liabilitiesTotal, net);

        drawChart(root, assetsTotal, liabilitiesTotal, net);
    }

    function renderRows(list) {
        if (!list.length) {
            return `<tr><td colspan="4" class="text-muted">まだ項目がありません</td></tr>`;
        }
        return list.map(item => `
            <tr>
                <td>${escapeHtml(item.name)}</td>
                <td>${escapeHtml(item.tag || '-')}</td>
                <td class="text-end">${formatCurrency(item.amount)}</td>
                <td class="text-end">
                    <button type="button" data-action="remove" data-category="${item.category === 0 ? 'assets' : 'liabilities'}" data-id="${item.id}">
                        <i class="fa-solid fa-xmark"></i>
                    </button>
                </td>
            </tr>`).join('');
    }

    function escapeHtml(value) {
        return (value || '').toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function sum(list) {
        return list.reduce((acc, item) => acc + Number(item.amount || 0), 0);
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('ja-JP', { style: 'currency', currency: 'JPY', maximumFractionDigits: 0 }).format(value || 0);
    }

    function setText(root, selector, text) {
        const el = root.querySelector(selector);
        if (el) el.textContent = text;
    }

    function setWidth(root, selector, percent) {
        const el = root.querySelector(selector);
        if (el) el.style.width = `${percent}%`;
    }

    function drawChart(root, assets, liabilities, net) {
        const canvas = root.querySelector('[data-role="bs-chart"]');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        ctx.clearRect(0, 0, width, height);

        const total = Math.max(assets + liabilities + Math.max(net, 0), 1);
        const leftWidth = Math.round(width * 0.55);
        const rightWidth = width - leftWidth - 20;

        if (assets === 0 && liabilities === 0 && net === 0) {
            ctx.fillStyle = '#94a3b8';
            ctx.fillRect(10, 10, leftWidth, height - 20);
            ctx.fillStyle = '#cbd5f5';
            ctx.fillRect(leftWidth + 20, 10, rightWidth, (height - 30) / 2);
            ctx.fillStyle = '#d8b4fe';
            ctx.fillRect(leftWidth + 20, (height / 2) + 5, rightWidth, (height - 30) / 2);
            ctx.fillStyle = '#ffffff';
            ctx.font = 'bold 16px Segoe UI';
            ctx.fillText('資産', 22, height / 2);
            ctx.fillText('負債', leftWidth + 30, 30);
            ctx.fillText('純資産', leftWidth + 30, height - 24);
            return;
        }

        const assetHeight = Math.max(30, Math.round((assets / total) * (height - 20)));
        ctx.fillStyle = '#38bdf8';
        ctx.fillRect(10, height - assetHeight - 10, leftWidth, assetHeight);
        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 16px Segoe UI';
        ctx.fillText('資産', 20, height - assetHeight);

        const liabilityHeight = Math.max(25, Math.round((liabilities / total) * (height - 20)));
        ctx.fillStyle = '#f97316';
        ctx.fillRect(leftWidth + 20, 10, rightWidth, liabilityHeight);
        ctx.fillStyle = '#ffffff';
        ctx.fillText('負債', leftWidth + 30, 30);

        const netHeight = Math.max(25, Math.round((Math.max(net, 0) / total) * (height - 20)));
        ctx.fillStyle = '#c084fc';
        ctx.fillRect(leftWidth + 20, 10 + liabilityHeight + 10, rightWidth, netHeight);
        ctx.fillStyle = '#ffffff';
        ctx.fillText('純資産', leftWidth + 30, 15 + liabilityHeight + 30);
    }

    function downloadChart(root) {
        const canvas = root.querySelector('[data-role="bs-chart"]');
        if (!canvas) return;
        const link = document.createElement('a');
        link.href = canvas.toDataURL('image/png', 1.0);
        link.download = `balance-sheet-${new Date().toISOString().slice(0, 10)}.png`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    global.BalanceSheetWidget = { init };
})(window);
