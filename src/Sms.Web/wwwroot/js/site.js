// SMS shell behaviour (docs/DesignSystem/15): desktop collapse persisted per
// device, mobile off-canvas drawer with backdrop dismiss.
(function () {
    var shell = document.getElementById('sms-shell');
    if (!shell) { return; }

    var collapseBtn = document.getElementById('sms-collapse-btn');
    if (collapseBtn) {
        collapseBtn.addEventListener('click', function () {
            var collapsed = shell.classList.toggle('is-collapsed');
            try { localStorage.setItem('sms-sidebar-collapsed', collapsed ? '1' : '0'); } catch (e) { }
        });
    }

    var menuBtn = document.getElementById('sms-menu-btn');
    var backdrop = document.getElementById('sms-backdrop');
    function closeDrawer() { shell.classList.remove('is-mobile-open'); }
    if (menuBtn) { menuBtn.addEventListener('click', function () { shell.classList.toggle('is-mobile-open'); }); }
    if (backdrop) { backdrop.addEventListener('click', closeDrawer); }
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') { closeDrawer(); } });
})();
