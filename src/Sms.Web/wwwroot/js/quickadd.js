// "➕ add lookup value" next to a drop-down (pairs with Views/Shared/_QuickAddLookup.cshtml).
// Button attributes: data-quickadd-category (lookup category code),
// data-quickadd-target (CSS selector of the <select> to extend),
// data-quickadd-title (modal heading, optional).
(function () {
    document.addEventListener('DOMContentLoaded', function () {
        var modalEl = document.getElementById('sms-quickadd');
        var form = document.getElementById('sms-quickadd-form');
        if (!modalEl || !form || !window.bootstrap) { return; }

        var modal = new bootstrap.Modal(modalEl);
        var target = null;
        var errorBox = document.getElementById('sms-quickadd-error');
        var isRtl = (document.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';

        function showError(msg) { errorBox.textContent = msg; errorBox.classList.remove('d-none'); }
        function clearError() { errorBox.textContent = ''; errorBox.classList.add('d-none'); }

        document.querySelectorAll('[data-quickadd-category]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                target = document.querySelector(btn.getAttribute('data-quickadd-target') || '');
                document.getElementById('sms-quickadd-category').value = btn.getAttribute('data-quickadd-category') || '';
                document.getElementById('sms-quickadd-title').textContent = btn.getAttribute('data-quickadd-title') || btn.getAttribute('title') || '';
                ['sms-quickadd-ar', 'sms-quickadd-en', 'sms-quickadd-code'].forEach(function (id) {
                    var el = document.getElementById(id); el.value = ''; el.removeAttribute('data-translit-manual');
                });
                clearError();
                modal.show();
                setTimeout(function () { document.getElementById('sms-quickadd-ar').focus(); }, 200);
            });
        });

        form.addEventListener('submit', function (ev) {
            ev.preventDefault();
            clearError();
            var submit = form.querySelector('button[type=submit]');
            submit.disabled = true;
            fetch(form.getAttribute('data-url'), { method: 'POST', body: new FormData(form), headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
                .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.json(); })
                .then(function (data) {
                    if (!data.ok) { showError(data.error || 'Error'); return; }
                    if (target) {
                        var opt = document.createElement('option');
                        opt.value = data.id;
                        opt.textContent = isRtl ? data.ar : data.en;
                        opt.selected = true;
                        target.appendChild(opt);
                        target.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                    modal.hide();
                })
                .catch(function (e) { showError(String(e && e.message ? e.message : e)); })
                .finally(function () { submit.disabled = false; });
        });
    });
})();
