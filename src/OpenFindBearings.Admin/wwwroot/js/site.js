document.addEventListener('DOMContentLoaded', function () {
    // Theme toggle
    var themeToggle = document.getElementById('themeToggle');
    var themeIcon = document.getElementById('themeIcon');
    var savedTheme = localStorage.getItem('theme') || 'light';

    function applyTheme(theme) {
        if (theme === 'dark') {
            document.body.classList.add('dark-theme');
            if (themeIcon) { themeIcon.className = 'fas fa-sun'; }
        } else {
            document.body.classList.remove('dark-theme');
            if (themeIcon) { themeIcon.className = 'fas fa-moon'; }
        }
    }

    applyTheme(savedTheme);

    if (themeToggle) {
        themeToggle.addEventListener('click', function () {
            var isDark = document.body.classList.contains('dark-theme');
            var newTheme = isDark ? 'light' : 'dark';
            localStorage.setItem('theme', newTheme);
            applyTheme(newTheme);
        });
    }

    // Sidebar toggle
    var toggle = document.getElementById('sidebarToggle');
    var sidebar = document.getElementById('sidebar');
    var overlay = document.getElementById('sidebarOverlay');
    var mainContent = document.querySelector('.main-content');
    if (toggle) {
        toggle.addEventListener('click', function () {
            if (window.innerWidth <= 768) {
                sidebar.classList.toggle('open');
                overlay.classList.toggle('show');
            } else {
                sidebar.classList.toggle('collapsed');
                if (mainContent) mainContent.classList.toggle('expanded');
            }
        });
    }
    if (overlay) {
        overlay.addEventListener('click', function () {
            sidebar.classList.remove('open');
            overlay.classList.remove('show');
        });
    }

    // Sub-menu toggle
    document.querySelectorAll('.toggle-sub').forEach(function (el) {
        el.addEventListener('click', function (e) {
            e.preventDefault();
            var li = this.closest('.has-sub');
            var target = document.getElementById(this.dataset.target);
            if (!target) return;
            var isOpen = li.classList.contains('open');
            // close others
            document.querySelectorAll('.has-sub.open').forEach(function (other) {
                if (other !== li) {
                    other.classList.remove('open');
                    var otherMenu = other.querySelector('.sub-menu');
                    if (otherMenu) otherMenu.classList.remove('show');
                }
            });
            li.classList.toggle('open', !isOpen);
            target.classList.toggle('show', !isOpen);
        });
    });

    // Highlight active nav
    var path = location.pathname.toLowerCase();
    document.querySelectorAll('.sidebar-nav .nav-link[data-page]').forEach(function (a) {
        var href = a.getAttribute('href');
        if (href) {
            var h = href.toLowerCase();
            if (path === h || (h !== '/' && path.startsWith(h))) {
                a.classList.add('active');
                // expand parent
                var subMenu = a.closest('.sub-menu');
                if (subMenu) {
                    subMenu.classList.add('show');
                    var parent = subMenu.closest('.has-sub');
                    if (parent) parent.classList.add('open');
                }
            }
        }
    });

    // Service status
    var statusEl = document.getElementById('serviceStatus');
    if (statusEl) {
        fetch('/Home/Status')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var html = '';
                for (var key in data) {
                    var s = data[key];
                    var cls = s.available ? 'online' : 'offline';
                    html += '<span class="service-item"><span class="service-dot ' + cls + '"></span>' + key + '</span>';
                }
                statusEl.innerHTML = html;
            })
            .catch(function () {
                statusEl.innerHTML = '<span class="service-item"><span class="service-dot offline"></span>服务不可用</span>';
            });
    }
});
