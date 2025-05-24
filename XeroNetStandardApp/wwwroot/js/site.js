// site.js

// For .last-polled
function formatLastPolledDates(selector = '.last-polled') {
    document.querySelectorAll(selector).forEach(function (cell) {
        const tid = cell.dataset.tenant;
        let val = tid ? localStorage.getItem('pollLast_' + tid) : null;
        if (val && !isNaN(val)) {
            // If saved as integer timestamp
            val = parseInt(val, 10);
        }
        const date = val ? new Date(val) : null;
        cell.textContent = date && !isNaN(date)
            ? date.toLocaleString('en-GB', {
                day: '2-digit',
                month: 'short',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            }).replace(',', '')
            : 'Not updated yet';
    });
}

// For .last-rows
function formatLastRowsInserted(selector = '.last-rows') {
    document.querySelectorAll(selector).forEach(function (cell) {
        const tid = cell.dataset.tenant;
        const val = localStorage.getItem('pollRows_' + tid);
        const rows = parseInt(val, 10);
        cell.textContent = !isNaN(rows) ? rows : '—';
    });
}

/* Formats any element with a data-timestamp attribute that holds
a UTC ISO string trimmed to milliseconds (yyyy-MM-ddTHH:mm:ss.fffZ). */
function formatIsoDates(selector = '.last-run') {
    document.querySelectorAll(selector).forEach(cell => {
        const iso = cell.dataset.timestamp;
        if (!iso) { cell.textContent = 'Not updated yet'; return; }

        const date = new Date(iso);               // always parses
        if (isNaN(date)) { cell.textContent = 'Not updated yet'; return; }

        cell.textContent = date.toLocaleString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).replace(',', '');
    });
}
