/* eslint-disable no-undef */
// DataTables + jQuery implementation of expandable rows for the #roadmap table.
// Requires: jquery.js, datatables.js already loaded on the page.

import 'datatables.net';

function formatChildRow (data) {
  /* Build the HTML that appears when a row is expanded.
     Adjust fields to match your JSON. */
  return `
    <table class="table table-sm mb-0">
      <tr><th class="w-25">Description</th><td>${data.description ?? ''}</td></tr>
      <tr><th>Start</th><td>${data.startDate ?? ''}</td></tr>
      <tr><th>End</th><td>${data.endDate ?? ''}</td></tr>
    </table>`;
}

$(document).ready(function () {
  const table = $('#roadmap').DataTable({
    ajax: '/Roadmap/GetRoadmapItems',          // adjust to your API
    serverSide: true,
    processing: true,
    order: [[1, 'asc']],
    columns: [
      {
        className: 'dt-control',
        orderable: false,
        data: null,
        defaultContent: '',                    // plus/minus icon is injected by DataTables CSS
      },
      { data: 'title'       },
      { data: 'startDate'   },
      { data: 'endDate'     },
      { data: 'status'      }
    ],
  });

  // Delegate the click so it works after paging / redraw.
  $('#roadmap tbody').on('click', 'td.dt-control', function () {
    const tr  = $(this).closest('tr');
    const row = table.row(tr);

    if (row.child.isShown()) {
      // Row is open – close it
      row.child.hide();
      tr.removeClass('shown');
    } else {
      // Row is closed – open it
      row.child(formatChildRow(row.data())).show();
      tr.addClass('shown');
    }
  });
});
