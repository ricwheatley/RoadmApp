/* eslint-disable no-undef */
/**
 * data-load-logs.js
 * Uses Bootstrap 5's Collapse API to expand and collapse rows.
 */

function initDataLoadLogs() {
    if (window._DLInit) return;
    window._DLInit = true;

    const table = document.querySelector('table.table');
    if (!table) return;

    table.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-bs-toggle="collapse"],[data-toggle="collapse"]');
        if (!btn) return;

        const targetSel = btn.getAttribute('data-bs-target') || btn.getAttribute('data-target');
        const target = document.querySelector(targetSel);
        if (!target) return;

        // jQuery-based toggle (works in Bootstrap 4 and 5)
        const collapse = bootstrap.Collapse.getOrCreateInstance(target);
        collapse.toggle();

        target.addEventListener('shown.bs.collapse', () => { btn.textContent = '–'; }, { once: true });
        target.addEventListener('hidden.bs.collapse', () => { btn.textContent = '+'; }, { once: true });
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initDataLoadLogs);
} else {
    initDataLoadLogs();
}
