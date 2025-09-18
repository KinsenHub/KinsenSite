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

  const PriceText = document.querySelector(".price-value")?.innerText || "";
  let price = parseFloat(
    PriceText.replace(/\./g, "")
      .replace(",", ".")
      .replace(/[^\d.]/g, "")
  );
  const vatMultiplier = 1.24;

  // Βρες carId της σελίδας (π.χ. data-car-id στο body ή σε wrapper)
  const carId = Number(
    document.querySelector("[data-car-id]")?.dataset.carId || 0
  );

  items.forEach((item) => {
    item.addEventListener("click", function () {
      const selectedText = this.innerText;
      const selectedValue = this.value; // "efapaks" ή "6" | "12" | ...

      dropdownButton.textContent = selectedText;

      let perMonth = null;

      if (selectedValue === "efapaks") {
        resultSpan.textContent = "-";
      } else {
        const months = parseInt(selectedValue);
        if (!isNaN(months) && price > 0) {
          const totalWithVAT = price * vatMultiplier;
          perMonth = totalWithVAT / months;
          resultSpan.innerHTML = `<strong>${perMonth.toFixed(
            2
          )} €</strong> / μήνα (με ΦΠΑ)`;
        } else {
          resultSpan.textContent = "-";
        }
      }

      // === ΑΠΟΘΗΚΕΥΣΗ ανά car ===
      if (carId > 0) {
        window.installmentsByCar[carId] = {
          paymentPlan: selectedValue, // "efapaks" ή "6"/"12"/...
          perMonth: perMonth != null ? Number(perMonth.toFixed(2)) : null,
        };
        sessionStorage.setItem(
          "installmentsByCar",
          JSON.stringify(window.installmentsByCar)
        );
      }
    });
  });
}, 500);

// 1) Cart API client (μιλάει με /umbraco/api/cart/*)
const CartAPI = {
  async add(item) {
    const r = await fetch("/umbraco/api/cart/add", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
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
    console.log("[Cart][badge]", count);
  } catch (e) {
    console.warn("[Cart][badge] error:", e);
  }
}
// κάν’ την διαθέσιμη και global για layout/άλλα scripts
window.updateCartBadgeFromServer =
  window.updateCartBadgeFromServer || updateCartBadgeFromServer;

// 3) normalizeCar (maker/model & priceText/priceValue)
function normalizeCar(c) {
  const id = String(c?.id ?? c?.Id ?? c?.carId ?? c?.carID ?? "").trim();

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

  const img =
    c?.image ?? c?.imageUrl ?? c?.mainImage ?? c?.cover ?? c?.photo ?? "";
  const url = location.pathname + location.search;
  const year = c?.year ?? c?.Year ?? null;
  const km = c?.km ?? c?.Km ?? c?.mileage ?? null;
  const fuel = c?.fuel ?? c?.Fuel ?? c?.fuelType ?? "";

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
  };
}

// 4) API του αυτοκινήτου
async function fetchCarById(id) {
  const r = await fetch("/umbraco/api/carapimember/getcarbyid", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ id: Number(id) }),
  });
  if (!r.ok) throw new Error("API " + r.status);
  const raw = await r.json();
  return normalizeCar(raw);
}

document.addEventListener("DOMContentLoaded", async () => {
  console.log("✅ carDetails loaded");

  // Βρες ID (session/query/data-attr)
  const btnProbe = document.querySelector(
    "#addToCartBtn button, .addToCartBtn"
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
    console.log("✅ Loaded from API:", window.CURRENT_CAR);
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
          (btn.getAttribute("data-car-id") || "").trim()
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
        id, // υποχρεωτικό
        maker: c.maker ?? "",
        model: c.model ?? "",
        title: c.title ?? "",
        priceText: c.priceText ?? null, // π.χ. "15.000"
        priceValue: toIntOrNullStrict(c.priceValue ?? c.priceText), // π.χ. 15000
        img: c.img ?? "",
        url: c.url ?? location.pathname + location.search,
        year: toIntOrNullStrict(c.year),
        km: toIntOrNullStrict(c.km), // ΣΗΜΑΝΤΙΚΟ
        fuel: c.fuel ?? "",
        paymentPlan: paymentPlan, // "efapaks" ή "6" | "12" | ...
        perMonth: perMonthNum,
      };

      console.log(
        "👉 POST /umbraco/api/cart/add payload:",
        payload,
        JSON.stringify(payload)
      );

      try {
        const before = getCartBadgeCount();
        setCartBadgeCount(before + 1);
        window.dispatchEvent(
          new CustomEvent("cart:updated", { detail: { count: before + 1 } })
        );

        // Κλήση στο API
        const res = await CartAPI.add(payload); // { count, items }
        console.log("📦 server cart:", res);

        // --- ΣΥΜΦΙΛΙΩΣΗ ΜΕ ΤΟΝ SERVER ---
        setCartBadgeCount(res.count);
        window.dispatchEvent(
          new CustomEvent("cart:updated", { detail: { count: res.count } })
        );

        // feedback στο κουμπί
        const prev = btn.textContent;
        btn.disabled = true;
        btn.textContent = "Στο καλάθι ✓";
        setTimeout(() => {
          btn.disabled = false;
          btn.textContent = prev;
        }, 1200);
      } catch (err) {
        // Αν αποτύχει το API, επανέφερε το badge
        const before = getCartBadgeCount();
        setCartBadgeCount(Math.max(0, before - 1));
        window.dispatchEvent(
          new CustomEvent("cart:updated", {
            detail: { count: getCartBadgeCount() },
          })
        );
        console.error("❌ add error:", err);
      }
    },
    true
  );
});
