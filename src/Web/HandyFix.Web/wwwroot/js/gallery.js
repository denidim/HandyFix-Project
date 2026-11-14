// Accessible Lightbox for the public /Gallery page.
document.addEventListener("DOMContentLoaded", function () {
    const lightbox = document.getElementById("gallery-lightbox");
    const items = Array.from(document.querySelectorAll("[data-gallery-item]"));
    if (!lightbox || items.length === 0) return;

    const imgEl = document.getElementById("gallery-lightbox-img");
    const captionEl = document.getElementById("gallery-lightbox-caption");
    const badgeEl = document.getElementById("gallery-lightbox-badge");
    const counterEl = document.getElementById("gallery-lightbox-counter");
    const closeBtn = document.getElementById("gallery-lightbox-close");
    const prevBtn = document.getElementById("gallery-lightbox-prev");
    const nextBtn = document.getElementById("gallery-lightbox-next");
    const backdrop = lightbox.querySelector("[data-gallery-close]");

    let activeIndex = 0;
    let lastTrigger = null;

    function showSlide(index) {
        activeIndex = (index + items.length) % items.length;
        const item = items[activeIndex];
        const fullSrc = item.dataset.fullSrc || "";
        const caption = item.dataset.caption || "";
        const badge = item.dataset.badge || "";

        imgEl.src = fullSrc;
        imgEl.alt = caption;
        captionEl.textContent = caption;
        counterEl.textContent = `${activeIndex + 1} / ${items.length}`;

        if (badge) {
            badgeEl.textContent = badge;
            badgeEl.classList.toggle("gallery-lightbox-badge-after", badge === "After");
            badgeEl.hidden = false;
        } else {
            badgeEl.textContent = "";
            badgeEl.hidden = true;
        }
    }

    function openLightbox(index, trigger) {
        lastTrigger = trigger || null;
        showSlide(index);
        lightbox.hidden = false;
        document.body.style.overflow = "hidden";
        if (closeBtn) {
            closeBtn.focus();
        }
    }

    function closeLightbox() {
        lightbox.hidden = true;
        imgEl.src = "";
        document.body.style.overflow = "";
        if (lastTrigger && typeof lastTrigger.focus === "function") {
            lastTrigger.focus();
        }
        lastTrigger = null;
    }

    items.forEach((item, index) => {
        item.addEventListener("click", function () {
            openLightbox(index, item);
        });
    });

    if (closeBtn) closeBtn.addEventListener("click", closeLightbox);
    if (backdrop) backdrop.addEventListener("click", closeLightbox);
    if (prevBtn) prevBtn.addEventListener("click", () => showSlide(activeIndex - 1));
    if (nextBtn) nextBtn.addEventListener("click", () => showSlide(activeIndex + 1));

    document.addEventListener("keydown", function (event) {
        if (lightbox.hidden) return;
        if (event.key === "Escape") {
            closeLightbox();
        } else if (event.key === "ArrowLeft") {
            showSlide(activeIndex - 1);
        } else if (event.key === "ArrowRight") {
            showSlide(activeIndex + 1);
        }
    });
});
