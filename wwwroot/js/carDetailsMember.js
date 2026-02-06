// Αρχικοποίηση storage
window.installmentsByCar = (() => {
  try {
    return JSON.parse(sessionStorage.getItem("installmentsByCar") || "{}");
  } catch {
    return {};
  }
})();

function getCartBadgeCount() {
  const el = document.getElementById("offerCartCount");
  return el ? parseInt(el.textContent, 10) || 0 : 0;
}
function setCartBadgeCount(n) {
  const el = document.getElementById("offerCartCount");
  if (el) el.textContent = String(n);
}

setTimeout(() => {
  const items = document.querySelectorAll(".dropdown-item");
  const dropdownButton = document.querySelector(".custom-dropdown-button");
  const resultSpan = document.getElementById("installmentValue");
  const depositInput = document.getElementById("inputProkatavoli");

  if (!dropdownButton || !resultSpan) return;

  // ---------------- helpers ----------------
  const vatMultiplier = 1.24;

  // price από το UI (π.χ. "30.500 €")
  const priceText = document.querySelector(".price-value")?.innerText || "";
  const basePrice = Number(
    priceText
      .replace(/\./g, "") // remove thousands dot
      .replace(",", ".") // decimal comma -> dot
      .replace(/[^\d.]/g, ""), // keep digits + dot
  );

  const formatEUR = (n) =>
    Number(n).toLocaleString("el-GR", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });

  const parseDeposit = () => {
    // input έχει μόνο digits (λόγω oninput), άρα είναι safe
    const v = (depositInput?.value || "").trim();
    if (!v) return 0;
    const num = Number(v);
    return Number.isFinite(num) ? num : 0;
  };

  // carId για αποθήκευση
  const carId = Number(
    document.querySelector("[data-car-id]")?.dataset.carId || 0,
  );

  // κρατάμε σε μνήμη τι έχει διαλέξει ο χρήστης
  let selectedPlan = dropdownButton.dataset.plan || "efapaks"; // "efapaks" ή "6" κλπ

  // ---------------- core calc ----------------
  const updateInstallment = () => {
    // αν δεν έχουμε τιμή, δεν υπολογίζουμε τίποτα
    if (!Number.isFinite(basePrice) || basePrice <= 0) {
      resultSpan.textContent = "-";
      return;
    }

    // efapaks => δεν έχει δόση
    if (selectedPlan === "efapaks") {
      resultSpan.textContent = "-";
      // αποθήκευση
      if (carId > 0) {
        window.installmentsByCar = window.installmentsByCar || {};
        window.installmentsByCar[carId] = {
          paymentPlan: selectedPlan,
          perMonth: null,
          deposit: parseDeposit(),
        };
        sessionStorage.setItem(
          "installmentsByCar",
          JSON.stringify(window.installmentsByCar),
        );
      }
      return;
    }

    const months = parseInt(selectedPlan, 10);
    if (!Number.isFinite(months) || months <= 0) {
      resultSpan.textContent = "-";
      return;
    }

    const totalWithVAT = basePrice * vatMultiplier;

    const deposit = parseDeposit();
    const remaining = totalWithVAT - deposit;

    // αν προκαταβολή μεγαλύτερη από το σύνολο => δεν βγάζουμε δόση
    if (remaining <= 0) {
      resultSpan.textContent = "-";
      return;
    }

    const perMonth = remaining / months;

    // ✅ κόμμα στα δεκαδικά (el-GR)
    resultSpan.innerHTML = `<strong>${formatEUR(
      perMonth,
    )} €</strong> / μήνα (με ΦΠΑ)`;

    // αποθήκευση
    if (carId > 0) {
      window.installmentsByCar = window.installmentsByCar || {};
      window.installmentsByCar[carId] = {
        paymentPlan: selectedPlan,
        perMonth: Number(perMonth.toFixed(2)),
        deposit: deposit,
      };
      sessionStorage.setItem(
        "installmentsByCar",
        JSON.stringify(window.installmentsByCar),
      );
    }
  };

  // ---------------- events ----------------
  items.forEach((item) => {
    item.addEventListener("click", function () {
      const selectedText = this.innerText;
      const selectedValue = this.value; // "efapaks" ή "6" | "12" | ...

      dropdownButton.textContent = selectedText;
      dropdownButton.dataset.plan = selectedValue;

      selectedPlan = selectedValue;
      updateInstallment();
    });
  });

  // όταν γράφει προκαταβολή, κάνε live update (αν έχει επιλέξει μήνες)
  if (depositInput) {
    depositInput.addEventListener("input", () => {
      updateInstallment();
    });
  }

  // initial render (σε περίπτωση που έχει default επιλογή)
  updateInstallment();
}, 500);

