(() => {
  "use strict";
  const input = document.getElementById("profilePhoto");
  const canvas = document.getElementById("avatarCropCanvas");
  if (!input || !canvas) return;

  const zoom = document.getElementById("avatarZoom");
  const error = document.getElementById("photoSizeError");
  const previewBox = document.getElementById("photoPreviewBox");
  const preview = document.getElementById("photoPreview");
  const modalElement = document.getElementById("avatarCropModal");
  const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
  const cropButton = document.getElementById("applyAvatarCrop");
  const modifyButton = document.getElementById("editCropButton");
  const maxBytes = 5 * 1024 * 1024;
  const ctx = canvas.getContext("2d");
  let image = null;
  let imageObjectUrl = null;
  let previewUrl = null;
  let selectedFile = null;
  let confirmedFile = null;
  let offsetX = 0, offsetY = 0, zoomValue = 1;
  let pointer = null;

  function showError(message) { error.textContent = message; error.hidden = !message; }
  function baseScale(size) { return Math.max(size / image.naturalWidth, size / image.naturalHeight); }
  function limitOffsets() {
    const scale = baseScale(canvas.width) * zoomValue;
    const maxX = Math.max(0, (image.naturalWidth * scale - canvas.width) / 2);
    const maxY = Math.max(0, (image.naturalHeight * scale - canvas.height) / 2);
    offsetX = Math.max(-maxX, Math.min(maxX, offsetX));
    offsetY = Math.max(-maxY, Math.min(maxY, offsetY));
  }
  function draw(target, size) {
    const c = target.getContext("2d");
    c.clearRect(0, 0, size, size);
    c.save();
    c.beginPath(); c.arc(size / 2, size / 2, size / 2, 0, 2 * Math.PI); c.clip();
    const s = baseScale(size) * zoomValue;
    const factor = size / canvas.width;
    c.drawImage(image, (size - image.naturalWidth * s) / 2 + offsetX * factor,
      (size - image.naturalHeight * s) / 2 + offsetY * factor,
      image.naturalWidth * s, image.naturalHeight * s);
    c.restore();
  }
  function redraw() { if (!image) return; limitOffsets(); draw(canvas, canvas.width); }
  function setInputFile(file) {
    const transfer = new DataTransfer(); transfer.items.add(file);
    input.files = transfer.files;
  }
  async function openFile(file) {
    showError("");
    if (!file) return;
    if (!(["image/jpeg", "image/png", "image/webp"].includes(file.type))) {
      showError("Позволени са JPG, PNG и WebP."); if (confirmedFile) setInputFile(confirmedFile); else input.value = ""; return;
    }
    if (file.size > maxBytes) {
      showError("Снимката е твърде голяма. Максималният размер е 5 MB.");
      if (confirmedFile) setInputFile(confirmedFile); else input.value = "";
      return;
    }
    selectedFile = file;
    if (imageObjectUrl) URL.revokeObjectURL(imageObjectUrl);
    imageObjectUrl = URL.createObjectURL(file);
    const nextImage = new Image();
    try {
      await new Promise((resolve, reject) => {
        nextImage.onload = resolve; nextImage.onerror = reject;
        nextImage.src = imageObjectUrl;
      });
      if (!nextImage.naturalWidth || !nextImage.naturalHeight) throw new Error("empty image");
      image = nextImage; offsetX = 0; offsetY = 0; zoomValue = 1; zoom.value = "1";
      redraw(); modal.show();
    } catch {
      showError("Изображението не може да бъде прочетено. Избери друг файл.");
      if (confirmedFile) setInputFile(confirmedFile); else input.value = "";
    }
  }
  input.addEventListener("change", () => { const f = input.files?.[0]; if (f) void openFile(f); });
  modifyButton.addEventListener("click", () => { if (image) modal.show(); });
  zoom.addEventListener("input", () => { zoomValue = Number(zoom.value); redraw(); });
  canvas.addEventListener("pointerdown", e => {
    if (!image) return; pointer = {id:e.pointerId, x:e.clientX, y:e.clientY};
    canvas.setPointerCapture(e.pointerId); canvas.style.cursor = "grabbing";
  });
  canvas.addEventListener("pointermove", e => {
    if (!pointer || e.pointerId !== pointer.id) return;
    const ratio = canvas.width / canvas.getBoundingClientRect().width;
    offsetX += (e.clientX - pointer.x) * ratio;
    offsetY += (e.clientY - pointer.y) * ratio;
    pointer.x = e.clientX; pointer.y = e.clientY; redraw();
  });
  function releasePointer(e) {
    if (pointer && pointer.id === e.pointerId) { pointer = null; canvas.style.cursor = "grab"; }
  }
  canvas.addEventListener("pointerup", releasePointer);
  canvas.addEventListener("pointercancel", releasePointer);
  cropButton.addEventListener("click", async () => {
    if (!image || !selectedFile) return;
    cropButton.disabled = true;
    try {
      const output = document.createElement("canvas"); output.width = output.height = 512;
      draw(output, 512);
      const blob = await new Promise(resolve => output.toBlob(resolve, "image/png"));
      if (!blob) throw new Error("Could not encode image");
      if (blob.size > maxBytes) throw new Error("Result exceeds 5 MB");
      const file = new File([blob], "avatar.png", {type:"image/png"});
      setInputFile(file); confirmedFile = file;
      if (previewUrl) URL.revokeObjectURL(previewUrl);
      previewUrl = URL.createObjectURL(blob);
      preview.src = previewUrl; previewBox.hidden = false; showError("");
      modal.hide();
    } catch { showError("Неуспешно изрязване на снимката. Опитай с друг файл."); }
    finally { cropButton.disabled = false; }
  });
  modalElement.addEventListener("hidden.bs.modal", () => {
    if (confirmedFile) setInputFile(confirmedFile); else input.value = "";
  });
})();
