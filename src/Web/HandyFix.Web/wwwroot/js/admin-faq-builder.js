// The question rows of the admin forms that have them: a town's (ServiceAreas/_ServiceAreaForm)
// and a service's (Services/_ServicePageFields). Add puts a new row from the page's template,
// the cross takes one away.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
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
    });
}());
