// Drives the dynamic FAQ builder and the picture text's copy button on the admin
// Service Area form (Areas/Administration/Views/ServiceAreas/_ServiceAreaForm.cshtml).
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.getElementById('service-area-form-root');
        if (!root) {
            return;
        }

        var rowsContainer = document.getElementById('faq-rows');
        var template = document.getElementById('faq-template');
        var addButton = document.getElementById('add-faq');

        // The model binder needs a gap-free Faqs[0..n] sequence: it stops at the first
        // missing index, so removing row 1 of 3 would silently drop row 2 as well. Every
        // add and remove therefore rewrites the indices across all remaining rows.
        function reindex() {
            var rows = rowsContainer.querySelectorAll('[data-faq-row]');
            Array.prototype.forEach.call(rows, function (row, index) {
                var numberLabel = row.querySelector('.faq-row-number');
                if (numberLabel) {
                    numberLabel.textContent = 'FAQ ' + (index + 1);
                }

                var fields = row.querySelectorAll('[name], [id], [for], [data-valmsg-for]');
                Array.prototype.forEach.call(fields, function (el) {
                    ['name', 'id', 'for', 'data-valmsg-for'].forEach(function (attr) {
                        var value = el.getAttribute(attr);
                        if (!value) {
                            return;
                        }

                        // Razor emits name="Faqs[0].Question" but id="Faqs_0__Question",
                        // so both bracket and underscore forms have to be handled.
                        el.setAttribute(
                            attr,
                            value
                                .replace(/Faqs\[\d+\]/, 'Faqs[' + index + ']')
                                .replace(/Faqs_\d+__/, 'Faqs_' + index + '__'));
                    });
                });
            });
        }

        if (addButton && template && rowsContainer) {
            addButton.addEventListener('click', function () {
                var index = rowsContainer.querySelectorAll('[data-faq-row]').length;
                var fragment = template.content.cloneNode(true);

                Array.prototype.forEach.call(fragment.querySelectorAll('[name]'), function (el) {
                    el.setAttribute('name', el.getAttribute('name').replace('__PREFIX__', 'Faqs[' + index + ']'));
                });

                rowsContainer.appendChild(fragment);
                reindex();

                var added = rowsContainer.lastElementChild;
                var firstInput = added && added.querySelector('input');
                if (firstInput) {
                    firstInput.focus();
                }
            });
        }

        if (rowsContainer) {
            rowsContainer.addEventListener('click', function (e) {
                var removeButton = e.target.closest('.remove-faq');
                if (!removeButton) {
                    return;
                }

                var row = removeButton.closest('[data-faq-row]');
                if (row) {
                    row.remove();
                    reindex();
                }
            });
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
