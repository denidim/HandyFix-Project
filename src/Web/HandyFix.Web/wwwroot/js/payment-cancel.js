// The payment page a customer comes back to (Views/Payment/Cancel.cshtml): "Try again" shows
// that it is on its way to Stripe for a second before it goes.
// Micro-interaction for the retry button loading state
const retryBtn = document.getElementById('retry-btn');
if (retryBtn) {
    retryBtn.addEventListener('click', function (e) {
        // Prevent immediate navigation so the user sees transition
        e.preventDefault();
        const targetHref = this.getAttribute('href');

        this.innerHTML = `
            <svg class="animate-spin spinner-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            <span>Reconnecting to Stripe...</span>
        `;

        setTimeout(() => {
            window.location.href = targetHref;
        }, 1000);
    });
}
