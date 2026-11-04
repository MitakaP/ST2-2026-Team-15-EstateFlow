(() => {
    'use strict';
    const form = document.getElementById('details-favorite-form');
    if (!form || !window.fetch) return;

    const button = form.querySelector('button');
    const label = form.querySelector('[data-favorite-label]');
    const hint = form.querySelector('[data-favorite-hint]');
    const status = document.getElementById('favorite-detail-status');
    let submitting = false;

    function render(isFavorite) {
        form.dataset.favoriteState = String(isFavorite);
        button.classList.toggle('is-favorite', isFavorite);
        button.setAttribute('aria-pressed', String(isFavorite));
        const action = isFavorite ? 'Премахни от любими' : 'Добави в любими';
        button.setAttribute('aria-label', action);
        button.title = action;
        label.textContent = isFavorite ? 'В любими' : 'Запази в любими';
        hint.textContent = isFavorite ? 'Натисни, за да премахнеш' : 'Натисни сърцето, за да запазиш';
        status.textContent = isFavorite ? 'Имотът е добавен в любими.' : 'Имотът е премахнат от любими.';
    }

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (submitting) return;
        submitting = true;
        button.disabled = true;
        button.classList.add('is-saving');
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' },
                body: new FormData(form)
            });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) {
                throw new Error('Не успяхме да променим любимите имоти. Опитайте отново.');
            }
            const result = await response.json();
            if (typeof result.isFavorite !== 'boolean') throw new Error('Невалиден отговор от сървъра.');
            render(result.isFavorite);
            button.classList.remove('favorite-error');
        } catch (error) {
            status.textContent = error.message || 'Възникна грешка. Опитайте отново.';
            button.classList.add('favorite-error');
            window.alert(status.textContent);
        } finally {
            submitting = false;
            button.disabled = false;
            button.classList.remove('is-saving');
        }
    });
})();
