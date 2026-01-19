async function renderCart() {
  const emptyBox = document.getElementById("cartEmpty");
  const hasBox = document.getElementById("cartHasItems");
  const list = document.getElementById("cartList");
  const noImg =
    list?.dataset.noimg && list.dataset.noimg.trim()
      ? list.dataset.noimg.trim()
      : "/images/no-image.png";
  const totalEl = document.getElementById("cart-total");
  const footer = document.getElementById("cartFooter");

  // default: δείξε άδειο μέχρι να απαντήσει το API
  emptyBox?.classList.remove("d-none");
  hasBox?.classList.add("d-none");
  footer?.classList.add("d-none");
  if (list) list.innerHTML = "";

  try {
    const r = await fetch("/umbraco/api/cart/get", {
      cache: "no-store",
      credentials: "same-origin",
    });
    console.log("[Cart] GET status:", r.status);
    if (!r.ok) throw new Error("API " + r.status);
    const items = await r.json();
    console.log("[Cart] GET items:", items);

    if (!Array.isArray(items) || items.length === 0) {
      console.log("[Cart] → EMPTY VIEW");
      totalEl && (totalEl.textContent = "0");
      footer?.classList.add("d-none");
      return;
    }

    // έχουμε αντικείμενα
    emptyBox?.classList.add("d-none");
    hasBox?.classList.remove("d-none");
    footer?.classList.remove("d-none");
    totalEl && (totalEl.textContent = String(items.length));

    if (list) {
      list.innerHTML = items
        .map((x) => {
          const title = `${x.maker ?? ""} ${x.model ?? ""}`.trim();
          const specs = [
            x.year ?? "",
            x.km != null ? `${Number(x.km).toLocaleString("el-GR")} km` : "",
            x.fuel ?? "",
          ]
            .filter(Boolean)
            .join(" • ");
          const priceTxt =
            (x.priceText &&
              `${Number(x.priceText).toLocaleString("el-GR")} €`) ||
            (x.priceValue != null
              ? `${Number(x.priceValue).toLocaleString("el-GR")} €`
              : "-");

          return `
            <article class="cart-item border p-3 mb-3" data-id="${x.id}">
              <div class="row align-items-center g-3">

                <!-- Εικόνα -->
                <div class="col-auto">
                  <img
                    class="car-img"
                    src="${x.img && String(x.img).trim() ? x.img : noImg}"
                    alt=""
                    onerror="this.onerror=null;this.src=this.dataset.fallback;"
                    data-fallback="${noImg}"
                  >
                </div>

                <!-- Περιεχόμενο: Τίτλος, Specs, Τιμή, Link -->
                <div class="col">
                  <div class="car-title">${title}</div>
                  <div class="text-muted small">${specs}</div>
                  <div class="car-price mt-2">${priceTxt}</div>
                  <a class="small d-inline-block mt-1" style="color:#023859; text-decoration: underline; text-underline-offset: 4px;" href="${x.url || "#"}">Προβολή</a>
                </div>

                <!-- Actions δεξιά: μόνο κάδος -->
                <div class="col-auto text-end">
                  <button class="btn btn-outline-danger btn-sm removeFromCart" data-id="${
                    x.id
                  }" title="Αφαίρεση">
                    <i class="fa-solid fa-trash" style="font-size:13px"></i>
                  </button>
                </div>

              </div>
            </article>
          `;
        })
        .join("");
    }
  } catch (err) {
    console.error("❌ renderCart error:", err);
    // fallback: κράτα το "άδειο"
    emptyBox?.classList.remove("d-none");
    hasBox?.classList.add("d-none");
    footer?.classList.add("d-none");
    totalEl && (totalEl.textContent = "0");
    if (list)
      list.innerHTML = `<div class="alert alert-warning">Αποτυχία φόρτωσης καλαθιού.</div>`;
  }
}

document.addEventListener("DOMContentLoaded", renderCart);

function setCartBadge(n) {
  const el = document.getElementById("offerCartCount");
  if (el) el.textContent = String(n);
}

