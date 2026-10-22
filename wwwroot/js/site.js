// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    const historialLinks = document.querySelectorAll('[asp-route-rango]');

    historialLinks.forEach((link) => {
        link.addEventListener('click', async (event) => {
            event.preventDefault();

            const target = event.currentTarget;
            const rango = target.getAttribute('asp-route-rango');
            if (!rango) return;

            const container = document.querySelector('.panel');
            if (!container) return;

            const previous = container.innerHTML;
            container.innerHTML = '<div class="text-center py-5">Cargando historial...</div>';

            try {
                const response = await fetch(`/Home/Historial?rango=${encodeURIComponent(rango)}`);
                const html = await response.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newContent = doc.querySelector('.panel');

                if (newContent) {
                    container.innerHTML = newContent.innerHTML;
                } else {
                    container.innerHTML = previous;
                }
            } catch (error) {
                container.innerHTML = previous;
            }
        });
    });
});
