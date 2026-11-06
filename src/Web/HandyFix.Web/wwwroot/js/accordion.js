// The question-and-answer lists on the FAQ and Pricing pages. A click on a question opens its
// answer and closes the others in the same list (data-accordion); a list marked
// data-accordion-open-first opens on its first question, as the FAQ page does. Each page used to
// carry its own copy of this as an inline script.
document.addEventListener('click', function (event) {
    const button = event.target.closest('[data-accordion-toggle]');
    if (!button) return;

    const list = button.closest('[data-accordion]');
    const item = button.closest('.accordion-item');
    const isActive = item.classList.contains('active');

    list.querySelectorAll('.accordion-item').forEach(el => {
        el.classList.remove('active');
    });

    if (!isActive) {
        item.classList.add('active');
    }
});

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-accordion-open-first]').forEach(list => {
        const firstItem = list.querySelector('.accordion-item');
        if (firstItem) firstItem.classList.add('active');
    });
});