//-------------ΚΑΘΑΡΙΣΜΟΣ ΜΕΜΩΝΟΜΕΝΟΥ ΚΟΥΜΠΙΟΥ ΚΑΘΑΡΙΣΜΟΥ-----------------
document.addEventListener(
  "click",
  async (e) => {
    const btn = e.target.closest(".removeFromCart");
    if (!btn) return;

    e.preventDefault();

    const id = btn.dataset.id;
    if (!id) return;

    const card = btn.closest(".cart-item");
    const list = document.getElementById("cartList");
    const empty = document.getElementById("cartEmpty");
    const hasBox = document.getElementById("cartHasItems");
    const total = document.getElementById("cart-total");

    // 1) Optimistic UI (μικρό fade + προσωρινή απόκρυψη)
    try {
      card.style.transition = "opacity .15s ease";
      card.style.opacity = "0";
      await new Promise((r) => setTimeout(r, 160));
      card.style.display = "none";
    } catch {}

    try {
      // 2) Κλήση API
      const r = await fetch("/umbraco/api/cart/remove", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ id }),
        credentials: "same-origin",
      });
      if (!r.ok) throw new Error(await r.text());
      const res = await r.json(); // { count, items }
      console.log("[Cart] removed:", id, res);

      // 3) Ενημέρωση badge (site-wide)
      window.dispatchEvent(
        new CustomEvent("cart:updated", { detail: { count: res.count } }),
      );
      await window.updateCartBadgeFromServer?.();
      setCartBadge(res.count);

      // 4) Αν άδειασε, δείξε το empty view
      if (!res.items || !res.items.length) {
        empty?.classList.remove("d-none");
        hasBox?.classList.add("d-none");
        document.getElementById("cartFooter")?.classList.add("d-none");
        if (list) list.innerHTML = "";
        if (total) total.textContent = "0";
        return;
      }

      // 5) Ανανέωσε το σύνολο και ξανα-ζωγράφισε τη λίστα για απόλυτο sync
      if (total) total.textContent = String(res.items.length);
      await (typeof renderCart === "function"
        ? renderCart()
        : Promise.resolve());
    } catch (err) {
      console.error("❌ remove error:", err);
      // rollback αν αποτύχει: επανέφερε την κάρτα
      card.style.display = "";
      card.style.opacity = "1";
    }
  },
  true,
);

// -----------ΟΛΙΚΟΣ ΚΑΘΑΡΙΣΜΟΣ ΤΟΥ ΚΑΛΑΘΙΟΥ!!!-------------------
document.addEventListener("click", async (e) => {
  const clr = e.target.closest("#clearCartBtn");
  if (!clr) return;

  e.preventDefault();
  try {
    const r = await fetch("/umbraco/api/cart/clear", { method: "POST" });
    if (!r.ok) throw new Error("API " + r.status);
    const { count } = await r.json();

    // ενημέρωσε το badge στο navbar
    window.dispatchEvent(
      new CustomEvent("cart:updated", { detail: { count } }),
    );
    await window.updateCartBadgeFromServer?.();

    // ξαναφτιάξε το καλάθι (θα δείξει empty)
    await renderCart();
  } catch (err) {
    console.error("❌ clearCart error:", err);
  }
});

// ===== Helper validation για το modal του καλαθιού =====
const __isValidEmail = (s) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s || "");
const __normalizeGreekMobile = (s) => {
  let d = (s || "").replace(/\D/g, "");
  if (d.startsWith("0030")) d = d.slice(4);
  else if (d.startsWith("30")) d = d.slice(2);
  return d; // 69XXXXXXXX
};
const __isValidGreekMobile = (s) => /^69\d{8}$/.test(__normalizeGreekMobile(s));

// ===== Άνοιγμα modal όταν πατηθεί "Ζήτα προσφορά" =====
document.addEventListener("click", async (e) => {
  const proceed = e.target.closest("#proceedBtn");
  if (!proceed) return;

  e.preventDefault();

  // αν για κάποιο λόγο το καλάθι είναι άδειο, σταμάτα
  try {
    const r = await fetch("/umbraco/api/cart/get", {
      cache: "no-store",
      credentials: "same-origin",
    });
    if (!r.ok) throw new Error("API " + r.status);
    const items = await r.json();
    if (!Array.isArray(items) || items.length === 0) {
      alert("Το καλάθι είναι άδειο.");
      return;
    }
  } catch (err) {
    console.error("❌ proceedBtn error:", err);
    return;
  }

  const modalEl = document.getElementById("#offerModalfromCart");
  if (window.bootstrap && modalEl) {
    (
      bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl)
    ).show();
  }
});

const formEl = document.getElementById("offerForm");
const modalEl = document.getElementById("offerModalfromCart");
const statusEl = document.getElementById("offerStatus");

