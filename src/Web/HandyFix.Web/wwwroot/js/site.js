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

    // Date picker: the booking page's calendar as a dropdown under the field, instead of the
    // browser's picker, which only opened from its icon and closed whenever the page scrolled
    // (PROJECT_STATE Section 3bo). The chosen day goes into the hidden #widget-date as
    // yyyy-MM-dd, exactly what the old date input sent.
    const dateButton = document.getElementById("widget-date-btn");
    const dateText = document.getElementById("widget-date-text");
    const calendar = document.getElementById("widget-calendar");
    const calTitle = document.getElementById("widget-cal-title");
    const calGrid = document.getElementById("widget-cal-grid");
    const calPrev = document.getElementById("widget-cal-prev");
    const calNext = document.getElementById("widget-cal-next");

    const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    // Built from parts, not new Date("yyyy-MM-dd"), which reads the string as UTC midnight
    // and can land on the previous day in local time.
    function parseIsoDate(value) {
        const parts = value.split("-").map(Number);
        return new Date(parts[0], parts[1] - 1, parts[2]);
    }

    function toIsoDate(date) {
        return date.getFullYear() + "-" + String(date.getMonth() + 1).padStart(2, "0") + "-" + String(date.getDate()).padStart(2, "0");
    }

    // "Sat 26 Sep 2026" written out by hand: toLocaleDateString gives "Sat, 26 Sept 2026" in
    // some browsers and "Sat 26 Sep 2026" in others.
    function toDisplayDate(date) {
        return dayNames[date.getDay()].slice(0, 3) + " " + date.getDate() + " " + monthNames[date.getMonth()].slice(0, 3) + " " + date.getFullYear();
    }

    const firstBookableDay = parseIsoDate(calendar.dataset.min);
    let shownMonth = new Date(firstBookableDay.getFullYear(), firstBookableDay.getMonth(), 1);
    let selectedDate = null;

    function renderCalendar() {
        const year = shownMonth.getFullYear();
        const month = shownMonth.getMonth();
        calTitle.textContent = monthNames[month] + " " + year;
        calPrev.disabled = year === firstBookableDay.getFullYear() && month === firstBookableDay.getMonth();

        // Weeks start on Monday; getDay() is 0 for Sunday.
        const leadingBlanks = (new Date(year, month, 1).getDay() + 6) % 7;
        const daysInMonth = new Date(year, month + 1, 0).getDate();
        calGrid.innerHTML = "";

        for (let i = 0; i < leadingBlanks; i++) {
            const blank = document.createElement("span");
            blank.className = "cal-day-cell other-month";
            calGrid.appendChild(blank);
        }

        for (let day = 1; day <= daysInMonth; day++) {
            const date = new Date(year, month, day);
            const cell = document.createElement("button");
            cell.type = "button";
            cell.className = "cal-day-cell";
            cell.textContent = day;
            cell.setAttribute("aria-label", dayNames[date.getDay()] + " " + day + " " + monthNames[month] + " " + year);

            if (date < firstBookableDay) {
                cell.classList.add("disabled");
                cell.disabled = true;
            } else {
                if (date.getTime() === firstBookableDay.getTime()) {
                    cell.classList.add("today");
                }
                if (selectedDate && date.getTime() === selectedDate.getTime()) {
                    cell.classList.add("selected");
                }
                cell.addEventListener("click", function () {
                    pickDate(date, true);
                });
            }

            calGrid.appendChild(cell);
        }
    }

    function openCalendar() {
        if (selectedDate) {
            shownMonth = new Date(selectedDate.getFullYear(), selectedDate.getMonth(), 1);
        }
        renderCalendar();
        calendar.hidden = false;
        dateButton.setAttribute("aria-expanded", "true");
    }

    function closeCalendar(returnFocus) {
        calendar.hidden = true;
        dateButton.setAttribute("aria-expanded", "false");
        if (returnFocus) {
            dateButton.focus();
        }
    }

    function pickDate(date, fromCalendar) {
        selectedDate = date;
        dateInput.value = toIsoDate(date);
        dateText.textContent = toDisplayDate(date);
        dateText.classList.remove("hero-date-placeholder");
        if (fromCalendar) {
            closeCalendar(true);
        }
        checkFormValidity();
    }

    dateButton.addEventListener("click", function () {
        if (calendar.hidden) {
            openCalendar();
        } else {
            closeCalendar(false);
        }
    });

    calPrev.addEventListener("click", function () {
        shownMonth.setMonth(shownMonth.getMonth() - 1);
        renderCalendar();
    });

    calNext.addEventListener("click", function () {
        shownMonth.setMonth(shownMonth.getMonth() + 1);
        renderCalendar();
    });

    // Closes on a click outside the field or on Escape; deliberately not on scroll. "Outside"
    // is the whole field, label included: its click reaches the button as a second click,
    // which already toggles the calendar.
    const dateField = dateButton.closest(".hero-date-field");
    document.addEventListener("click", function (event) {
        if (!calendar.hidden && !dateField.contains(event.target)) {
            closeCalendar(false);
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !calendar.hidden) {
            closeCalendar(true);
        }
    });

    // A browser that restores form values on Back can refill the hidden input; show that date
    // in the field and re-check the button, rather than a blank field with a filled value.
    if (dateInput.value) {
        pickDate(parseIsoDate(dateInput.value), false);
    }
    checkFormValidity();
});
