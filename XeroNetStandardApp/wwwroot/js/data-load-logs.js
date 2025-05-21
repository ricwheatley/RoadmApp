/* eslint-disable no-undef */
/**
 * data-load-logs.js
 * Handles “+ / –” toggle and row collapse on the Data-load logs page.
 *
 * Depends on: bootstrap.bundle.min.js (for Collapse), no jQuery needed.
 */
document.addEventListener('DOMContentLoaded', () => {
  // Event delegation so it works for every button in the table
  document.querySelector('table.table').addEventListener('click', (e) => {
    const btn = e.target.closest('[data-bs-toggle="collapse"]');
    if (!btn) return;                // not a toggle button

    const targetSel = btn.getAttribute('data-bs-target');
    const target    = document.querySelector(targetSel);
    if (!target) return;

    // When the collapse finishes opening, swap to "–"
    target.addEventListener('shown.bs.collapse', () => { btn.textContent = '–'; }, { once: true });
    // When the collapse finishes closing, swap back to "+"
    target.addEventListener('hidden.bs.collapse', () => { btn.textContent = '+'; }, { once: true });
  });
});
