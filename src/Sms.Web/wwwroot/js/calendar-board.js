// Range selection for the academic calendar board (doc/Modules/04 §8.1 "day-type painting
// (drag ranges)").
//
// The board used to fill the From/To boxes on click and show nothing on the grid, so picking a
// two-week Eid holiday across three hundred cells meant reading two date inputs in the sidebar to
// find out what you had picked. Days the server will refuse — past ones (BR-CAL-003) and days
// outside the year — carry no data-date, so they are not selectable here at all.
//
// The two date inputs stay the source of truth: typing in them re-lights the grid exactly as
// dragging does, and the form posts what it always posted.
(function () {
    'use strict';

    var from = document.getElementById('paint-from');
    var to = document.getElementById('paint-to');
    var board = document.getElementById('calendar-board');
    if (!from || !to || !board) {
        return; // A closed year renders the board with no paint card; nothing to select into.
    }

    // :not([data-date=""]) is belt and braces — a locked cell that ever renders the attribute
    // empty must not become selectable again just because the markup slipped.
    var cells = Array.prototype.slice.call(board.querySelectorAll('.sms-cal-day[data-date]:not([data-date=""])'));
    var readout = document.getElementById('paint-count');
    var clear = document.getElementById('paint-clear');
    var dragging = false;
    var anchor = null;

    // ISO yyyy-MM-dd sorts lexicographically, which is the whole reason the cells carry it in that
    // form — no Date parsing, no timezone to get wrong on a date-only value.
    function ordered(a, b) {
        return a <= b ? [a, b] : [b, a];
    }

    function dayCount(lo, hi) {
        var a = new Date(lo + 'T00:00:00Z');
        var b = new Date(hi + 'T00:00:00Z');
        return Math.round((b - a) / 86400000) + 1;
    }

    function render() {
        var start = from.value;
        var end = to.value || start;
        var lo = null;
        var hi = null;
        if (start && end) {
            var range = ordered(start, end);
            lo = range[0];
            hi = range[1];
        }

        for (var i = 0; i < cells.length; i++) {
            var cell = cells[i];
            var date = cell.getAttribute('data-date');
            var picked = lo !== null && date >= lo && date <= hi;
            cell.classList.toggle('is-picked', picked);
            cell.setAttribute('aria-pressed', picked ? 'true' : 'false');
        }

        // The count is of calendar days in the range, not of lit cells: that is what Paint will
        // write, and a range whose middle falls outside a rendered month would otherwise
        // under-report.
        var total = lo === null ? 0 : dayCount(lo, hi);
        if (readout) {
            readout.textContent = total === 0
                ? readout.dataset.empty
                : (total === 1 ? readout.dataset.one : readout.dataset.many.replace('{0}', total));
        }
        if (clear) {
            clear.hidden = total === 0;
        }
    }

    function select(date, extend) {
        if (extend && anchor) {
            var range = ordered(anchor, date);
            from.value = range[0];
            to.value = range[1];
        } else {
            anchor = date;
            from.value = date;
            to.value = '';
        }
        render();
    }

    cells.forEach(function (cell) {
        var date = cell.getAttribute('data-date');

        cell.addEventListener('pointerdown', function (e) {
            e.preventDefault(); // Keeps a drag from selecting the day numbers as text.
            dragging = true;
            select(date, e.shiftKey);
        });

        cell.addEventListener('pointerenter', function () {
            if (dragging) {
                select(date, true);
            }
        });

        // The cells are role="button" and focusable, so they answer the keyboard the way a button
        // does. Shift+Enter extends from the anchor, matching shift-click.
        cell.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ' || e.key === 'Spacebar') {
                e.preventDefault();
                select(date, e.shiftKey);
            }
        });
    });

    // Ending the drag anywhere ends it — releasing outside the board otherwise leaves the grid
    // following the pointer.
    document.addEventListener('pointerup', function () { dragging = false; });
    document.addEventListener('pointercancel', function () { dragging = false; });

    from.addEventListener('change', function () { anchor = from.value || null; render(); });
    to.addEventListener('change', render);

    if (clear) {
        clear.addEventListener('click', function () {
            from.value = '';
            to.value = '';
            anchor = null;
            render();
        });
    }

    render();
})();
