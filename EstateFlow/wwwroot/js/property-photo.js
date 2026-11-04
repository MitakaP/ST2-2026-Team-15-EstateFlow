(() => {
    'use strict';
    const input = document.getElementById('Photo');
    const preview = document.getElementById('property-photo-preview');
    const remove = document.getElementById('RemovePhoto');
    const error = document.getElementById('property-photo-error');
    if (!input || !preview) return;
    const original = preview.src;
    let objectUrl;
    function clearUrl() { if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = null; }
    function update() {
        clearUrl();
        const file = input.files?.[0];
        error.hidden = true;
        if (file && (file.size > 5 * 1024 * 1024 || !['image/jpeg', 'image/png', 'image/webp'].includes(file.type))) {
            error.textContent = 'Изберете JPG, PNG или WebP снимка до 5 MB.';
            error.hidden = false;
            input.value = '';
            preview.src = remove?.checked ? '/images/property-placeholder.svg' : original;
            return;
        }
        if (file) {
            objectUrl = URL.createObjectURL(file);
            preview.src = objectUrl;
            if (remove) remove.checked = false;
        } else preview.src = remove?.checked ? '/images/property-placeholder.svg' : original;
    }
    input.addEventListener('change', update);
    remove?.addEventListener('change', () => { if (remove.checked) input.value = ''; update(); });
    window.addEventListener('pagehide', clearUrl);
})();
