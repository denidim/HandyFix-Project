// The booking page's wizard (Views/Booking/Index.cshtml): the service list for each tab, the
// calendar, the day's slots, the postcode check, the summary and the steps beside the form.
// What it needs from the server arrives in data- attributes on the form, so nothing here is
// written by Razor, and the browser keeps the file between visits.
document.addEventListener("DOMContentLoaded", function () {
    // What the page needs from the server, written by Razor into the form's data- attributes.
    const form = document.getElementById("booking-form");
    const descriptionMinLength = parseInt(form.dataset.descriptionMin, 10);

    // Core Elements
    const serviceSelect = document.getElementById("wizard-service");
    const dateInput = document.getElementById("wizard-date");
    const slotsContainer = document.getElementById("wizard-slots");
    const slotsHeaderDate = document.getElementById("slots-header-date");
    const slotsCountBadge = document.getElementById("slots-count-badge");
    const hiddenSlotInput = document.getElementById("wizard-slot-id");
    const submitBtn = document.getElementById("submit-btn");
    const step1IncompleteHint = document.getElementById("step1-incomplete-hint");
    const enquiryLink = document.getElementById("slot-enquiry-link");
    const enquiryBaseHref = enquiryLink.getAttribute("href");

    // Postcode: bookings are taken only within 15 miles of the base or in a district a
    // service area lists. The server has the last word (BookingController); this tells
    // the visitor as soon as the postcode is typed, before the rest of the form is filled in.
    const postcodeInput = document.getElementById("Postcode");
    const postcodeNotice = document.getElementById("postcode-not-served");
    const postcodeNoticeDistrict = document.getElementById("postcode-not-served-district");
    const postcodeEnquiryLink = document.getElementById("postcode-enquiry-link");
    const servedDistricts = JSON.parse(form.dataset.servedDistricts || "[]");
    const wholePostcode = /^[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2}$/;

    // "KT9" for "kt9 2qn", or null while the postcode is still being typed.
    function postcodeDistrict() {
        const compact = postcodeInput.value.replace(/\s+/g, "").toUpperCase();
        return wholePostcode.test(compact) ? compact.slice(0, -3) : null;
    }

    // The server always sends the 15-mile list, so an empty one means the page lost it;
    // then this check steps aside and the server's decides. A few London districts carry
    // a letter ("SW1A"); those count as their district ("SW1").
    function isPostcodeServed() {
        const district = postcodeDistrict();
        if (!district || servedDistricts.length === 0) return true;
        const withoutLetter = /[A-Z]$/.test(district) ? district.slice(0, -1) : district;
        return servedDistricts.includes(district) || servedDistricts.includes(withoutLetter);
    }

    function updatePostcodeNotice() {
        const served = isPostcodeServed();
        postcodeNotice.hidden = served;
        if (!served) postcodeNoticeDistrict.textContent = postcodeDistrict();
    }

    // Sidebar Elements
    const summaryService = document.getElementById("summary-service");
    const summaryDatetime = document.getElementById("summary-datetime");
    const summaryMinCharge = document.getElementById("summary-min-charge");
    const summaryDeposit = document.getElementById("summary-deposit");
    const summaryTotalPrice = document.getElementById("summary-total-price");

    // Category Filter Logic
    const categoryRadios = document.querySelectorAll('input[name="service-category"]');
    const allServiceOptions = Array.from(serviceSelect.options);

    function filterServices() {
        const activeCategory = document.querySelector('input[name="service-category"]:checked').value;

        // A phone's native select sheet ignores display:none on an option and shows every
        // option still inside the select. Keeping only the chosen category's options in the
        // select hides the rest on phones and computers alike. The list arrives in display
        // order (an admin field on each service), so the first option after the blank one is
        // the category's default: the general call-out, unless an admin has put another
        // service ahead of it (PROJECT_STATE Section 3ca).
        serviceSelect.replaceChildren(
            ...allServiceOptions.filter(opt => opt.value === "" || opt.dataset.category === activeCategory)
        );
        const firstCategoryOptionVal = serviceSelect.options[1]?.value || "";

        // The service named in the link if it belongs to this category, else the default
        const targetServiceId = form.dataset.selectedServiceId || "";
        const matchedOpt = targetServiceId
            ? Array.from(serviceSelect.options).find(o => o.value.toLowerCase() === targetServiceId.toLowerCase())
            : null;
        serviceSelect.value = matchedOpt ? matchedOpt.value : firstCategoryOptionVal;

        updateSummaryPrice();
        checkFormValidity();
    }

    categoryRadios.forEach(radio => {
        radio.addEventListener("change", filterServices);
    });

    // Update Price Summary based on selected service base price
    function updateSummaryPrice() {
        const activeCategory = document.querySelector('input[name="service-category"]:checked').value;
        const opt = serviceSelect.options[serviceSelect.selectedIndex];
        let basePrice = 0;

        if (opt && opt.value !== "") {
            basePrice = parseFloat(opt.dataset.price) || 0;
            summaryService.textContent = opt.text.split(" (from")[0];
        } else {
            summaryService.textContent = activeCategory.charAt(0).toUpperCase() + activeCategory.slice(1) + " Service";
            basePrice = activeCategory === form.dataset.plumbingCategory ? 90 : 60; // Flat category rates (Plumbing 90 pounds, Handyman 60)
        }

        updateEnquiryLink();

        const formattedPrice = "\u00A3" + basePrice.toFixed(2);
        summaryMinCharge.textContent = formattedPrice;
        // The deposit is a flat amount (HandyFix.Common.Deposit), not the first-hour
        // price - showing the hourly rate here promised a different amount than Stripe charges.
        summaryDeposit.textContent = "\u00A3" + form.dataset.deposit;
        summaryTotalPrice.textContent = formattedPrice;

        updateSummaryDateTime();
    }

    function updateSummaryDateTime() {
        const dateVal = dateInput.value;
        const selectedSlotBtn = document.querySelector(".slot-selection-btn.selected");

        if (dateVal && selectedSlotBtn) {
            const formattedDate = new Date(dateVal).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
            const timeText = selectedSlotBtn.querySelector(".slot-time-text")?.textContent || selectedSlotBtn.textContent;
            summaryDatetime.textContent = `${formattedDate}, ${timeText}`;
        } else {
            summaryDatetime.textContent = "--";
        }
    }

    // "Send an Enquiry" in the timeslot card: carries the chosen service and day to the
    // Contact page, which pre-fills a message asking about that day. The day only goes
    // with a service, since the message names both.
    function updateEnquiryLink() {
        const opt = serviceSelect.options[serviceSelect.selectedIndex];
        const params = new URLSearchParams();
        if (opt && opt.value !== "") {
            params.set("service", opt.text.split(" (from")[0].trim());
            if (dateInput.value) params.set("date", dateInput.value);
        }
        const query = params.toString();
        const href = query ? `${enquiryBaseHref}?${query}` : enquiryBaseHref;
        enquiryLink.setAttribute("href", href);
        postcodeEnquiryLink.setAttribute("href", href);
    }

    serviceSelect.addEventListener("change", updateSummaryPrice);

    // ==========================================
    // Custom Open Calendar Logic
    // ==========================================
    // The date comes from the link the visitor opened. The controller only passes on a
    // real date, as yyyy-MM-dd; without one the calendar opens on today. Today is the
    // visitor's own date: toISOString() gave the date in UTC, which between midnight and
    // 1am in summer is still yesterday, so the page opened on a day already gone.
    let initialDateStr = form.dataset.selectedDate || "";
    if (!initialDateStr) {
        const now = new Date();
        initialDateStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
    }

    // When a sent form comes back to the customer, the slot they had chosen. The page
    // opens on that slot's day (BookingController) and picks the slot again once the
    // day's slots have loaded, if it is still free (PROJECT_STATE Section 3cb).
    let keptSlotId = form.dataset.keptSlotId || "";

    let calCurrentDate = new Date(initialDateStr); // Month navigator pointer
    let selectedDateStr = initialDateStr;
    dateInput.value = selectedDateStr;

    const calMonthYearLabel = document.getElementById("cal-month-year");
    const calPrevBtn = document.getElementById("cal-prev-month");
    const calNextBtn = document.getElementById("cal-next-month");
    const calDatesGrid = document.getElementById("cal-dates-grid");

    function renderCalendar() {
        const year = calCurrentDate.getFullYear();
        const month = calCurrentDate.getMonth();
        const today = new Date();
        today.setHours(0,0,0,0);

        const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
        calMonthYearLabel.textContent = `${monthNames[month]} ${year}`;

        // Prev button disable check (can't navigate before current month)
        if (year === today.getFullYear() && month === today.getMonth()) {
            calPrevBtn.disabled = true;
        } else {
            calPrevBtn.disabled = false;
        }

        // First day of month (0 = Sun, convert to 0 = Mon)
        let firstDayIndex = new Date(year, month, 1).getDay() - 1;
        if (firstDayIndex < 0) firstDayIndex = 6;

        const lastDate = new Date(year, month + 1, 0).getDate();
        const prevMonthLastDate = new Date(year, month, 0).getDate();

        calDatesGrid.innerHTML = "";

        // Padding days for previous month
        for (let i = firstDayIndex; i > 0; i--) {
            const cell = document.createElement("button");
            cell.type = "button";
            cell.className = "cal-day-cell other-month";
            cell.textContent = prevMonthLastDate - i + 1;
            calDatesGrid.appendChild(cell);
        }

        // Current month dates
        for (let day = 1; day <= lastDate; day++) {
            const cell = document.createElement("button");
            cell.type = "button";
            cell.className = "cal-day-cell";
            cell.textContent = day;

            const cellDateObj = new Date(year, month, day);
            const yyyy = year;
            const mm = String(month + 1).padStart(2, '0');
            const dd = String(day).padStart(2, '0');
            const dateStr = `${yyyy}-${mm}-${dd}`;

            if (cellDateObj < today) {
                cell.classList.add("disabled");
                cell.disabled = true;
            } else {
                if (cellDateObj.getTime() === today.getTime()) {
                    cell.classList.add("today");
                }
                if (dateStr === selectedDateStr) {
                    cell.classList.add("selected");
                }

                cell.addEventListener("click", function () {
                    selectedDateStr = dateStr;
                    dateInput.value = selectedDateStr;
                    renderCalendar();
                    loadSlotsForDate(selectedDateStr);
                });
            }

            calDatesGrid.appendChild(cell);
        }
    }

    calPrevBtn.addEventListener("click", function () {
        calCurrentDate.setMonth(calCurrentDate.getMonth() - 1);
        renderCalendar();
    });

    calNextBtn.addEventListener("click", function () {
        calCurrentDate.setMonth(calCurrentDate.getMonth() + 1);
        renderCalendar();
    });

    // ==========================================
    // Timeslots Loading Logic (Red / Green)
    // ==========================================
    function loadSlotsForDate(dateStr) {
        if (!dateStr) return;

        updateEnquiryLink();

        const formattedDateHeading = new Date(dateStr).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
        slotsHeaderDate.textContent = `Slots for ${formattedDateHeading}`;
        slotsCountBadge.textContent = "Loading...";
        slotsContainer.innerHTML = "<div class='text-center p-3 text-muted w-100 slots-message-full-width'>Loading availability...</div>";

        fetch(`/Booking/GetSlots?date=${dateStr}`)
            .then(res => {
                // 429: more days looked at in a minute than one visitor is allowed
                // (RateLimits.SlotLookupsPolicy). It passes by itself, so say that.
                if (res.status === 429) throw new Error("too-many-lookups");
                if (!res.ok) throw new Error("Failed to load slots");
                return res.json();
            })
            .then(slots => {
                slotsContainer.innerHTML = "";
                hiddenSlotInput.value = "";

                // The slot to pick again, once: the one the customer had chosen when the
                // form came back to them, if this is its day.
                const slotToRestore = dateStr === initialDateStr ? keptSlotId : "";
                keptSlotId = "";

                if (!slots || slots.length === 0) {
                    slotsCountBadge.textContent = "0 Available";
                    slotsContainer.innerHTML = "<div class='text-danger p-3 text-center w-100 slots-message-full-width'>No availability on this date. Please pick another date.</div>";
                    updateSummaryDateTime();
                    checkFormValidity();
                    return;
                }

                let availableCount = 0;

                slots.forEach(slot => {
                    const btn = document.createElement("button");
                    btn.type = "button";
                    btn.dataset.id = slot.id;

                    const timeSpan = document.createElement("span");
                    timeSpan.className = "slot-time-text";
                    timeSpan.textContent = slot.formattedTime;
                    btn.appendChild(timeSpan);

                    const badgeSpan = document.createElement("span");
                    badgeSpan.className = "slot-badge-text";

                    if (slot.isAvailable) {
                        availableCount++;
                        btn.className = "slot-selection-btn slot-available";
                        badgeSpan.textContent = "Available";
                        btn.appendChild(badgeSpan);

                        btn.addEventListener("click", function () {
                            document.querySelectorAll(".slot-selection-btn").forEach(b => b.classList.remove("selected"));
                            btn.classList.add("selected");
                            hiddenSlotInput.value = slot.id;
                            updateSummaryDateTime();
                            checkFormValidity();
                        });

                        if (slotToRestore && slot.id.toLowerCase() === slotToRestore.toLowerCase()) {
                            btn.classList.add("selected");
                            hiddenSlotInput.value = slot.id;
                        }
                    } else {
                        btn.className = "slot-selection-btn slot-unavailable";
                        btn.disabled = true;
                        badgeSpan.textContent = "Unavailable";
                        btn.appendChild(badgeSpan);
                    }

                    slotsContainer.appendChild(btn);
                });

                slotsCountBadge.textContent = `${availableCount} Available`;
                updateSummaryDateTime();
                checkFormValidity();
            })
            .catch(err => {
                console.error(err);
                hiddenSlotInput.value = "";
                checkFormValidity();
                if (err.message === "too-many-lookups") {
                    slotsCountBadge.textContent = "Please wait";
                    slotsContainer.innerHTML = "<div class='text-danger p-3 text-center w-100 slots-message-full-width'>You have looked at a lot of days in a short while. Please wait a minute, then pick the day again.</div>";
                    return;
                }
                slotsCountBadge.textContent = "Error";
                slotsContainer.innerHTML = "<div class='text-danger p-3 text-center w-100 slots-message-full-width'>Error loading slots.</div>";
            });
    }

    // ==========================================
    // Vertical Stepper Illumination & Scroll Tracking
    // ==========================================
    const vStep1 = document.getElementById("v-step-1");
    const vStep2 = document.getElementById("v-step-2");
    const vStep3 = document.getElementById("v-step-3");
    const vLine1 = document.getElementById("v-line-1");
    const vLine2 = document.getElementById("v-line-2");

    function updateStepperState() {
        const section1 = document.getElementById("section-step-1");
        const section2 = document.getElementById("section-step-2");
        const section3 = document.getElementById("section-step-3");

        // Check field completion
        const isStep1Done = Boolean(serviceSelect.value && hiddenSlotInput.value);
        const firstName = document.getElementById("CustomerFirstName")?.value.trim();
        const lastName = document.getElementById("CustomerLastName")?.value.trim();
        const email = document.getElementById("Email")?.value.trim();
        const phone = document.getElementById("PhoneNumber")?.value.trim();
        const address = document.getElementById("Address")?.value.trim();
        const postcode = postcodeInput.value.trim();
        const isStep2Done = Boolean(isStep1Done && firstName && lastName && email && phone && address && postcode && isPostcodeServed());

        const problemDesc = document.getElementById("ProblemDescription")?.value.trim();
        const isStep3Done = Boolean(isStep2Done && problemDesc && problemDesc.length >= descriptionMinLength);

        // Check scroll position
        const scrollPos = window.scrollY + 250;
        const sec2Top = section2 ? section2.offsetTop : 99999;
        const sec3Top = section3 ? section3.offsetTop : 99999;

        // Step 1
        if (isStep1Done || scrollPos >= sec2Top) {
            vStep1.className = "v-step-item step-done";
            vLine1.className = "v-step-line line-done";
        } else {
            vStep1.className = "v-step-item step-active";
            vLine1.className = "v-step-line";
        }

        // Step 2
        if (isStep2Done || scrollPos >= sec3Top) {
            vStep2.className = "v-step-item step-done";
            vLine2.className = "v-step-line line-done";
        } else if (isStep1Done || scrollPos >= sec2Top) {
            vStep2.className = "v-step-item step-active";
            vLine2.className = "v-step-line";
        } else {
            vStep2.className = "v-step-item step-inactive";
            vLine2.className = "v-step-line";
        }

        // Step 3
        if (isStep3Done) {
            vStep3.className = "v-step-item step-done";
        } else if (isStep2Done || scrollPos >= sec3Top) {
            vStep3.className = "v-step-item step-active";
        } else {
            vStep3.className = "v-step-item step-inactive";
        }
    }

    window.addEventListener("scroll", updateStepperState);

    // Real-time Form Validity Check for Submit Button
    //
    // Deliberately does NOT call jQuery Unobtrusive Validation's own .valid() here.
    // jQuery's native per-field blur/keyup handlers already show live, real error messages
    // (Required/MinLength/EmailAddress/Phone, from BookingInputModel's own DataAnnotations)
    // in each field's asp-validation-for span on their own, with no help needed - that part
    // was only ever broken by jQuery 4's removal of $.parseJSON, which the bundled
    // jquery-validation-unobtrusive still called (fixed by pinning jQuery back to 3.7.1 in
    // libman.json). Calling $(form).valid() repeatedly from here, once per keystroke, was
    // tried and found to corrupt jQuery Validate's own internal per-field state - even a
    // fresh, isolated .valid() call on a single field afterwards started returning wrong
    // results. So this keeps the original presence-based gate for the button instead, and
    // only adds the one thing that was never covered by DataAnnotations at all: the
    // service/slot pickers above are custom widgets, not typed inputs, so nothing would
    // otherwise tell the user that's what's blocking "Proceed".
    function checkFormValidity() {
        const isStep1Done = Boolean(serviceSelect.value && hiddenSlotInput.value);
        const firstName = document.getElementById("CustomerFirstName")?.value.trim();
        const lastName = document.getElementById("CustomerLastName")?.value.trim();
        const email = document.getElementById("Email")?.value.trim();
        const phone = document.getElementById("PhoneNumber")?.value.trim();
        const address = document.getElementById("Address")?.value.trim();
        const postcode = postcodeInput.value.trim();
        const problemDesc = document.getElementById("ProblemDescription")?.value.trim();

        // The description's minimum is the server's own (BookingInputModel). A postcode
        // outside our districts keeps the button off: its notice says what to do instead.
        const isFormValid = isStep1Done && firstName && lastName && email && phone && address && postcode && isPostcodeServed() && problemDesc && problemDesc.length >= descriptionMinLength;

        updatePostcodeNotice();
        submitBtn.disabled = !isFormValid;
        step1IncompleteHint.classList.toggle("d-none", isStep1Done);
        updateStepperState();
    }

    // Input Event Listeners
    const inputsToWatch = ["CustomerFirstName", "CustomerLastName", "Email", "PhoneNumber", "Address", "Postcode", "ProblemDescription"];
    inputsToWatch.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener("input", checkFormValidity);
    });

    // Initializations
    filterServices();
    renderCalendar();
    loadSlotsForDate(selectedDateStr);
});
