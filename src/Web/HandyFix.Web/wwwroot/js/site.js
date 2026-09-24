// Handy Fix Booking Widget Client Logic
document.addEventListener("DOMContentLoaded", function () {
    // Mobile Nav Toggle (Public)
    const mobileBtn = document.getElementById('mobile-menu-btn');
    const mobileMenu = document.getElementById('mobile-nav-menu');
    if (mobileBtn && mobileMenu) {
        mobileBtn.addEventListener('click', function () {
            mobileMenu.classList.toggle('is-open');
            document.body.classList.toggle('mobile-nav-open', mobileMenu.classList.contains('is-open'));
        });
    }

    // Publishes the header's real height as --header-height, used by navbar.css to pad
    // the page under the fixed mobile header. Observed rather than read once, since the
    // height shifts when web fonts load and when the phone number hides at narrow widths.
    const siteHeader = document.querySelector('.header-docked');
    if (siteHeader && 'ResizeObserver' in window) {
        new ResizeObserver(function () {
            document.documentElement.style.setProperty('--header-height', siteHeader.offsetHeight + 'px');
        }).observe(siteHeader);
    }

    // Admin Sidebar Toggle
    const adminToggleBtn = document.getElementById('admin-sidebar-btn');
    const adminSidebar = document.querySelector('.admin-sidebar');
    if (adminToggleBtn && adminSidebar) {
        adminToggleBtn.addEventListener('click', function () {
            adminSidebar.classList.toggle('open');
        });
    }

    // Printing: opens the closed <details> FAQs on service pages so their answers reach the
    // paper (print.css can't open a <details> in every browser), and closes them again after.
    // Each one is marked on the element, so a repeated beforeprint can't lose track of it.
    window.addEventListener('beforeprint', function () {
        document.querySelectorAll('details:not([open])').forEach(function (details) {
            details.open = true;
            details.dataset.openedForPrint = '';
        });
    });
    window.addEventListener('afterprint', function () {
        document.querySelectorAll('details[data-opened-for-print]').forEach(function (details) {
            details.open = false;
            delete details.dataset.openedForPrint;
        });
    });

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