// ===== Υποβολή προσφοράς από το modal του καλαθιού =====
document.addEventListener("click", async (e) => {
  const submitBtn = e.target.closest("#offerSubmitBtnCart");
  if (!submitBtn) return;
  e.preventDefault();

  const firstName = document.getElementById("firstName")?.value.trim() || "";
  const lastName = document.getElementById("lastName")?.value.trim() || "";
  const email = document.getElementById("email")?.value.trim() || "";
  const phone = document.getElementById("phone")?.value.trim() || "";

  // helpers: __isValidEmail, __isValidGreekMobile, __normalizeGreekMobile πρέπει να υπάρχουν
  const emailOk = __isValidEmail(email);
  const phoneOk = __isValidGreekMobile(phone);
  const normalizedPhone = phoneOk
    ? `+30${__normalizeGreekMobile(phone)}`
    : phone;

  // Διόρθωση IDs στα invalid states (όχι co_email/co_phone)
  document.getElementById("email")?.classList.toggle("is-invalid", !emailOk);
  document.getElementById("phone")?.classList.toggle("is-invalid", !phoneOk);

  if (!firstName || !lastName || !emailOk || !phoneOk) {
    if (statusEl) {
      statusEl.textContent = "Συμπληρώστε σωστά όλα τα πεδία.";
      statusEl.className = "small text-danger";
    }
    return;
  }

  // πάρε τα items του καλαθιού
  let items = [];
  try {
    const r = await fetch("/umbraco/api/cart/get", {
      cache: "no-store",
      credentials: "same-origin",
    });
    if (!r.ok) throw new Error("API " + r.status);
    items = await r.json();
  } catch (err) {
    console.error("❌ get items error:", err);
    if (statusEl) {
      statusEl.textContent = "Αποτυχία φόρτωσης καλαθιού.";
      statusEl.className = "small text-danger";
    }
    return;
  }

  const carIds = [
    ...new Set(
      (items || [])
        .map((x) => Number(x.carId ?? x.id ?? 0))
        .filter((n) => Number.isInteger(n) && n > 0),
    ),
  ];

  if (carIds.length === 0) {
    if (statusEl) {
      statusEl.textContent = "Δεν βρέθηκαν έγκυρα IDs οχημάτων στο καλάθι.";
      statusEl.className = "small text-danger";
    }
    return;
  }

  const payload = {
    firstName,
    lastName,
    email,
    phone: normalizedPhone,
    cars: items.map((x) => ({
      id: String(x.id ?? ""),
      maker: x.maker ?? "",
      model: x.model ?? "",
      priceText: x.priceText ?? "",
      img: x.img ?? "",
      year: typeof x.year === "number" ? x.year : null,
      km: typeof x.km === "number" ? x.km : null,
      fuel: x.fuel ?? "",
      cc: typeof x.cc === "number" ? x.cc : null,
      hp: typeof x.hp === "number" ? x.hp : null,
      color: x.color ?? "",
    })),
  };

  const original = submitBtn.textContent;
  submitBtn.disabled = true;
  submitBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Αποστολή`;
  if (statusEl) statusEl.textContent = "";

  setTimeout(async () => {
    try {
      const r = await fetch("/umbraco/api/modaloffermemberapi/send", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
        credentials: "same-origin",
      });

      if (!r.ok) throw new Error(await r.text());
      const res = await r.json(); // { ok: true }

      const successNote = document.getElementById("offerSuccessNote");
      if (successNote) {
        successNote.style.display = "block";
        successNote.classList.add("offer-success-note");
      }

      // ⏳ δώσε χρόνο να το διαβάσει
      await new Promise((r) => setTimeout(r, 2000));

      // 🔥 διακριτικό fade out
      if (successNote) {
        successNote.classList.add("fade-out");
      }

      await new Promise((r) => setTimeout(r, 800));

      // animation στο modal
      modalEl
        .querySelector(".modal-content")
        ?.style.setProperty("animation", "modalFadeOut 0.25s ease-in");

      // ✅ Κλείσιμο modal
      await waitModalHidden(modalEl);

      // reset
      document.getElementById("offerForm")?.reset();
      if (modalEl) {
        modalEl.addEventListener("show.bs.modal", () => {
          const note = document.getElementById("offerSuccessNote");
          if (note) note.style.display = "none";
          note.classList.remove("offer-success-note");
        });
      }

      // καθάρισε καλάθι
      await fetch("/umbraco/api/cart/clear", { method: "POST" });

      // ενημέρωσε badge + UI
      window.dispatchEvent(
        new CustomEvent("cart:updated", { detail: { count: 0 } }),
      );
      await window.updateCartBadgeFromServer?.();
      await window.renderCart?.();
    } catch (err) {
      console.error("❌ submit offer error:", err);
      if (statusEl) {
        statusEl.style.display = "block";
        statusEl.textContent = "Κάτι πήγε στραβά. Προσπαθήστε ξανά.";
        statusEl.className = "small mt-2 text-danger";
      }
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = original;
      clearUiOverlays();
    }
  }, 1000);
});

// Ασφάλεια να μη γίνει submit με reload
document.addEventListener("submit", (e) => {
  if (e.target?.id === "offerForm") e.preventDefault();
});

window.addEventListener("beforeunload", () => {
  sessionStorage.removeItem("selectedCarId");
});

// Ειναι μετα την αποστολή της προσφοράς να μην μαυριζει η οθονη στον χρηστη και παγωνουν ολα
function waitModalHidden(modalEl) {
  return new Promise((resolve) => {
    if (!modalEl || !window.bootstrap || !bootstrap.Modal) return resolve();
    const onHidden = () => {
      modalEl.removeEventListener("hidden.bs.modal", onHidden);
      resolve();
    };
    modalEl.addEventListener("hidden.bs.modal", onHidden, { once: true });
    const inst = bootstrap.Modal.getOrCreateInstance(modalEl);
    inst.hide();
  });
}

// Fail-safe καθάρισμα από τυχόν overlays/backdrops/lock scroll που έμειναν
function clearUiOverlays() {
  document.body.classList.remove("modal-open");
  document.body.style.overflow = "";
  document
    .querySelectorAll(
      ".modal-backdrop, .nx-overlay, .overlay, .backdrop, .loader",
    )
    .forEach((el) => el.remove());
}
