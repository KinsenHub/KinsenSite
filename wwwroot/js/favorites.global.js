const FAVORITES_KEY = "favoriteCars";

// ---------- helpers ----------
function getLocalFavorites() {
  try {
    return JSON.parse(localStorage.getItem(FAVORITES_KEY)) || [];
  } catch {
    return [];
  }
}

function setLocalFavorites(ids) {
  localStorage.setItem(FAVORITES_KEY, JSON.stringify(ids));
}

function updateHeart(btn, isFavorite) {
  btn.classList.toggle("is-favorite", isFavorite);

  const icon = btn.querySelector("i");
  if (icon) {
    icon.className = isFavorite ? "fa-solid fa-heart" : "fa-regular fa-heart";
  }
}

// ---------- GLOBAL CLICK (παντού) ----------
document.addEventListener("click", async (e) => {
  const btn = e.target.closest(".favBtn");
  if (!btn) return;

  e.preventDefault();
  e.stopPropagation();

  const carId = Number(btn.dataset.carId);
  if (!carId) return;

  try {
    const r = await fetch("/umbraco/api/favorites/toggle", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ carId }),
    });

    if (!r.ok) throw new Error(await r.text());

    const { isFavorite } = await r.json();

    // 🔔 ενημέρωση ΠΑΝΤΟΥ
    document.dispatchEvent(
      new CustomEvent("favorites:changed", { detail: { carId, isFavorite } }),
    );
  } catch (err) {
    console.error("Favorite toggle error:", err);
  }
});

// ---------- GLOBAL UPDATE (παντού) ----------
document.addEventListener("favorites:changed", (e) => {
  const { carId, isFavorite } = e.detail || {};

  // ❤️ ενημέρωση ΟΛΩΝ των καρδιών σε όλη τη σελίδα
  document
    .querySelectorAll(`.favBtn[data-car-id="${carId}"]`)
    .forEach((btn) => updateHeart(btn, isFavorite));

  // 🧹 favorites page μόνο: αν αφαιρέθηκε, βγάλε την κάρτα
  if (!isFavorite) {
    const card = document.querySelector(`.favorite-card[data-id="${carId}"]`);
    if (card) {
      card.remove();

      if (!document.querySelector(".favorite-card")) {
        // υπάρχει μόνο στη favorites σελίδα
        if (typeof showEmptyState === "function") showEmptyState();
      }
    }
  }
});

// ---------- INITIAL SYNC (όταν φορτώνει/επιστρέφει η σελίδα) ----------
async function syncFavoriteHearts() {
  try {
    const r = await fetch("/umbraco/api/favorites/ids", {
      credentials: "same-origin",
    });
    if (!r.ok) return;

    const ids = await r.json(); // [12,45,88]

    document.querySelectorAll(".favBtn").forEach((btn) => {
      const id = Number(btn.dataset.carId);
      updateHeart(btn, ids.includes(id));
    });
  } catch (e) {
    console.warn("syncFavoriteHearts failed", e);
  }
}

document.addEventListener("DOMContentLoaded", syncFavoriteHearts);
window.addEventListener("pageshow", syncFavoriteHearts);
