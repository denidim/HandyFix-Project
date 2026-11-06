// The search box above an admin list (jobs, enquiries, technicians, areas, services): it hides
// the rows that do not contain what is typed. The box names its table body in data-search-target.
// The five pages used to carry five identical copies of this as inline scripts.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('input[data-search-target]').forEach(searchInput => {
        const rows = document.querySelectorAll(searchInput.dataset.searchTarget + ' tr');

        searchInput.addEventListener('input', (e) => {
            const term = e.target.value.toLowerCase();
            rows.forEach(row => {
                const text = row.innerText.toLowerCase();
                row.style.display = text.includes(term) ? '' : 'none';
            });
        });
    });
});
