(() => {
    'use strict';
    const sidebar = document.getElementById('site-sidebar');
    const toggle = document.getElementById('menu-toggle');
    const backdrop = document.getElementById('menu-backdrop');
    const mobile = window.matchMedia('(max-width: 767.98px)');
    function setMenu(open, focus = false) {
        const expanded = mobile.matches && open;
        document.body.classList.toggle('menu-open', expanded);
        sidebar.inert = mobile.matches && !expanded;
        toggle.setAttribute('aria-expanded', String(expanded));
        toggle.setAttribute('aria-label', expanded ? 'Затвори менюто' : 'Отвори менюто');
        backdrop.hidden = !expanded;
        if (focus) (expanded ? sidebar.querySelector('a') : toggle)?.focus();
    }
    toggle.addEventListener('click', () => setMenu(toggle.getAttribute('aria-expanded') !== 'true', true));
    backdrop.addEventListener('click', () => setMenu(false, true));
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && document.body.classList.contains('menu-open')) setMenu(false, true);
        if (e.key !== 'Tab' || !document.body.classList.contains('menu-open')) return;
        const links = [...sidebar.querySelectorAll('a, button')].filter(el => !el.disabled);
        const first = links[0], last = links.at(-1);
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); toggle.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); toggle.focus(); }
        else if (document.activeElement === toggle) { e.preventDefault(); (e.shiftKey ? last : first)?.focus(); }
    });
    mobile.addEventListener('change', () => setMenu(false));
    setMenu(false);
    document.addEventListener('error', event => {
        const image = event.target;
        if (image instanceof HTMLImageElement && image.matches('[data-property-photo]') && !image.dataset.fallback) {
            image.dataset.fallback = 'true';
            image.src = '/images/property-placeholder.svg';
        }
    }, true);
})();
