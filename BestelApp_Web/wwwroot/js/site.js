// Basis UI scripts (simpel gehouden)

// Navbar scroll effect (premium)
document.addEventListener("DOMContentLoaded", function () {
    const navbar = document.querySelector(".luxe-navbar");
    if (!navbar) return;

    function updateNavbar() {
        if (window.scrollY > 50) {
            navbar.classList.add("is-scrolled");
        } else {
            navbar.classList.remove("is-scrolled");
        }
    }

    window.addEventListener("scroll", updateNavbar);
    updateNavbar();

    // Lucide icons renderen (als lucide script geladen is)
    if (window.lucide && typeof window.lucide.createIcons === "function") {
        window.lucide.createIcons();
    }
});
