(function () {
    'use strict';

    const app = document.getElementById('properties-app');
    if (!app) return;

    const els = {
        body: document.getElementById('properties-body'),
        filter: document.getElementById('properties-filter'),
        pageSize: document.getElementById('properties-page-size'),
        info: document.getElementById('properties-info'),
        pagination: document.getElementById('properties-pagination')
    };

    const state = { all: [], filtered: [], page: 1, pageSize: parseInt(els.pageSize.value, 10), terms: [] };

    // Мапинг на Enum стойностите от C# към четим текст на български
    const propertyTypes = {
        0: 'Апартамент',
        1: 'Къща',
        2: 'Парцел',
        3: 'Офис',
        4: 'Гараж'
    };

    const escapeRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const norm = v => (v ?? '').toString().toLowerCase();
    const link = (template, id) => template.replace('__ID__', encodeURIComponent(id));

    function applyFilter() {
        state.terms = norm(els.filter.value).split(/\s+/).filter(Boolean);

        state.filtered = state.all.filter(c => {
            // Събираме всички полета в един масив за търсене, за да хващаме всичко
            const haystack = [
                c.id,
                c.referenceNumber,
                propertyTypes[c.type], // Слагаме текста, а не цифрата
                c.city,
                c.neighborhood,
                c.street,
                c.description,
                c.price,
                c.area,
                c.rooms,
                c.floor,
                c.contactPhone,
                c.contactEmail
            ].map(f => norm(f));

            // Логическо И: всяка въведена дума трябва да присъства някъде в данните
            return state.terms.every(t => haystack.some(h => h.includes(t)));
        });
        state.page = 1;
        render();
    }

    function highlight(parent, text) {
        const value = (text ?? '').toString();
        if (!value) { parent.textContent = '—'; return; }
        if (!state.terms.length) { parent.textContent = value; return; }
        const re = new RegExp('(' + state.terms.map(escapeRegExp).join('|') + ')', 'gi');

        value.split(re).forEach((part, i) => {
            if (!part) return;
            if (i % 2 === 1) {
                const mark = document.createElement('mark');
                mark.textContent = part;
                parent.appendChild(mark);
            } else {
                parent.appendChild(document.createTextNode(part));
            }
        });
    }

    function cell(tr, text, addClass = '') {
        const td = document.createElement('td');
        if (addClass) td.className = addClass;
        highlight(td, text);
        tr.appendChild(td);
        return td;
    }

    function actionButton(href, label, cls) {
        const a = document.createElement('a');
        a.href = href;
        a.className = 'btn btn-sm ' + cls;
        a.textContent = label;
        return a;
    }

    function renderRows(rows) {
        els.body.replaceChildren();
        if (!rows.length) {
            const tr = document.createElement('tr');
            const td = document.createElement('td');
            td.colSpan = 7;
            td.className = 'text-center text-muted py-4';
            td.textContent = state.all.length ? 'Няма имоти, отговарящи на търсенето.' : 'Все още няма обяви.';
            tr.appendChild(td);
            els.body.appendChild(tr);
            return;
        }
        rows.forEach(c => {
            const tr = document.createElement('tr');
            cell(tr, c.id);
            cell(tr, c.referenceNumber, 'fw-bold');
            cell(tr, propertyTypes[c.type]);
            cell(tr, `${c.city}, ${c.neighborhood}`);
            cell(tr, `${c.price.toLocaleString('bg-BG')} €`, 'text-success fw-semibold');
            cell(tr, `${c.area} кв.м.`);

            const actions = document.createElement('td');
            actions.className = 'text-end property-actions';
            const group = document.createElement('div');
            group.className = 'btn-group';
            group.append(
                actionButton(link(app.dataset.detailsUrl, c.id), 'Детайли', 'btn-outline-secondary'),
                actionButton(link(app.dataset.editUrl, c.id), 'Редакция', 'btn-outline-primary'),
                actionButton(link(app.dataset.deleteUrl, c.id), 'Изтрий', 'btn-outline-danger'));
            actions.appendChild(group);
            tr.appendChild(actions);
            els.body.appendChild(tr);
        });
    }

    function pageList(current, total) {
        const pages = new Set([1, total, current - 2, current - 1, current, current + 1, current + 2]);
        const sorted = [...pages].filter(p => p >= 1 && p <= total).sort((a, b) => a - b);
        const result = [];
        sorted.forEach((p, i) => {
            if (i && p - sorted[i - 1] > 1) result.push('…');
            result.push(p);
        });
        return result;
    }

    function pageItem(label, page, { active = false, disabled = false } = {}) {
        const li = document.createElement('li');
        li.className = 'page-item' + (active ? ' active' : '') + (disabled ? ' disabled' : '');
        const a = document.createElement('a');
        a.className = 'page-link';
        a.href = '#';
        a.textContent = label;
        if (active) a.setAttribute('aria-current', 'page');
        if (!disabled && !active) {
            a.addEventListener('click', e => { e.preventDefault(); state.page = page; render(); });
        } else {
            a.addEventListener('click', e => e.preventDefault());
        }
        li.appendChild(a);
        return li;
    }

    function renderPagination(totalPages) {
        els.pagination.replaceChildren();
        if (totalPages <= 1) return;
        els.pagination.appendChild(pageItem('«', state.page - 1, { disabled: state.page === 1 }));
        pageList(state.page, totalPages).forEach(p => {
            if (p === '…') {
                els.pagination.appendChild(pageItem('…', 0, { disabled: true }));
            } else {
                els.pagination.appendChild(pageItem(p, p, { active: p === state.page }));
            }
        });
        els.pagination.appendChild(pageItem('»', state.page + 1, { disabled: state.page === totalPages }));
    }

    function render() {
        const total = state.filtered.length;
        const totalPages = Math.max(1, Math.ceil(total / state.pageSize));
        state.page = Math.min(state.page, totalPages);
        const start = (state.page - 1) * state.pageSize;
        const rows = state.filtered.slice(start, start + state.pageSize);

        renderRows(rows);
        renderPagination(totalPages);

        els.info.textContent = total
            ? `Показани ${start + 1}–${start + rows.length} от ${total}` +
            (total !== state.all.length ? ` (общо ${state.all.length})` : '')
            : '';
    }

    function debounce(fn, ms) {
        let t;
        return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
    }

    els.filter.addEventListener('input', debounce(applyFilter, 150));
    els.pageSize.addEventListener('change', () => {
        state.pageSize = parseInt(els.pageSize.value, 10);
        state.page = 1;
        render();
    });

    fetch(app.dataset.listUrl, { headers: { Accept: 'application/json' } })
        .then(r => { if (!r.ok) throw new Error(r.statusText); return r.json(); })
        .then(data => { state.all = data; applyFilter(); })
        .catch(() => {
            els.body.replaceChildren();
            const tr = document.createElement('tr');
            const td = document.createElement('td');
            td.colSpan = 7;
            td.className = 'text-center text-danger py-4';
            td.textContent = 'Грешка при зареждане на обявите.';
            tr.appendChild(td);
            els.body.appendChild(tr);
        });
})();