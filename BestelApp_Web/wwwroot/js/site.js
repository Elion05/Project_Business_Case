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

// =========================
// Luxury dropdown (custom) + filter bar helpers
// =========================
document.addEventListener("DOMContentLoaded", function () {
    const dropdowns = Array.from(document.querySelectorAll(".dd-luxe"));
    if (dropdowns.length === 0) return;

    function closeAll(except) {
        dropdowns.forEach((dd) => {
            if (dd !== except) dd.classList.remove("is-open");
        });
    }

    function openDropdown(dd) {
        closeAll(dd);
        dd.classList.add("is-open");
        const first = dd.querySelector(".dd-item");
        if (first) first.focus();
    }

    function closeDropdown(dd) {
        dd.classList.remove("is-open");
        const trigger = dd.querySelector(".dd-trigger");
        if (trigger) trigger.focus();
    }

    dropdowns.forEach((dd) => {
        const trigger = dd.querySelector(".dd-trigger");
        const hidden = dd.querySelector("input[type='hidden']");
        const valueEl = dd.querySelector(".dd-value");
        const items = Array.from(dd.querySelectorAll(".dd-item"));

        if (!trigger || !hidden || !valueEl || items.length === 0) return;

        function setValue(val, label) {
            hidden.value = val;
            valueEl.textContent = label;
            items.forEach((btn) => btn.classList.remove("is-selected"));
            const found = items.find((btn) => (btn.getAttribute("data-value") ?? "") === val);
            if (found) found.classList.add("is-selected");
        }

        trigger.addEventListener("click", function () {
            const isOpen = dd.classList.contains("is-open");
            if (isOpen) closeDropdown(dd);
            else openDropdown(dd);
        });

        trigger.addEventListener("keydown", function (e) {
            if (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                openDropdown(dd);
            }
            if (e.key === "Escape") {
                e.preventDefault();
                closeDropdown(dd);
            }
        });

        items.forEach((btn, idx) => {
            btn.addEventListener("click", function () {
                const val = btn.getAttribute("data-value") ?? "";
                const label = btn.textContent?.trim() ?? "";
                setValue(val, label);
                closeDropdown(dd);
            });

            btn.addEventListener("keydown", function (e) {
                if (e.key === "Escape") {
                    e.preventDefault();
                    closeDropdown(dd);
                    return;
                }
                if (e.key === "ArrowDown") {
                    e.preventDefault();
                    const next = items[idx + 1] ?? items[0];
                    next.focus();
                }
                if (e.key === "ArrowUp") {
                    e.preventDefault();
                    const prev = items[idx - 1] ?? items[items.length - 1];
                    prev.focus();
                }
                if (e.key === "Enter") {
                    e.preventDefault();
                    btn.click();
                }
            });
        });

        // Initialize selected label from hidden input + selected item marker
        const currentVal = hidden.value ?? "";
        const initial = items.find((btn) => (btn.getAttribute("data-value") ?? "") === currentVal) ?? items[0];
        setValue(currentVal, initial.textContent?.trim() ?? "");
    });

    document.addEventListener("click", function (e) {
        const target = e.target;
        if (!(target instanceof Element)) return;
        const inside = target.closest(".dd-luxe");
        if (!inside) closeAll(null);
    });
});

// Shoes filter helpers (clear + reset)
document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("shoesFilterForm");
    if (!(form instanceof HTMLFormElement)) return;

    const search = document.getElementById("filterSearch");
    const clearBtn = document.getElementById("filterClearBtn");
    const resetBtn = document.getElementById("filterResetBtn");

    let lastSearchValue = "";
    let clearTimer = null;

    function submitSoon() {
        if (clearTimer) window.clearTimeout(clearTimer);
        clearTimer = window.setTimeout(() => form.submit(), 150);
    }

    if (search instanceof HTMLInputElement) {
        lastSearchValue = search.value || "";
        search.addEventListener("input", function () {
            const v = search.value || "";
            // Auto-update when search is cleared
            if (lastSearchValue !== "" && v === "") {
                submitSoon();
            }
            lastSearchValue = v;
        });
    }

    if (clearBtn instanceof HTMLButtonElement && search instanceof HTMLInputElement) {
        clearBtn.addEventListener("click", function () {
            if (search.value !== "") {
                search.value = "";
                search.dispatchEvent(new Event("input", { bubbles: true }));
                search.focus();
            }
        });
    }

    if (resetBtn instanceof HTMLButtonElement) {
        resetBtn.addEventListener("click", function () {
            // Clear search
            if (search instanceof HTMLInputElement) search.value = "";

            // Reset hidden inputs to defaults
            const category = form.querySelector("input[name='categoryId']");
            const gender = form.querySelector("input[name='gender']");
            const sortBy = form.querySelector("input[name='sortBy']");
            if (category instanceof HTMLInputElement) category.value = "";
            if (gender instanceof HTMLInputElement) gender.value = "Alles";
            if (sortBy instanceof HTMLInputElement) sortBy.value = "nieuwste";

            // Update visible labels if dropdowns exist
            const dds = form.querySelectorAll(".dd-luxe");
            dds.forEach((dd) => {
                const hidden = dd.querySelector("input[type='hidden']");
                const items = Array.from(dd.querySelectorAll(".dd-item"));
                const valueEl = dd.querySelector(".dd-value");
                if (!(hidden instanceof HTMLInputElement) || !(valueEl instanceof Element) || items.length === 0) return;

                const val = hidden.value ?? "";
                items.forEach((btn) => btn.classList.remove("is-selected"));
                const selected = items.find((btn) => (btn.getAttribute("data-value") ?? "") === val) ?? items[0];
                selected.classList.add("is-selected");
                valueEl.textContent = (selected.textContent || "").trim();
            });

            form.submit();
        });
    }
});
