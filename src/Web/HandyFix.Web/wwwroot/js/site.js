// Handy Fix Booking Widget Client Logic
document.addEventListener("DOMContentLoaded", function () {
    // Mobile Nav Toggle (Public)
    const mobileBtn = document.getElementById('mobile-menu-btn');
    const mobileMenu = document.getElementById('mobile-nav-menu');
    if (mobileBtn && mobileMenu) {
        mobileBtn.addEventListener('click', function () {
            mobileMenu.classList.toggle('is-open');
        });
    }

    // Admin Sidebar Toggle
    const adminToggleBtn = document.getElementById('admin-sidebar-btn');
    const adminSidebar = document.querySelector('.admin-sidebar');
    if (adminToggleBtn && adminSidebar) {
        adminToggleBtn.addEventListener('click', function () {
            adminSidebar.classList.toggle('open');
        });
    }

    const heroForm = document.getElementById("hero-booking-form");
    if (!heroForm) return;

    const dateInput = document.getElementById("widget-date");
    const categorySelect = document.getElementById("widget-category");
    const submitBtn = document.getElementById("widget-submit-btn");

    function checkFormValidity() {
        if (categorySelect.value && dateInput.value) {
            submitBtn.removeAttribute("disabled");
        } else {
            submitBtn.setAttribute("disabled", "true");
        }
    }

    categorySelect.addEventListener("change", checkFormValidity);
    dateInput.addEventListener("change", checkFormValidity);
});