// 1) Cart API client (μιλάει με /umbraco/api/cart/*)
const CartAPI = {
  async add(item) {
    const r = await fetch("/umbraco/api/cart/add", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify(item),
    });
    if (!r.ok) throw new Error(await r.text());
    return r.json(); // { count, items }
  },
  async count() {
    const r = await fetch("/umbraco/api/cart/count");
    if (!r.ok) return { count: 0 };
    return r.json(); // { count }
  },
  async get() {
    const r = await fetch("/umbraco/api/cart/get");
    if (!r.ok) throw new Error(await r.text());
    return r.json(); // CartItem[]
  },
  async remove(id) {
    const r = await fetch("/umbraco/api/cart/remove", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ id }),
    });
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  },
  async clear() {
    const r = await fetch("/umbraco/api/cart/clear", { method: "POST" });
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  },
};

// 2) Badge από server
async function updateCartBadgeFromServer() {
  try {
    const { count } = await CartAPI.count();
    const el = document.getElementById("offerCartCount");
    if (el) el.textContent = String(count);
  } catch (e) {
    console.warn("[Cart][badge] error:", e);
  }
}
// κάν’ την διαθέσιμη και global για layout/άλλα scripts
window.updateCartBadgeFromServer =
  window.updateCartBadgeFromServer || updateCartBadgeFromServer;

// 3) normalizeCar (maker/model & priceText/priceValue)
function normalizeCar(c) {
  const id =
    typeof c?.id === "number"
      ? c.id
      : /^\d+$/.test(String(c?.id ?? c?.Id ?? c?.carId ?? c?.carID ?? ""))
        ? parseInt(String(c.id ?? c.Id ?? c.carId ?? c.carID), 10)
        : null;

  const maker = String(c?.maker ?? c?.Maker ?? "").trim(); // <- maker (σωστό)
  const model = String(c?.model ?? c?.Model ?? "").trim();

  const title = [maker, model].filter(Boolean).join(" ").trim();

  const rawPrice = c?.price ?? c?.Price ?? c?.priceText ?? c?.PriceText ?? "";
  let priceText = null,
    priceValue = null;
  if (rawPrice != null) {
    if (typeof rawPrice === "number") {
      priceText = rawPrice.toLocaleString("el-GR");
      priceValue = rawPrice;
    } else {
      priceText = String(rawPrice); // όπως έρχεται (π.χ. "15.000")
      const digits = priceText.replace(/[^\d]/g, "");
      if (digits) priceValue = Number(digits);
    }
  }

  const img = c?.image ?? c?.imageUrl ?? c?.carPic ?? c?.photo ?? "";
  const url = location.pathname + location.search;
  const year = c?.year ?? c?.Year ?? null;
  const km = c?.km ?? c?.Km ?? c?.mileage ?? null;
  const fuel = c?.fuel ?? c?.Fuel ?? c?.fuelType ?? "";
  const cc = c?.cc ?? c?.Cc ?? null;
  const hp = c?.hp ?? c?.Hp ?? null;
  const color = c?.color ?? c?.Color ?? "";

  return {
    id,
    maker,
    model,
    title,
    priceText,
    priceValue,
    img,
    url,
    year,
    km,
    fuel,
    cc,
    hp,
    color,
  };
}

// 4) API του αυτοκινήτου
async function fetchCarById(id) {
  const r = await fetch("/umbraco/api/CarApiMember/getcarbyid", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "same-origin",
    body: JSON.stringify({ id: id }),
  });
  if (!r.ok) {
    const txt = await r.text().catch(() => "");
    throw new Error("API " + r.status + (txt ? " - " + txt : ""));
  }
  const raw = await r.json();
  return normalizeCar(raw);
}

// Αφορά για την εμφανιση του μηνύματος αμα είναι
// ηδη γεμάτο το καλάθι και πάει να προσθέσει το ίδιο προϊόν
function showToast(text, type = "info") {
  const box =
    document.getElementById("toastBox") ||
    (() => {
      const d = document.createElement("div");
      d.id = "toastBox";
      d.style.position = "fixed";
      d.style.right = "16px";
      d.style.bottom = "16px";
      d.style.zIndex = "9999";
      document.body.appendChild(d);
      return d;
    })();
  const t = document.createElement("div");
  t.className = `alert alert-${type}`;
  t.textContent = text;
  t.style.minWidth = "280px";
  t.style.boxShadow = "0 8px 24px rgba(0,0,0,.12)";
  box.appendChild(t);
  setTimeout(() => t.remove(), 2500);
}

