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
    const serviceSelect = document.getElementById("widget-service");
    const slotsContainerGroup = document.getElementById("slots-container-group");
    const slotsContainer = document.getElementById("widget-slots");
    const hiddenSlotIdInput = document.getElementById("widget-slot-id");
    const submitBtn = document.getElementById("widget-submit-btn");

    function checkFormValidity() {
        if (serviceSelect.value && dateInput.value && hiddenSlotIdInput.value) {
            submitBtn.removeAttribute("disabled");
        } else {
            submitBtn.setAttribute("disabled", "true");
        }
    }

    // Reset slots when service changes
    serviceSelect.addEventListener("change", function () {
        hiddenSlotIdInput.value = "";
        checkFormValidity();
    });

    dateInput.addEventListener("change", async function () {
        const dateValue = dateInput.value;
        if (!dateValue) {
            slotsContainerGroup.style.display = "none";
            hiddenSlotIdInput.value = "";
            checkFormValidity();
            return;
        }

        slotsContainer.innerHTML = "<div class='text-center text-muted p-2 w-100 small'>Checking slots...</div>";
        slotsContainerGroup.style.display = "block";
        hiddenSlotIdInput.value = "";
        checkFormValidity();

        try {
            const response = await fetch(`/Booking/GetSlots?date=${dateValue}`);
            if (!response.ok) throw new Error("Error fetching slots");

            const slotsData = await response.json();
            slotsContainer.innerHTML = "";

            if (slotsData.length === 0) {
                slotsContainer.innerHTML = "<div class='text-danger text-center p-2 w-100 small'>No slots available. Try another date!</div>";
                return;
            }

            slotsData.forEach(slot => {
                const btn = document.createElement("button");
                btn.type = "button";
                btn.className = "btn btn-outline-secondary slot-selection-btn btn-sm m-1";
                btn.textContent = slot.formattedTime;
                btn.dataset.id = slot.id;

                btn.addEventListener("click", function () {
                    document.querySelectorAll("#widget-slots .slot-selection-btn").forEach(b => {
                        b.className = "btn btn-outline-secondary slot-selection-btn btn-sm m-1";
                    });

                    btn.className = "btn btn-primary slot-selection-btn btn-sm m-1";
                    hiddenSlotIdInput.value = slot.id;
                    checkFormValidity();
                });

                slotsContainer.appendChild(btn);
            });
        } catch (error) {
            console.error(error);
            slotsContainer.innerHTML = "<div class='text-danger text-center p-2 w-100 small'>Failed to load availability.</div>";
        }
    });
});
