document.addEventListener("DOMContentLoaded", () => {
  const p = location.pathname.replace(/\/+$/, "");
  const isHome = p === "" || p === "/" || /^\/(el|en)\/?$/.test(p);
  document.body.classList.toggle("is-home", isHome);
});

/* ---------- Badge: offerCartCount (ΚΡΑΤΑΣ) ---------- */
(function () {
  const SELECTORS = ["#offerCartCount"];
  function findBadges() {
    return SELECTORS.flatMap((s) => [...document.querySelectorAll(s)]);
  }
  function setBadge(n) {
    findBadges().forEach((el) => el && (el.textContent = String(n)));
  }

  async function fetchCount() {
    try {
      const r = await fetch("/umbraco/api/cart/count", {
        cache: "no-store",
        credentials: "same-origin",
      });
      if (!r.ok) return 0;
      const { count } = await r.json();
      return Number.isFinite(count) ? count : 0;
    } catch {
      return 0;
    }
  }
  async function syncBadge() {
    setBadge(await fetchCount());
  }

  function whenBadgeReady(cb) {
    if (findBadges().length) return cb();
    const obs = new MutationObserver(() => {
      if (findBadges().length) {
        obs.disconnect();
        cb();
      }
    });
    obs.observe(document.documentElement, { childList: true, subtree: true });
  }

  window.updateCartBadgeFromServer = () => whenBadgeReady(syncBadge);
  document.addEventListener("DOMContentLoaded", () =>
    whenBadgeReady(syncBadge)
  );
  window.addEventListener("pageshow", () => whenBadgeReady(syncBadge));
  window.addEventListener("cart:updated", (ev) => {
    const c = ev.detail?.count;
    if (typeof c === "number") setBadge(c);
    else whenBadgeReady(syncBadge);
  });
})();

(function () {
  var MOBILE_BP = 1024; // μέχρι 991px θεωρούμε mobile
  var menu = document.getElementById("nxMenu");
  var toggle = document.getElementById("nxToggle");
  if (!menu || !toggle) return;

  // overlay – φτιάξ’ το αν δεν υπάρχει
  var overlay = document.getElementById("nxOverlay");
  if (!overlay) {
    overlay = document.createElement("div");
    overlay.id = "nxOverlay";
    overlay.className = "nx-overlay";
    overlay.hidden = true;
    document.body.appendChild(overlay);
  }

  // Βάλε σωστό FA icon στο burger (αν λείπει)
  if (!toggle.querySelector("i")) {
    toggle.innerHTML = '<i class="fa-solid fa-bars" aria-hidden="true"></i>';
  }

  // === helpers ===
  function isMobile() {
    return window.innerWidth <= MOBILE_BP;
  }
  function lockScroll(on) {
    document.documentElement.style.overflow = on ? "hidden" : "";
  }
  function openNav() {
    menu.classList.add("is-open");
    overlay.hidden = false;
    toggle.setAttribute("aria-expanded", "true");
    lockScroll(true);
  }
  function closeNav() {
    menu.classList.remove("is-open");
    overlay.hidden = true;
    toggle.setAttribute("aria-expanded", "false");
    lockScroll(false);
    // κλείσε τυχόν dropdowns μέσα στο menu
    document
      .querySelectorAll(".kx-menu.is-open")
      .forEach((m) => m.classList.remove("is-open"));
    document.querySelectorAll('[aria-expanded="true"]').forEach((b) => {
      if (b !== toggle) b.setAttribute("aria-expanded", "false");
    });
  }
  function toggleNav(e) {
    if (e) {
      e.preventDefault();
      e.stopPropagation();
    }
    menu.classList.contains("is-open") ? closeNav() : openNav();
  }

  // === mode switch (desktop <-> mobile) ===
  function applyMode() {
    if (isMobile()) {
      // κάνε το menu off-canvas
      menu.classList.add("nx-offcanvas");
      // burger on
      toggle.style.display = "inline-flex";
    } else {
      // γύρνα σε desktop κανονική μπάρα
      closeNav();
      menu.classList.remove("nx-offcanvas", "is-open");
      toggle.style.display = "none";
      // καθάρισε τυχόν inline/locked overflow
      lockScroll(false);
    }
  }

  // === wire events ===
  toggle.addEventListener("click", toggleNav, true);
  overlay.addEventListener("click", closeNav);
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") closeNav();
  });
  // Κλείσε όταν γίνεται click σε link
  menu.addEventListener("click", function (e) {
    if (e.target.closest("a")) closeNav();
  });

  // αρχικοποίηση + σε resize
  applyMode();
  window.addEventListener("resize", debounce(applyMode, 120));

  // μικρό debounce για τα resize
  function debounce(fn, wait) {
    var t;
    return function () {
      clearTimeout(t);
      t = setTimeout(fn, wait);
    };
  }

  // helper αν χρειαστείς από αλλού
  window.closeNavbarIfOpen = function () {
    if (menu.classList.contains("is-open")) closeNav();
  };
})();

/* ---------- Dropdowns (ΚΡΑΤΑΣ ΕΝΑ) ---------- */
(function () {
  function wireDropdown(btnId, menuId) {
    var btn = document.getElementById(btnId);
    var menu = document.getElementById(menuId);
    if (!btn || !menu) return;

    function open() {
      menu.classList.add("is-open");
      btn.setAttribute("aria-expanded", "true");
      document.addEventListener("click", onDoc, true);
      document.addEventListener("keydown", onKey);
    }
    function close() {
      menu.classList.remove("is-open");
      btn.setAttribute("aria-expanded", "false");
      document.removeEventListener("click", onDoc, true);
      document.removeEventListener("keydown", onKey);
    }
    function toggle(e) {
      e.preventDefault();
      e.stopPropagation();
      menu.classList.contains("is-open") ? close() : open();
    }
    function onDoc(e) {
      if (e.target === btn || btn.contains(e.target) || menu.contains(e.target))
        return;
      close();
    }
    function onKey(e) {
      if (e.key === "Escape") {
        close();
        btn.focus();
      }
    }

    btn.addEventListener("click", toggle);
  }

  // Δέσε ΜΙΑ φορά όλα τα dropdowns του navbar
  wireDropdown("companyDropdown", "companyMenu"); // Η Εταιρεία μας
  wireDropdown("accountBtn", "accountMenu"); // Logged-in
  wireDropdown("guestBtn", "guestMenu"); // Guest
})();