// API: ρωτάει τον server αν υπάρχει ήδη στο καλάθι
async function cartContains(id) {
  const r = await fetch(
    `/umbraco/api/cart/contains?id=${encodeURIComponent(id)}`,
    { credentials: "same-origin" },
  );
  if (!r.ok) return false;
  const data = await r.json().catch(() => ({}));
  return !!data.contains;
}

document.addEventListener("DOMContentLoaded", async () => {
  // Βρες ID (session/query/data-attr)
  const btnProbe = document.querySelector(
    "#addToCartBtn button, .addToCartBtn",
  );
  const id =
    (sessionStorage.getItem("selectedCarId") || "").trim() ||
    (new URLSearchParams(location.search).get("id") || "").trim() ||
    (btnProbe?.getAttribute?.("data-car-id") || "").trim();

  if (!id) {
    console.warn("❗ Δεν βρέθηκε carId στη σελίδα");
    await updateCartBadgeFromServer();
    return;
  }

  try {
    window.CURRENT_CAR = await fetchCarById(id);
  } catch (err) {
    console.error("❌ API error:", err);
    await updateCartBadgeFromServer();
    return;
  }

  await updateCartBadgeFromServer();

  // helpers για σίγουρη μετατροπή αριθμών
  const toIntOrNull = (v) => {
    if (v == null) return null;
    const digits = String(v).replace(/[^\d]/g, "");
    return digits ? parseInt(digits, 10) : null;
  };
  const toIntOrNullStrict = (v) =>
    typeof v === "number" && Number.isFinite(v)
      ? Math.trunc(v)
      : toIntOrNull(v);

  // Delegation για σιγουριά
  document.addEventListener(
    "click",
    async (e) => {
      const btn = e.target.closest("#addToCartBtn button, .addToCartBtn");
      if (!btn) return;

      e.preventDefault();
      e.stopPropagation();
      e.stopImmediatePropagation();

      // --- Σίγουρο id από ΚΑΠΟΥ ---
      const id = String(
        (window.CURRENT_CAR?.id ?? "").toString().trim() ||
          (sessionStorage.getItem("selectedCarId") || "").trim() ||
          (new URLSearchParams(location.search).get("id") || "").trim() ||
          (btn.getAttribute("data-car-id") || "").trim(),
      );

      if (!id) {
        console.error("❌ Δεν βρέθηκε id για /cart/add");
        return;
      }

      // === ΔΙΑΒΑΣΕ την επιλογή δόσεων για το συγκεκριμένο car ===
      const instMap = window.installmentsByCar || {};
      // τα κλειδιά μπορεί να είναι "123" ή 123 — έλεγξε και τα δύο
      const inst = instMap[id] || instMap[Number(id)] || {};
      const paymentPlan =
        typeof inst.paymentPlan === "string" ? inst.paymentPlan : null;
      const perMonthNum =
        typeof inst.perMonth === "number" && isFinite(inst.perMonth)
          ? Number(inst.perMonth.toFixed(2))
          : null;

      // --- Payload με σωστούς αριθμούς ---
      const c = window.CURRENT_CAR || {};
      const payload = {
        id,
        maker: c.maker ?? "",
        model: c.model ?? "",
        title: c.title ?? "",
        priceText: c.priceText ?? "",
        priceValue: toIntOrNullStrict(c.priceValue ?? c.priceText),
        img: c.img ?? "",
        url: c.url ?? location.pathname + location.search,
        year: toIntOrNullStrict(c.year),
        km: toIntOrNullStrict(c.km),
        fuel: c.fuel ?? "",
        cc: toIntOrNullStrict(c.cc),
        hp: toIntOrNullStrict(c.hp),
        color: typeof c.color === "string" ? c.color.trim() : "",
        paymentPlan,
        perMonth: perMonthNum,
      };

      try {
        const already = await cartContains(id); // ή await CartAPI.contains(id)
        const cartMsg = document.getElementById("cartMsg");

        if (already) {
          cartMsg.textContent =
            "Το συγκεκριμένο αυτοκίνητο υπάρχει ήδη στο καλάθι σου.";
          cartMsg.className =
            "cartMsg warning d-flex justify-content-center mt-3";

          // badge bounce (σωστό, το κρατάμε)
          const badge = document.querySelector("[data-cart-badge]");
          if (badge) {
            badge.classList.add("animate-bounce");
            setTimeout(() => badge.classList.remove("animate-bounce"), 600);
          }

          // auto-hide
          setTimeout(() => {
            cartMsg.textContent = "";
            cartMsg.className = "cartMsg d-flex justify-content-center mt-3";
          }, 3500);

          return;
        }

        const before = getCartBadgeCount();
        setCartBadgeCount(before + 1);
        window.dispatchEvent(
          new CustomEvent("cart:updated", { detail: { count: before + 1 } }),
        );

        // Κλήση στο API
        const res = await CartAPI.add(payload); // { count, items }

        setCartBadgeCount(res.count);
        window.dispatchEvent(
          new CustomEvent("cart:updated", { detail: { count: res.count } }),
        );

        cartMsg.textContent = "Το αυτοκίνητο προστέθηκε στο καλάθι σου.";
        cartMsg.className =
          "cartMsg success d-flex justify-content-center mt-3";

        // auto-hide
        setTimeout(() => {
          cartMsg.textContent = "";
          cartMsg.className = "cartMsg d-flex justify-content-center mt-3";
        }, 3000);

        // --- feedback στο κουμπί (κρατάμε το δικό σου) ---
        const prev = btn.textContent;
        btn.disabled = true;
        btn.textContent = "Στο καλάθι ✓";

        setTimeout(() => {
          btn.disabled = false;
          btn.textContent = prev;
        }, 3500);
      } catch (err) {
        if (
          err &&
          (err.code === "DUPLICATE" || /duplicate/i.test(err.message || ""))
        ) {
          showToast(
            "Το συγκεκριμένο αυτοκίνητο υπάρχει ήδη στο καλάθι σου.",
            "warning",
          );
          // αν ο server έστειλε count, συγχρόνισε
          if (typeof err.count === "number") {
            setCartBadgeCount(err.count);
            window.dispatchEvent(
              new CustomEvent("cart:updated", { detail: { count: err.count } }),
            );
          }
          return;
        }

        // Αν αποτύχει το API, επανέφερε το badge
        const before = getCartBadgeCount();
        setCartBadgeCount(Math.max(0, before - 1));
        window.dispatchEvent(
          new CustomEvent("cart:updated", {
            detail: { count: getCartBadgeCount() },
          }),
        );
        console.error("❌ add error:", err);
      }
    },
    true,
  );
});

