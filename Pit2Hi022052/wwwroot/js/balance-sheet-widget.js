(function (global) {
    const STORAGE_VERSION = 1;
    const DETAIL_LIMIT = 5;

    function init(options) {
        const root = options?.root;
        if (!root) return;
        const storageKey = options.storageKey || 'balance-sheet-state';
        root.dataset.storageKey = storageKey;
        const seed = Array.isArray(options.seed) ? options.seed : [];
        const state = loadState(storageKey, seed);
        render(root, state);
        bind(root, state, storageKey, seed);
    }

    function loadState(storageKey, seed) {
        const stored = readStorage(storageKey);
        const hasSeed = Array.isArray(seed) && seed.length > 0;
        if (stored && stored.version === STORAGE_VERSION) {
            const hasStoredData = (stored.assets?.length || 0) + (stored.liabilities?.length || 0) > 0;
            if (hasStoredData || !hasSeed) {
                stored.assets = Array.isArray(stored.assets) ? stored.assets : [];
                stored.liabilities = Array.isArray(stored.liabilities) ? stored.liabilities : [];
                return stored;
            }
        }
        const initial = {
            version: STORAGE_VERSION,
            assets: normalizeList(seed.filter(isAsset), 'assets'),
            liabilities: normalizeList(seed.filter(isLiability), 'liabilities')
        };
        writeStorage(storageKey, initial);
        return initial;
    }

    function normalizeList(list, type) {
        return list.map(item => ({
            id: item.id || item.Id || `${type}-${Math.random().toString(36).slice(2)}`,
            name: (item.name || item.Name || '未設定').toString(),
            tag: (item.tag || item.Tag || '').toString(),
            amount: Number(item.amount ?? item.Amount ?? 0),
            category: type === 'assets' ? 0 : 1
        }));
    }

    function isAsset(item) {
        const value = item?.category ?? item?.Category;
        return value === 0 || value === 'asset' || value === 'Asset';
    }

    function isLiability(item) {
        const value = item?.category ?? item?.Category;
        return value === 1 || value === 'liability' || value === 'Liability';
    }

    function readStorage(key) {
        try {
            const raw = localStorage.getItem(key);
            return raw ? JSON.parse(raw) : null;
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

    function bind(root, state, storageKey, seed) {
        const form = root.querySelector('[data-role="add-form"]');
        form?.addEventListener('submit', (e) => {
            e.preventDefault();
            const data = new FormData(form);
            const bucket = data.get('category') === 'liability' ? 'liabilities' : 'assets';
            const name = (data.get('name') || '').toString().trim();
            const tag = (data.get('tag') || '').toString().trim();
            const amount = Number(data.get('amount'));
            if (!name || Number.isNaN(amount) || amount < 0) return;
            state[bucket].push({
                id: `custom-${Date.now()}`,
                name,
                tag,
                amount,
                category: bucket === 'assets' ? 0 : 1
            });
            writeStorage(storageKey, state);
            form.reset();
            render(root, state);
        });

        root.querySelectorAll('[data-role="scenario"]').forEach(button => {
            button.addEventListener('click', () => {
                applyScenario(state, button.dataset.scenario);
                writeStorage(storageKey, state);
                render(root, state);
            });
        });

        root.querySelectorAll('[data-role="detail-toggle"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const target = btn.dataset.detail === 'liabilities' ? 'liabilities' : 'assets';
                if (root.dataset.detailActive === target) return;
                root.dataset.detailActive = target;
                renderDetail(root, state, target);
            });
        });

        root.querySelectorAll('[data-role="download-chart"]').forEach(btn => {
            btn.addEventListener('click', () => downloadChart(root));
        });

        root.querySelectorAll('[data-role="reset-state"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const fresh = loadState(storageKey, seed);
                state.assets = [...fresh.assets];
                state.liabilities = [...fresh.liabilities];
                render(root, fresh);
                writeStorage(storageKey, fresh);
            });
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
        const assetsTotal = sum(state.assets);
        const liabilitiesTotal = sum(state.liabilities);
        const net = assetsTotal - liabilitiesTotal;
        const ratio = assetsTotal === 0 ? null : Math.min(100, Math.round((liabilitiesTotal / assetsTotal) * 100));

        setText(root, '[data-role="summary-assets"]', formatCurrency(assetsTotal));
        setText(root, '[data-role="summary-liabilities"]', formatCurrency(liabilitiesTotal));
        setText(root, '[data-role="summary-net"]', formatCurrency(net));
        setText(root, '[data-role="summary-ratio"]', ratio === null ? '--' : `${ratio}%`);

        const allocation = assetsTotal + liabilitiesTotal === 0 ? 0 : Math.round((assetsTotal / (assetsTotal + liabilitiesTotal)) * 100);
        setWidth(root, '[data-role="progress-assets"]', allocation);
        setWidth(root, '[data-role="progress-liabilities"]', ratio ?? 0);

        const assetsBody = root.querySelector('[data-table="assets"]');
        const liabilitiesBody = root.querySelector('[data-table="liabilities"]');
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

        const activeDetail = root.dataset.detailActive === 'liabilities' ? 'liabilities' : 'assets';
        root.dataset.detailActive = activeDetail;
        renderDetail(root, state, activeDetail);
        drawChart(root, assetsTotal, liabilitiesTotal, net);
    }

    function renderDetail(root, state, type) {
        updateDetailTabs(root, type);
        renderDetailList(root, state[type], sum(state[type]), type);
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

    function updateDetailTabs(root, active) {
        root.querySelectorAll('[data-role="detail-toggle"]').forEach(btn => {
            const isActive = btn.dataset.detail === active;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });
    }

    function renderDetailList(root, list, total, type) {
        const container = root.querySelector('[data-role="detail-list"]');
        if (!container) return;
        container.dataset.detailType = type;
        if (!Array.isArray(list) || !list.length || !total) {
            container.innerHTML = '<p class="detail-empty">まだ内訳がありません</p>';
            return;
        }
        container.innerHTML = buildDetailItems(list, total);
    }

    function buildDetailItems(list, total) {
        const sorted = [...list].sort((a, b) => Number(b.amount || 0) - Number(a.amount || 0));
        const rows = [];
        let otherTotal = 0;
        sorted.forEach((item, index) => {
            if (index < DETAIL_LIMIT - 1) {
                rows.push(renderDetailItem(item.name, item.amount, (item.amount / total) * 100));
            } else {
                otherTotal += Number(item.amount || 0);
            }
        });
        if (otherTotal > 0) {
            rows.push(renderDetailItem('その他', otherTotal, (otherTotal / total) * 100));
        }
        return rows.join('');
    }

    function renderDetailItem(label, value, percent) {
        const safePercent = Math.max(0, Math.min(percent || 0, 100));
        return `
            <div class="bs-detail-item">
                <div class="detail-labels">
                    <p>${escapeHtml(label || '-')}</p>
                    <span>${formatPercent(percent)}</span>
                </div>
                <div class="detail-meter">
                    <span style="width:${safePercent}%"></span>
                </div>
                <strong>${formatCurrency(value)}</strong>
            </div>`;
    }

    function drawChart(root, assets, liabilities, net) {
        const canvas = root.querySelector('[data-role="bs-chart"]');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        ctx.clearRect(0, 0, width, height);

        const padding = 18;
        const containerX = padding;
        const containerY = padding;
        const containerWidth = width - padding * 2;
        const containerHeight = height - padding * 2;
        const headerHeight = 44;
        const footerHeight = 28;
        const innerPadding = 22;
        const columnTop = containerY + headerHeight;
        const columnBottom = containerY + containerHeight - footerHeight;
        const columnHeight = columnBottom - columnTop;
        const dividerX = containerX + containerWidth / 2;
        const columnWidth = dividerX - containerX - innerPadding * 1.3;
        const leftX = containerX + innerPadding;
        const rightX = dividerX + innerPadding / 2;

        ctx.fillStyle = '#f8fafc';
        drawRoundedRect(ctx, containerX, containerY, containerWidth, containerHeight, 32);

        ctx.strokeStyle = 'rgba(148, 163, 184, 0.45)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(dividerX, columnTop - 10);
        ctx.lineTo(dividerX, columnBottom + 6);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(containerX + 14, columnTop - 12);
        ctx.lineTo(containerX + containerWidth - 14, columnTop - 12);
        ctx.stroke();

        ctx.fillStyle = '#0f172a';
        ctx.textBaseline = 'top';
        ctx.font = '700 15px "Segoe UI", "Noto Sans JP"';
        ctx.fillText('資産 = 負債 + 純資産', containerX + innerPadding, containerY + 6);
        ctx.font = '600 13px "Segoe UI", "Noto Sans JP"';
        ctx.fillText(`${formatCurrency(assets)} = ${formatCurrency(liabilities)} + ${formatCurrency(net)}`, containerX + innerPadding, containerY + 24);

        const safeAssets = Math.max(assets, 0);
        const safeLiabilities = Math.max(liabilities, 0);
        const equity = net;
        const positiveEquity = Math.max(equity, 0);
        const deficit = Math.abs(Math.min(equity, 0));
        const stackTotal = safeLiabilities + positiveEquity + deficit;
        const scaleBase = Math.max(safeAssets, stackTotal, 1);

        if (safeAssets === 0 && stackTotal === 0) {
            ctx.fillStyle = '#94a3b8';
            ctx.font = '600 16px "Segoe UI", "Noto Sans JP"';
            ctx.textBaseline = 'middle';
            ctx.fillText('データを追加すると貸借対照表が描画されます', leftX, columnTop + columnHeight / 2);
            return;
        }

        const assetsHeight = safeAssets ? Math.max((safeAssets / scaleBase) * columnHeight, 42) : 0;
        const liabilitiesHeight = safeLiabilities ? Math.max((safeLiabilities / scaleBase) * columnHeight, 32) : 0;
        const equityHeight = positiveEquity ? Math.max((positiveEquity / scaleBase) * columnHeight, 26) : 0;
        const deficitHeight = deficit ? Math.max((deficit / scaleBase) * columnHeight, 26) : 0;
        const rightTotalHeight = liabilitiesHeight + equityHeight + deficitHeight;
        const rightTopEdge = columnTop;
        const rightBottomEdge = columnTop + rightTotalHeight;

        if (assetsHeight > 0) {
            const leftWidth = columnWidth - 12;
            const leftY = columnBottom - assetsHeight;
            ctx.fillStyle = '#38bdf8';
            drawRoundedRect(ctx, leftX, leftY, leftWidth, assetsHeight, 28);
            drawChartLabel(ctx, '資産', formatCurrency(assets), leftX + 18, leftY + 18);

            if (rightTotalHeight > 0) {
                ctx.strokeStyle = 'rgba(14, 165, 233, 0.45)';
                ctx.setLineDash([6, 4]);
                ctx.lineWidth = 1.5;
                ctx.beginPath();
                ctx.moveTo(leftX + leftWidth + 6, leftY + 8);
                ctx.lineTo(rightX - 6, rightTopEdge + 6);
                ctx.stroke();
                ctx.beginPath();
                ctx.moveTo(leftX + leftWidth + 6, leftY + assetsHeight - 8);
                ctx.lineTo(rightX - 6, rightBottomEdge - 6);
                ctx.stroke();
                ctx.setLineDash([]);
            }
        }

        let cursor = columnTop;
        const rightWidth = columnWidth - 12;
        if (liabilitiesHeight > 0) {
            ctx.fillStyle = '#f97316';
            drawRoundedRect(ctx, rightX, cursor, rightWidth, liabilitiesHeight, {
                tl: 28,
                tr: 28,
                br: equityHeight > 0 || deficitHeight > 0 ? 10 : 28,
                bl: equityHeight > 0 || deficitHeight > 0 ? 10 : 28
            });
            drawChartLabel(ctx, '負債', formatCurrency(liabilities), rightX + 18, cursor + 18);
            cursor += liabilitiesHeight;
        }

        if (equityHeight > 0) {
            ctx.fillStyle = '#c084fc';
            drawRoundedRect(ctx, rightX, cursor, rightWidth, equityHeight, {
                tl: 10,
                tr: 10,
                br: deficitHeight > 0 ? 10 : 28,
                bl: deficitHeight > 0 ? 10 : 28
            });
            drawChartLabel(ctx, '純資産', formatCurrency(positiveEquity), rightX + 18, cursor + 16);
            cursor += equityHeight;
        } else {
            const placeholderHeight = 26;
            const placeholderY = Math.min(columnBottom - placeholderHeight, cursor + 6);
            ctx.strokeStyle = 'rgba(148, 163, 184, 0.6)';
            ctx.setLineDash([4, 4]);
            ctx.strokeRect(rightX, placeholderY, rightWidth, placeholderHeight);
            ctx.setLineDash([]);
            drawChartLabel(ctx, '純資産', formatCurrency(0), rightX + 18, placeholderY + 4, '#475569');
        }

        if (deficitHeight > 0) {
            ctx.fillStyle = '#f43f5e';
            drawRoundedRect(ctx, rightX, cursor, rightWidth, deficitHeight, {
                tl: cursor === columnTop ? 28 : 10,
                tr: cursor === columnTop ? 28 : 10,
                br: 28,
                bl: 28
            });
            drawChartLabel(ctx, '純資産不足', formatCurrency(Math.abs(equity)), rightX + 18, cursor + 16);
        }
    }

    function drawRoundedRect(ctx, x, y, width, height, radius) {
        if (width <= 0 || height <= 0) return;
        const base = typeof radius === 'number'
            ? { tl: radius, tr: radius, br: radius, bl: radius }
            : {
                tl: radius.tl ?? 0,
                tr: radius.tr ?? 0,
                br: radius.br ?? 0,
                bl: radius.bl ?? 0
            };
        const limit = Math.min(width / 2, height / 2);
        base.tl = Math.min(base.tl, limit);
        base.tr = Math.min(base.tr, limit);
        base.br = Math.min(base.br, limit);
        base.bl = Math.min(base.bl, limit);

        ctx.beginPath();
        ctx.moveTo(x + base.tl, y);
        ctx.lineTo(x + width - base.tr, y);
        ctx.quadraticCurveTo(x + width, y, x + width, y + base.tr);
        ctx.lineTo(x + width, y + height - base.br);
        ctx.quadraticCurveTo(x + width, y + height, x + width - base.br, y + height);
        ctx.lineTo(x + base.bl, y + height);
        ctx.quadraticCurveTo(x, y + height, x, y + height - base.bl);
        ctx.lineTo(x, y + base.tl);
        ctx.quadraticCurveTo(x, y, x + base.tl, y);
        ctx.closePath();
        ctx.fill();
    }

    function drawChartLabel(ctx, label, value, x, y, color = '#ffffff') {
        ctx.save();
        ctx.fillStyle = color;
        ctx.textBaseline = 'top';
        ctx.font = '600 13px "Segoe UI", "Noto Sans JP"';
        ctx.fillText(label, x, y);
        ctx.font = '700 16px "Segoe UI", "Noto Sans JP"';
        ctx.fillText(value, x, y + 18);
        ctx.restore();
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

    function sum(list) {
        return list.reduce((acc, item) => acc + Number(item.amount || 0), 0);
    }

    function setText(root, selector, value) {
        const el = root.querySelector(selector);
        if (el) el.textContent = value;
    }

    function setWidth(root, selector, percent) {
        const el = root.querySelector(selector);
        if (el) el.style.width = `${Math.max(0, Math.min(100, percent || 0))}%`;
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('ja-JP', { style: 'currency', currency: 'JPY', maximumFractionDigits: 0 }).format(value || 0);
    }

    function formatPercent(value) {
        if (!Number.isFinite(value) || value <= 0) {
            return '0%';
        }
        if (value >= 10) {
            return `${Math.round(value)}%`;
        }
        return `${value.toFixed(1)}%`;
    }

    function escapeHtml(value) {
        return (value || '').toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    global.BalanceSheetWidget = { init };
})(window);
