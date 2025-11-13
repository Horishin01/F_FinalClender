document.addEventListener('DOMContentLoaded', () => {
    const seedEl = document.getElementById('balance-sheet-json');
    if (!seedEl || typeof BalanceSheetWidget === 'undefined') return;
    let seed = [];
    try {
        seed = JSON.parse(seedEl.textContent || '[]');
    } catch {
        seed = [];
    }

    const root = document.querySelector('[data-balance-sheet]');
    BalanceSheetWidget.init({
        root,
        seed,
        storageKey: root?.dataset.storageKey || 'balance-sheet-state'
    });
});
