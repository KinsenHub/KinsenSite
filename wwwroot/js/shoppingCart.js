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

    if (!r.ok) throw new Error("API " + r.status);
    const items = await r.json();

    if (!Array.isArray(items) || items.length === 0) {
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

document.addEventListener("click", async (e) => {
  const btn = e.target.closest("#cartSubmitOfferBtn");
  if (!btn) return;

  e.preventDefault();

  const originalText = btn.textContent;
  btn.disabled = true;
  btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Αποστολή...`;

  try {
    const r = await fetch("/umbraco/api/cart/get", {
      cache: "no-store",
      credentials: "same-origin",
    });
    if (!r.ok) throw new Error("Cart fetch failed");

    const items = await r.json();
    if (!Array.isArray(items) || items.length === 0) {
      btn.disabled = false;
      btn.textContent = originalText;
      alert("Το καλάθι είναι άδειο.");
      return;
    }

    const payload = {
      cars: items.map((x) => ({
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

    const send = await fetch("/umbraco/api/modaloffermemberapi/send", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify(payload),
    });

    if (!send.ok) throw new Error(await send.text());

    // ✅ SUCCESS message μέσα στο κουμπί
    btn.innerHTML = `✓ Θα ενημερωθείτε σύντομα με email`;
    btn.classList.add("btn-success");
    await new Promise((resolve) => setTimeout(resolve, 3000));

    // ✅ καθάρισε καλάθι (backend)
    await CartAPI.clear();

    // ✅ ενημέρωσε cart icon
    window.dispatchEvent(
      new CustomEvent("cart:updated", { detail: { count: 0 } }),
    );

    // ✅ ΚΑΘΑΡΙΣΕ UI ΚΑΛΑΘΙΟΥ
    const hasItems = document.getElementById("cartHasItems");
    const emptyBox = document.getElementById("cartEmpty");

    // κρύψε ΟΛΟ το section με τα αυτοκίνητα
    if (hasItems) hasItems.classList.add("d-none");

    // δείξε το empty state
    if (emptyBox) emptyBox.classList.remove("d-none");

    // ❌ ΜΗΝ ξαναστείλει
    btn.style.display = "none";

    // ⏳ κράτα το μήνυμα 2.5 sec και γύρνα στο default
    setTimeout(() => {
      btn.disabled = false;
      btn.textContent = originalText;
    }, 3000);
  } catch (err) {
    console.error("❌ Offer error:", err);

    // ❌ ERROR state μέσα στο κουμπί
    btn.innerHTML = `✗ Αποτυχία. Ξανά δοκιμή`;
    btn.classList.add("btn-danger");

    setTimeout(() => {
      btn.classList.remove("btn-danger");
      btn.disabled = false;
      btn.textContent = originalText;
    }, 3500);
  }
});

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
