// The one upload box in the product (doc 10 §5). Every file the system takes — a student's
// photograph, a birth certificate, an employee contract — is chosen through this behaviour, so
// there is one place where "you picked the wrong file" can still be caught by the person who
// picked it.
//
// Nothing here is a security check. The server refuses what it refuses (AttachmentIntake, which
// also reads the first bytes rather than trusting the name); this only spares somebody a round
// trip and shows them what they chose. A page whose script never loaded still uploads correctly —
// the file input is a real input, not something drawn by this file.
(function () {
    'use strict';

    var IMAGE_TYPES = /^image\/(jpeg|png)$/i;

    // The units come from the page, not from this file: the same number is "م.ب" on an Arabic
    // screen and "MB" on an English one, and a size written by script must not be the one place
    // English leaks into the Arabic UI.
    function formatBytes(panel, bytes) {
        if (bytes >= 1024 * 1024) {
            return (bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, '') + ' ' + (panel.getAttribute('data-unit-mb') || 'MB');
        }
        return Math.max(1, Math.round(bytes / 1024)) + ' ' + (panel.getAttribute('data-unit-kb') || 'KB');
    }

    // The accept list read back as format names, so a refusal can say what would have been taken
    // instead of only what was not.
    function formatNames(accept) {
        var seen = [];
        (accept || '').split(',').forEach(function (token) {
            token = token.trim().toLowerCase();
            if (token.charAt(0) !== '.') { return; }
            var name = token.slice(1).toUpperCase();
            if (name === 'JPG') { name = 'JPEG'; }
            if (seen.indexOf(name) < 0) { seen.push(name); }
        });
        return seen.length ? seen.join(' · ') : '—';
    }

    function fill(panel, template, accept, maxBytes) {
        return (template || '')
            .replace('{formats}', formatNames(accept))
            .replace('{size}', formatBytes(panel, maxBytes));
    }

    // The accept list is the same string the input carries, so the warning shown here and the
    // picker's own filter can never disagree.
    function accepted(panel, file) {
        var accept = (panel.getAttribute('data-accept') || '').toLowerCase();
        if (!accept) { return true; }

        var name = (file.name || '').toLowerCase();
        var type = (file.type || '').toLowerCase();
        return accept.split(',').some(function (token) {
            token = token.trim();
            if (!token) { return false; }
            return token.charAt(0) === '.' ? name.slice(-token.length) === token : type === token;
        });
    }

    function wire(panel) {
        var input = panel.querySelector('[data-sms-upload-input]');
        if (!input) { return; }

        var drop = panel.querySelector('[data-sms-upload-drop]');
        var image = panel.querySelector('[data-sms-upload-image]');
        var glyph = panel.querySelector('[data-sms-upload-glyph]');
        var nameOut = panel.querySelector('[data-sms-upload-name]');
        var clear = panel.querySelector('[data-sms-upload-clear]');
        var error = panel.querySelector('[data-sms-upload-error]');
        var hint = panel.querySelector('[data-sms-upload-hint]');
        var maxBytes = parseInt(panel.getAttribute('data-max-bytes'), 10) || 0;

        // What the frame showed on arrival: the photograph already on file, where there is one.
        // Clearing a choice has to put that back rather than empty the frame, or removing a
        // mistaken pick would look like removing the photograph itself.
        var originalSrc = image && !image.hidden ? image.getAttribute('src') : null;
        var objectUrl = null;

        function releasePreview() {
            if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
        }

        function showError(message) {
            if (!error) { return; }
            error.textContent = message || '';
            error.hidden = !message;
        }

        function restoreFrame() {
            if (!image) { return; }
            releasePreview();
            if (originalSrc) {
                image.setAttribute('src', originalSrc);
                image.hidden = false;
                if (glyph) { glyph.hidden = true; }
            } else {
                image.removeAttribute('src');
                image.hidden = true;
                if (glyph) { glyph.hidden = false; }
            }
        }

        function reset() {
            input.value = '';
            restoreFrame();
            if (nameOut) { nameOut.textContent = ''; nameOut.hidden = true; }
            if (clear) { clear.hidden = true; }
            panel.classList.remove('has-file');
            showError('');
        }

        function show(file) {
            showError('');

            var accept = panel.getAttribute('data-accept');

            if (maxBytes && file.size > maxBytes) {
                reset();
                showError(fill(panel, panel.getAttribute('data-error-too-large'), accept, maxBytes));
                return;
            }

            if (!accepted(panel, file)) {
                reset();
                showError(fill(panel, panel.getAttribute('data-error-format'), accept, maxBytes));
                return;
            }

            if (nameOut) {
                nameOut.textContent = file.name + ' · ' + formatBytes(panel, file.size);
                nameOut.hidden = false;
            }
            if (clear) { clear.hidden = false; }
            panel.classList.add('has-file');

            // Only an image can be previewed. A PDF is named and measured instead — drawing a
            // generic page icon and calling it a preview tells the reader nothing they did not
            // already know from the file name.
            releasePreview();
            if (image && IMAGE_TYPES.test(file.type || '')) {
                objectUrl = URL.createObjectURL(file);
                image.setAttribute('src', objectUrl);
                image.hidden = false;
                if (glyph) { glyph.hidden = true; }
            } else {
                restoreFrame();
            }
        }

        input.addEventListener('change', function () {
            if (input.files && input.files.length) { show(input.files[0]); } else { reset(); }
        });

        // Where the document type is picked beside the file, the chosen type's own formats and
        // ceiling replace the panel's opening guess — and the file picker's filter with them, so
        // the dialog stops offering what this type would not have taken.
        var rulesFrom = panel.getAttribute('data-rules-from');
        var rules = rulesFrom ? document.getElementById(rulesFrom) : null;
        if (rules) {
            var applyRules = function () {
                var option = rules.options[rules.selectedIndex];
                if (!option) { return; }

                var accept = option.getAttribute('data-accept');
                var limit = parseInt(option.getAttribute('data-max-bytes'), 10) || 0;
                if (accept) { panel.setAttribute('data-accept', accept); input.setAttribute('accept', accept); }
                if (limit) { maxBytes = limit; panel.setAttribute('data-max-bytes', String(limit)); }
                if (hint) { hint.textContent = fill(panel, panel.getAttribute('data-hint-template'), accept, maxBytes); }

                // A file already chosen under the previous type is re-judged rather than left
                // sitting in a box whose rules have moved underneath it.
                if (input.files && input.files.length) { show(input.files[0]); }
            };

            rules.addEventListener('change', applyRules);
            applyRules();
        }

        if (clear) {
            // The button sits inside the label, so its click would otherwise re-open the picker
            // the moment the choice was cleared.
            clear.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                reset();
            });
        }

        if (drop && window.DataTransfer) {
            ['dragenter', 'dragover'].forEach(function (name) {
                drop.addEventListener(name, function (e) {
                    e.preventDefault();
                    panel.classList.add('is-dragging');
                });
            });
            ['dragleave', 'dragend', 'drop'].forEach(function (name) {
                drop.addEventListener(name, function () { panel.classList.remove('is-dragging'); });
            });
            drop.addEventListener('drop', function (e) {
                if (!e.dataTransfer || !e.dataTransfer.files || !e.dataTransfer.files.length) { return; }
                e.preventDefault();

                // Assigning the dropped file back into the input is what makes a drop and a click
                // the same act: the form still posts one ordinary file field either way.
                var transfer = new DataTransfer();
                transfer.items.add(e.dataTransfer.files[0]);
                input.files = transfer.files;
                show(transfer.files[0]);
            });
        }

        window.addEventListener('pagehide', releasePreview);
    }

    document.querySelectorAll('[data-sms-upload]').forEach(wire);
})();
