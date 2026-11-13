// The picture text's copy button on the admin Service Area form
// (Areas/Administration/Views/ServiceAreas/_ServiceAreaForm.cshtml). Its question rows are
// admin-faq-builder.js.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.getElementById('service-area-form-root');
        if (!root) {
            return;
        }

        // The text for the Gemini app names the town as the name box has it, and "Copy" puts it
        // on the clipboard (PROJECT_STATE.md Section 3dl).
        var nameInput = document.getElementById('area-name');
        var prompt = document.getElementById('area-style-prompt');
        var copyButton = document.getElementById('copy-style-prompt');

        if (prompt && nameInput) {
            var town = prompt.getAttribute('data-town');
            nameInput.addEventListener('input', function () {
                var newTown = nameInput.value.trim() || '[town name]';
                prompt.value = prompt.value.replace(town + ', Surrey', newTown + ', Surrey');
                town = newTown;
            });
        }

        if (prompt && copyButton) {
            copyButton.addEventListener('click', function () {
                function copied() {
                    copyButton.textContent = 'Copied';
                    setTimeout(function () { copyButton.textContent = 'Copy'; }, 2000);
                }

                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(prompt.value).then(copied);
                } else {
                    // Without a secure page (a local run over http) the clipboard API is not
                    // there; selecting the text and copying still works.
                    prompt.select();
                    document.execCommand('copy');
                    copied();
                }
            });
        }
    });
}());
