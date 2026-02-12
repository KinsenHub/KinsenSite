function updateHeart(btn, isFavorite) {
  const icon = btn.querySelector("i");
  if (!icon) return;

  if (isFavorite) {
    icon.classList.remove("fa-regular");
    icon.classList.add("fa-solid");
    icon.style.color = "#023859"; // μπλε
  } else {
    icon.classList.remove("fa-solid");
    icon.classList.add("fa-regular");
    icon.style.color = "#696c6d"; // γκρι
  }
}

// ==========================
// TOGGLE FAVORITE
// ==========================
document.addEventListener("click", async (e) => {
  const btn = e.target.closest(".favBtn");
  if (!btn) return;

  e.preventDefault();
  e.stopPropagation();

  const carId = Number(btn.dataset.carId);
  if (!carId) return;

  try {
    const r = await fetch("/umbraco/api/FavoritesCustomer/Toggle", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ carId }),
    });

    if (!r.ok) throw new Error(await r.text());

    // 🔔 ΕΝΑ event – χωρίς payload
    document.dispatchEvent(new CustomEvent("favorites:changed"));
  } catch (err) {
    console.error("Favorite toggle error:", err);
  }
});

document.addEventListener("favorites:changed", syncFavoriteHearts);

// ==========================
// INITIAL SYNC
// ==========================
async function syncFavoriteHearts() {
  try {
    const r = await fetch("/umbraco/api/FavoritesCustomer/GetIds", {
      credentials: "include",
    });
    if (!r.ok) return;

    const cars = await r.json();

    document.querySelectorAll(".favBtn").forEach((btn) => {
      const id = Number(btn.dataset.carId);
      const isFavorite = cars.some((c) => c.id === id);
      updateHeart(btn, isFavorite);
      if (!isFavorite) {
        const card = btn.closest(".favorite-card");
        if (card) card.remove();

        // ✅ αν δεν έμεινε καμία κάρτα -> δείξε empty state
        if (
          !document.querySelector(".favorite-card") &&
          typeof showEmptyState === "function"
        ) {
          showEmptyState();
        }
      }
    });
  } catch (e) {
    console.warn("syncFavoriteHearts failed", e);
  }
}

document.addEventListener("favorites:changed", syncFavoriteHearts);

document.addEventListener("DOMContentLoaded", syncFavoriteHearts);

window.addEventListener("pageshow", syncFavoriteHearts);
