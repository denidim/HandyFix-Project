// Updates the total shown on the "Pay Now" button when a customer picks or types an optional tip
// on the invoice payment page (Views/Payment/Invoice.cshtml).
(function () {
    const balanceEl = document.getElementById('invoice-balance-amount');
    const tipInput = document.getElementById('tip-amount-input');
    const payBtnText = document.getElementById('pay-invoice-btn-text');
    const presetBtns = document.querySelectorAll('.tip-preset-btn');

    if (!balanceEl || !tipInput || !payBtnText) {
        return;
    }

    const baseBalance = parseFloat(balanceEl.getAttribute('data-balance')) || 0;

    function updateButtonTotal() {
        const rawTip = parseFloat(tipInput.value);
        const tip = !isNaN(rawTip) && rawTip > 0 ? rawTip : 0;
        const total = (baseBalance + tip).toFixed(2);
        payBtnText.textContent = 'Pay \u00A3' + total + ' Now';
    }

    presetBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            const preset = parseFloat(this.getAttribute('data-tip')) || 0;
            tipInput.value = preset.toFixed(2);
            updateButtonTotal();
        });
    });

    tipInput.addEventListener('input', updateButtonTotal);
})();