// // ΑΦΟΡΑ ΤΟ ΚΟΥΜΠΙ ΑΓΑΠΗΜΕΝΑ :
// document.addEventListener("click", async (e) => {
//   const btn = e.target.closest(".favBtn");
//   if (!btn) return;

//   e.preventDefault();
//   e.stopPropagation();

//   const carId = Number(btn.dataset.carId);
//   if (!carId) return;

//   try {
//     const r = await fetch("/umbraco/api/favorites/toggle", {
//       method: "POST",
//       headers: { "Content-Type": "application/json" },
//       credentials: "same-origin",
//       body: JSON.stringify({ carId }),
//     });

//     if (!r.ok) throw new Error(await r.text());

//     const { isFavorite } = await r.json();

//     // ✅ UI update ΑΠΟΚΛΕΙΣΤΙΚΑ από server response
//     btn.classList.toggle("is-favorite", isFavorite);

//     const icon = btn.querySelector("i");
//     if (icon) {
//       icon.className = isFavorite ? "fa-solid fa-heart" : "fa-regular fa-heart";
//     }

//     // 🔔 ενημέρωση παντού
//     document.dispatchEvent(
//       new CustomEvent("favorites:changed", { detail: { carId, isFavorite } }),
//     );
//   } catch (err) {
//     console.error("Favorite toggle error:", err);
//   }
// });

// async function syncFavoriteHearts() {
//   try {
//     const r = await fetch("/umbraco/api/favorites/ids", {
//       credentials: "same-origin",
//     });
//     if (!r.ok) return;

//     const ids = await r.json(); // [12,45,88]

//     document.querySelectorAll(".favBtn").forEach((btn) => {
//       const id = Number(btn.dataset.carId);
//       const isFav = ids.includes(id);

//       btn.classList.toggle("is-favorite", isFav);

//       const icon = btn.querySelector("i");
//       if (icon) {
//         icon.className = isFav ? "fa-solid fa-heart" : "fa-regular fa-heart";
//       }
//     });
//   } catch (e) {
//     console.warn("syncFavoriteHearts failed", e);
//   }
// }

// document.addEventListener("DOMContentLoaded", syncFavoriteHearts);

// window.addEventListener("pageshow", () => {
//   syncFavoriteHearts();
// });
// document.addEventListener("favorites:changed", syncFavoriteHearts);
