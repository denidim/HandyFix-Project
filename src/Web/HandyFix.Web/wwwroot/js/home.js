// The home page (Views/Home/Index.cshtml): sections fade in and, when more than 6
// gallery photos are starred for home, rotate gently one slot at a time.
document.addEventListener("DOMContentLoaded", function () {
    const observerOptions = {
        threshold: 0.1
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    document.querySelectorAll('section').forEach(section => {
        section.style.transition = 'all 0.7s ease';
        section.style.opacity = '0';
        section.style.transform = 'translateY(2.5rem)';
        observer.observe(section);
    });

    // A visitor whose phone or computer asks for less motion keeps the first six photos still.
    const galleryStrip = document.querySelector("[data-home-photos]");
    if (!galleryStrip || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    let pool = [];
    try {
        pool = JSON.parse(galleryStrip.dataset.homePhotos || "[]");
    } catch (e) {
        pool = [];
    }

    const slots = Array.from(galleryStrip.querySelectorAll(".home-gallery-img"));
    if (slots.length === 0 || pool.length <= slots.length) return;

    let nextPoolIndex = slots.length % pool.length;
    let nextSlotIndex = 0;

    setInterval(function () {
        if (document.hidden) return;
        const img = slots[nextSlotIndex];
        const candidate = pool[nextPoolIndex];
        nextSlotIndex = (nextSlotIndex + 1) % slots.length;
        nextPoolIndex = (nextPoolIndex + 1) % pool.length;

        if (!candidate || !candidate.src) return;

        const preload = new Image();
        preload.onload = function () {
            img.classList.add("is-fading");
            setTimeout(function () {
                img.src = candidate.src;
                img.alt = candidate.alt || "Completed plumbing and handyman job in Surrey";
                img.classList.remove("is-fading");
            }, 250);
        };
        preload.src = candidate.src;
    }, 4000);
});
