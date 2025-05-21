/* eslint-disable no-undef */
/**
 * data-load-logs.js
 * Works with Bootstrap 4.x and old 5.x bundles.
 * Uses jQuery’s $(...).collapse('toggle') API.
 */

function initDataLoadLogs() {
    if (window._DLInit) return;
    window._DLInit = true;

    const table = document.querySelector('table.table');
    if (!table) return;

    table.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-bs-toggle="collapse"]');
        if (!btn) return;

        const targetSel = btn.getAttribute('data-bs-target');
        const target = document.querySelector(targetSel);
        if (!target) return;

        // jQuery-based toggle (works in Bootstrap 4 and 5)
        $(target).collapse('toggle');

        $(target).one('shown.bs.collapse', () => { btn.textContent = '–'; });
        $(target).one('hidden.bs.collapse', () => { btn.textContent = '+'; });
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initDataLoadLogs);
} else {
    initDataLoadLogs();
}
