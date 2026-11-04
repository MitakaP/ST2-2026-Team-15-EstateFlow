(() => {
    'use strict';
    const app = document.getElementById('properties-app');
    if (!app) return;
    const isAuthenticated = app.dataset.isAuthenticated === 'true';
    const isAdmin = app.dataset.isAdmin === 'true';
    const isBroker = app.dataset.isBroker === 'true';
    const favoriteIds = new Set();
    const token = document.querySelector('#favorites-token-form input[name="__RequestVerificationToken"]')?.value;
    const els = {
        body: document.getElementById('properties-body'), filter: document.getElementById('properties-filter'),
        pageSize: document.getElementById('properties-page-size'), info: document.getElementById('properties-info'),
        pagination: document.getElementById('properties-pagination'), error: document.getElementById('properties-error')
    };
    const aiForm = document.getElementById('properties-ai-form');
    const aiQuery = document.getElementById('properties-ai-query');
    const aiSubmit = document.getElementById('properties-ai-submit');
    const aiReset = document.getElementById('properties-ai-reset');
    const aiStatus = document.getElementById('properties-ai-status');
    const aiProvider = document.getElementById('properties-ai-provider');
    const state = { all: [], original: [], filtered: [], page: 1, pageSize: Number(els.pageSize.value), terms: [], type: '', loaded: false };
    const propertyTypes = { 0: 'Апартамент', 1: 'Къща', 2: 'Парцел', 3: 'Офис', 4: 'Гараж' };
    const norm = value => (value ?? '').toString().toLowerCase();
    const link = (template, id) => template.replace('__ID__', encodeURIComponent(id));
    const format = value => Number(value).toLocaleString('bg-BG');
    const escapeRegExp = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    function node(tag, className, text) {
        const el = document.createElement(tag); if (className) el.className = className;
        if (text !== undefined) el.textContent = text; return el;
    }
    function icon(name) {
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'icon'); svg.setAttribute('aria-hidden', 'true');
        const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', '/images/icons.svg#' + name); svg.append(use); return svg;
    }
    function highlight(parent, text) {
        const value = String(text ?? '');
        if (!state.terms.length) { parent.textContent = value; return; }
        const regex = new RegExp('(' + state.terms.map(escapeRegExp).join('|') + ')', 'gi');
        value.split(regex).forEach((part, i) => parent.append(i % 2 ? node('mark', '', part) : document.createTextNode(part)));
    }
    function action(href, label, cls) { const a = node('a', cls, label); a.href = href; return a; }
    function showError(message) { els.error.textContent = message; els.error.hidden = !message; }
    function updateFavorite(button, id) {
        const saved = favoriteIds.has(id);
        button.setAttribute('aria-pressed', String(saved));
        button.setAttribute('aria-label', (saved ? 'Премахни от любими: ' : 'Добави в любими: ') + button.dataset.reference);
    }
    function favoriteButton(property) {
        const b = node('button', 'property-favorite'); b.type = 'button'; b.dataset.reference = property.referenceNumber;
        b.append(icon('heart')); updateFavorite(b, property.id);
        b.addEventListener('click', async () => {
            if (!isAuthenticated) { window.location.assign(app.dataset.loginUrl); return; }
            b.disabled = true; showError('');
            try {
                const response = await fetch(app.dataset.favoriteToggleUrl, {
                    method: 'POST', headers: { Accept: 'application/json' },
                    body: new URLSearchParams({ propertyId: String(property.id), __RequestVerificationToken: token || '' })
                });
                if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Не успяхме да запазим имота. Влезте отново или опитайте пак.');
                const result = await response.json();
                if (typeof result.isFavorite !== 'boolean') throw new Error('Не успяхме да запазим имота. Опитайте отново.');
                result.isFavorite ? favoriteIds.add(property.id) : favoriteIds.delete(property.id);
                updateFavorite(b, property.id);
            } catch (error) { showError(error.message); }
            finally { b.disabled = false; }
        }); return b;
    }
    function propertyCard(p, index) {
        const card = node('article', 'property-card'); card.style.animationDelay = Math.min(index * 35, 140) + 'ms';
        const media = node('div', 'property-image');
        const imageLink = action(link(app.dataset.detailsUrl, p.id), '', ''); imageLink.setAttribute('aria-label', 'Детайли за ' + p.referenceNumber);
        const image = node('img'); image.alt = p.imagePath ? 'Снимка на имот ' + p.referenceNumber : 'Няма добавена снимка';
        image.loading = index < 3 ? 'eager' : 'lazy'; image.decoding = 'async'; image.dataset.propertyPhoto = '';
        const demoImage = /^\/images\/demo\/(apartment-plovdiv|house-sofia|land-varna|apartment-smirnenski|office-sofia|apartment-varna)\.jpg$/.test(p.imagePath || '');
        image.src = demoImage || /^\/uploads\/properties\/[a-f0-9]{32}\.(jpg|png|webp)$/.test(p.imagePath || '') ? p.imagePath : '/images/property-placeholder.svg';
        if (demoImage) image.alt = 'Примерна снимка за демонстрационен имот ' + p.referenceNumber;
        imageLink.append(image); media.append(imageLink, node('span', 'property-type', propertyTypes[p.type] || 'Имот'), favoriteButton(p));
        const content = node('div', 'property-content'); const price = node('div', 'property-price'); highlight(price, format(p.price) + ' €');
        const title = node('h3', 'property-title');
        const titleLink = action(link(app.dataset.detailsUrl, p.id), '', '');
        const name = Number(p.type) === 0 && p.rooms ? `${p.rooms}-стаен апартамент` : propertyTypes[p.type] || 'Имот';
        highlight(titleLink, name); title.append(titleLink);
        const location = node('div', 'property-location'); location.append(icon('pin')); const locationText = node('span'); highlight(locationText, p.city + ' · ' + p.neighborhood); location.append(locationText);
        const meta = node('div', 'property-meta'); const area = node('span'); area.append(icon('area')); const areaText = node('span'); highlight(areaText, format(p.area) + ' м²'); area.append(areaText); meta.append(area);
        if (p.rooms != null) { const rooms = node('span'); rooms.append(icon('door'), document.createTextNode(p.rooms + ' стаи')); meta.append(rooms); }
        const bottom = node('div', 'property-bottom'); const reference = node('span', 'property-reference'); highlight(reference, p.referenceNumber);
        const detail = action(link(app.dataset.detailsUrl, p.id), 'Детайли ', 'property-detail-link'); detail.append(icon('arrow')); bottom.append(reference, detail);
        content.append(price, title, location, meta, bottom);
        if (isAdmin || (isBroker && app.dataset.currentUserId && p.ownerId === app.dataset.currentUserId)) {
            const manage = node('div', 'property-manage'); manage.append(action(link(app.dataset.editUrl, p.id), 'Редакция', 'btn btn-sm btn-outline-primary'), action(link(app.dataset.deleteUrl, p.id), 'Изтрий', 'btn btn-sm btn-outline-danger')); content.append(manage);
        }
        card.append(media, content); return card;
    }
    function applyFilter() {
        state.terms = norm(els.filter.value).split(/\s+/).filter(Boolean);
        state.filtered = state.all.filter(p => {
            if (state.type !== '' && Number(p.type) !== Number(state.type)) return false;
            const fields = [p.id, p.referenceNumber, propertyTypes[p.type], p.city, p.neighborhood, p.street, p.description, p.price, p.area, p.rooms, p.floor, p.contactPhone, p.contactEmail].map(norm);
            return state.terms.every(term => fields.some(f => f.includes(term)));
        }); state.page = 1; render();
    }
    function pageItem(label, page, active, disabled) {
        const li = node('li', 'page-item' + (active ? ' active' : '') + (disabled ? ' disabled' : ''));
        const b = node('button', 'page-link', label); b.type = 'button'; b.disabled = !!disabled;
        b.setAttribute('aria-label', label === '‹' ? 'Предишна страница' : label === '›' ? 'Следваща страница' : 'Страница ' + label);
        if (active) b.setAttribute('aria-current', 'page');
        b.addEventListener('click', () => { state.page = page; render(); document.querySelector('.listing-toolbar').scrollIntoView({ block: 'start' }); }); li.append(b); return li;
    }
    function render() {
        if (!state.loaded) return;
        const total = state.filtered.length, pages = Math.max(1, Math.ceil(total / state.pageSize)); state.page = Math.min(state.page, pages);
        const start = (state.page - 1) * state.pageSize; const rows = state.filtered.slice(start, start + state.pageSize);
        els.body.replaceChildren(...rows.map(propertyCard)); els.body.setAttribute('aria-busy', 'false');
        if (!rows.length) {
            const empty = node('div', 'empty-state'); empty.append(icon('search'), node('h2', '', state.original.length ? 'Няма съвпадащи имоти' : 'Все още няма обяви'), node('p', '', 'Опитайте с други критерии или изчистете търсенето.')); els.body.append(empty);
        }
        els.pagination.replaceChildren();
        if (pages > 1) {
            els.pagination.append(pageItem('‹', state.page - 1, false, state.page === 1));
            const visible = [...new Set([1, pages, state.page - 1, state.page, state.page + 1])].filter(p => p > 0 && p <= pages).sort((a, b) => a - b);
            visible.forEach((p, i) => { if (i && p - visible[i - 1] > 1) els.pagination.append(pageItem('…', 0, false, true)); els.pagination.append(pageItem(String(p), p, p === state.page, false)); });
            els.pagination.append(pageItem('›', state.page + 1, false, state.page === pages));
        }
        els.info.textContent = total ? `Показани ${start + 1}–${start + rows.length} от ${total} имота` : '0 имота';
    }
    let filterTimer;
    els.filter.addEventListener('input', () => { clearTimeout(filterTimer); filterTimer = setTimeout(applyFilter, 150); });
    els.pageSize.addEventListener('change', () => { state.pageSize = Number(els.pageSize.value); state.page = 1; render(); });
    app.querySelectorAll('[data-property-type]').forEach(b => b.addEventListener('click', () => {
        state.type = b.dataset.propertyType; app.querySelectorAll('[data-property-type]').forEach(el => el.setAttribute('aria-pressed', String(el === b))); applyFilter();
    }));
    function resetTypes() { state.type = ''; app.querySelectorAll('[data-property-type]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.propertyType === ''))); }
    function hideProvider() { aiProvider.className = 'badge bg-secondary d-none'; aiProvider.textContent = ''; }
    let aiController;
    aiForm.addEventListener('submit', async event => {
        event.preventDefault(); if (!state.loaded) return;
        aiController?.abort(); const controller = new AbortController(); aiController = controller;
        aiSubmit.disabled = true; aiForm.setAttribute('aria-busy', 'true'); hideProvider(); showError('');
        aiStatus.className = 'ai-status small mt-2 text-muted'; aiStatus.textContent = 'Търсим подходящите имоти…';
        try {
            const response = await fetch(app.dataset.aiUrl, { method: 'POST', headers: { Accept: 'application/json' }, body: new FormData(aiForm), signal: controller.signal });
            if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('Търсенето временно не е достъпно. Опитайте отново.');
            const data = await response.json();
            if (!response.ok || !Array.isArray(data.properties)) throw new Error(data.error || 'Неуспешно търсене.');
            state.all = data.properties; els.filter.value = ''; resetTypes(); applyFilter();
            const provider = data.provider || 'ИИ'; const fallback = data.usedFallback === true;
            aiProvider.textContent = fallback ? `${provider} (резервен)` : provider;
            aiProvider.className = fallback ? 'badge bg-warning text-dark' : provider.startsWith('Локален модел') ? 'badge bg-success' : 'badge bg-primary';
            aiStatus.className = 'ai-status small mt-2 text-success'; aiStatus.textContent = (data.explanation || 'Готово.') + ` Намерени имоти: ${data.properties.length}.`;
        } catch (error) { if (error.name !== 'AbortError') { aiStatus.className = 'ai-status small mt-2 text-danger'; aiStatus.textContent = error.message; hideProvider(); } }
        finally { if (aiController === controller) { aiController = null; aiSubmit.disabled = false; aiForm.setAttribute('aria-busy', 'false'); } }
    });
    aiReset.addEventListener('click', () => {
        aiController?.abort(); aiController = null; aiSubmit.disabled = !state.loaded; aiForm.setAttribute('aria-busy', 'false'); aiQuery.value = ''; aiStatus.textContent = ''; hideProvider(); els.filter.value = ''; resetTypes(); showError(''); state.all = state.original.slice(); applyFilter();
    });
    app.querySelectorAll('[data-ai-prompt]').forEach(b => b.addEventListener('click', () => { aiQuery.value = b.dataset.aiPrompt; aiQuery.focus(); }));
    async function load() {
        aiSubmit.disabled = true;
        try {
            const response = await fetch(app.dataset.listUrl, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error(); const properties = await response.json(); if (!Array.isArray(properties)) throw new Error();
            if (isAuthenticated) {
                try {
                    const result = await fetch(app.dataset.favoriteIdsUrl, { headers: { Accept: 'application/json' } });
                    if (!result.ok || !result.headers.get('content-type')?.includes('application/json')) throw new Error();
                    const ids = await result.json(); ids.forEach(id => favoriteIds.add(id));
                } catch { showError('Любимите имоти не могат да бъдат заредени. Обновете страницата, за да видите актуалното им състояние.'); }
            }
            state.original = properties; state.all = properties; state.loaded = true; aiSubmit.disabled = false; applyFilter();
        } catch {
            els.body.replaceChildren(); els.body.setAttribute('aria-busy', 'false');
            const empty = node('div', 'empty-state'); empty.append(node('h2', '', 'Не успяхме да заредим обявите.'));
            const retry = node('button', 'btn btn-outline-primary', 'Опитай отново'); retry.type = 'button'; retry.addEventListener('click', () => { els.body.setAttribute('aria-busy', 'true'); load(); }); empty.append(retry); els.body.append(empty);
        }
    }
    load();
})();
