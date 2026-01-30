let sidebar;

window.currentPriceOrder = null;
window.sortedCards = [];

function toggleFilters() {
  sidebar = document.getElementById("filterSidebar");
  sidebar.classList.toggle("is-open");
}

document.addEventListener("DOMContentLoaded", () => {
  const toggles = document.querySelectorAll(".filter-toggle");

  toggles.forEach((toggle) => {
    toggle.addEventListener("click", () => {
      const item = toggle.closest(".filter-item");
      item.classList.toggle("active");
    });
  });
});

function normalizeGreek(str) {
  return (str || "")
    .toLowerCase()
    .normalize("NFD") // σπάει τα τονισμένα
    .replace(/[\u0300-\u036f]/g, "") // αφαιρεί τόνους
    .replace(/\s+/g, "") // αφαιρεί κενά
    .replace(/ς/g, "σ") // 🟢 τελικό σίγμα -> σ
    .trim();
}

//----------------------------------------------//
//-------------------FILTERS--------------------//

let filters;
let displayCars;
let noResultsMsg;
let paginationContainer;
let originalCardElements = Array.from(document.querySelectorAll(".cardCar"));
let allCards;
let filteredCards = [];

const typeOfCarMap = {
  suv: "SUV",
  outofroad: "Εκτόσ Δρόμου", // επιτηδες ειναι με 'σ'!! Μην πειραχθεί!
  town: "Πόλης",
  sedan: "Sedan",
};

let makerName,
  modelName,
  modelPriceText,
  modelPrice,
  fuelName,
  transmissionName,
  colorName,
  offerText,
  typeOfCarName,
  yearText,
  carYear,
  klmText,
  carKlm,
  ccText,
  carCc,
  hpText,
  carhp;

function normalizeColorStrict(v) {
  return (v || "")
    .toLowerCase()
    .normalize("NFD") // αφαιρεί τόνους
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/ς/g, "σ") // τελικό σ -> σ
    .replace(/[\u2010-\u2015]/g, "-") // όλα τα είδη dash -> "-"
    .replace(/\s*-\s*/g, "-") // ενιαίες παύλες χωρίς κενά γύρω
    .replace(/\s+/g, "-") // ⛔️ ό,τι κενό -> παύλα (έτσι “Κόκκινο Μεταλιζέ” == “Κόκκινο-Μεταλιζέ”)
    .trim();
}

document.addEventListener("change", (e) => {
  if (e.target && e.target.id === "priceOrderSelect") {
    const val = e.target.value;

    window.currentPriceOrder = val;

    const filters = collectFilters();
    filterCards(filters);
  }
});

function filterCards(filters) {
  filteredCards = [];

  if (
    displayCars &&
    noResultsMsg &&
    noResultsMsg.parentElement !== displayCars
  ) {
    displayCars.prepend(noResultsMsg);
  }
  if (noResultsMsg) noResultsMsg.style.display = "none";

  allCards = [...originalCardElements]; // shallow copy.
  let anyMatch = false;

  // ✅ Κρύψε όλες τις κάρτες στην αρχή
  allCards.forEach((card) => {
    card.style.display = "none";
  });

  allCards.forEach((card) => {
    makerName =
      card
        .querySelector(".maker-title")
        ?.childNodes[0]?.nodeValue.trim()
        .toLowerCase() || "";

    modelName =
      card.querySelector(".card-title")?.innerText.trim().toLowerCase() || "";

    modelPriceText = card.querySelector(".card-text")?.innerText || "";
    modelPrice = parsePrice(modelPriceText);
    console.log(modelPrice);

    fuelName =
      card.querySelector(".fuel")?.innerText.trim().toLowerCase() || "";

    transmissionName =
      card.querySelector(".transmission")?.innerText.trim().toLowerCase() || "";

    colorName =
      card.querySelector(".typeOfColor")?.innerText.trim().toLowerCase() || "";

    offerText =
      card.querySelector(".discount-badge")?.innerText.trim().toLowerCase() ||
      "";

    typeOfCarName =
      card.querySelector(".typeOfCar")?.innerText.trim().toLowerCase() || "";

    yearText = card
      .querySelector(".car-year")
      ?.textContent.trim()
      .replace(/\D/g, "");
    carYear = parseInt(yearText);

    klmText = card.querySelector(".klm")?.textContent.trim().replace(/\D/g, "");
    carKlm = parseInt(
      klmText.replace(/\u00A0|\u202F/g, "").replace(/[^\d]/g, ""),
      10,
    );

    ccText = card.querySelector(".cc")?.textContent.trim().replace(/\D/g, "");
    carCc = parseInt(ccText);

    hpText = card.querySelector(".hp")?.textContent.trim().replace(/\D/g, "");
    carhp = parseInt(hpText);

    const matches = [
      filters.brands.length === 0 ||
        filters.brands.some((b) => makerName.includes(b.toLowerCase())),
      !isNaN(modelPrice) &&
        modelPrice >= filters.minPrice &&
        modelPrice <= filters.maxPrice,
      !isNaN(carYear) &&
        carYear >= filters.minYear &&
        carYear <= filters.maxYear,
      !isNaN(carKlm) && carKlm >= filters.minKm && carKlm <= filters.maxKm,
      !isNaN(carCc) && carCc >= filters.minCc && carCc <= filters.maxCc,
      !isNaN(carhp) && carhp >= filters.minhp && carhp <= filters.maxhp,

      filters.fuel.length === 0 ||
        filters.fuel.some(
          (f) =>
            (f || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim() ===
            (fuelName || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim(),
        ),

      filters.transmission.length === 0 ||
        filters.transmission.some(
          (t) =>
            (t || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim() ===
            (transmissionName || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim(),
        ),

      filters.color.length === 0 ||
        filters.color.some((c) => {
          const left = (c || "").trim().toLowerCase(); // φίλτρο όπως είναι
          const right = (colorName || "")
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/ς/g, "σ")
            .replace(/-/g, "")
            .replace(/\s+/g, "");

          const leftNorm = left
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/ς/g, "σ")
            .replace(/-/g, "")
            .replace(/\s+/g, "");

          // console.log("🎨 compare(color):", {
          //   filterColorOriginal: c,
          //   cardColorOriginal: colorName,
          //   leftNorm,
          //   right,
          //   eq: leftNorm === right,
          // });

          return leftNorm === right;
        }),

      filters.carType.length === 0 ||
        filters.carType.some((t) => {
          const left = normalizeGreek(t);
          const right = normalizeGreek(typeOfCarName);

          // console.log("Σύγκριση typeOfCar:", { t, left, typeOfCarName, right });

          return left === right;
        }),

      filters.offerTypes.length === 0 ||
        filters.offerTypes.some((o) => o.toLowerCase() === offerText),
    ];

    if (matches.every(Boolean)) {
      filteredCards.push(card);
      anyMatch = true;
    }
  });

  // Αφορά το μήνυμα NoResults!!
  if (noResultsMsg) noResultsMsg.style.display = "none";
  if (displayCars) displayCars.classList.remove("is-empty");

  // ...
  if (filteredCards.length === 0) {
    // καθάρισε/κρύψε κάρτες
    displayCars
      .querySelectorAll(".cardCar")
      .forEach((c) => c.remove?.() || (c.style.display = "none"));

    // Εμφάνισε μήνυμα + κλάση
    if (noResultsMsg) noResultsMsg.style.display = "block"; // ή ''
    if (displayCars) displayCars.classList.add("is-empty");

    if (paginationContainer) paginationContainer.style.display = "none";
    return;
  } else {
    if (noResultsMsg) noResultsMsg.style.display = "none";
    if (displayCars) displayCars.classList.remove("is-empty");
    if (paginationContainer) paginationContainer.style.display = "";
  }

  if (
    window.currentPriceOrder === "asc" ||
    window.currentPriceOrder === "desc"
  ) {
    filteredCards.sort((a, b) => {
      const priceA = parsePrice(a.querySelector(".card-text")?.innerText || "");
      const priceB = parsePrice(b.querySelector(".card-text")?.innerText || "");

      return window.currentPriceOrder === "asc"
        ? priceA - priceB
        : priceB - priceA;
    });

    window.sortedCards = [...filteredCards];

    const container = document.getElementById("displayCars");
    container.innerHTML = "";

    window.sortedCards.forEach((card) => {
      card.style.display = "block";
      container.appendChild(card);
    });

    currentPage = 1;
    paginateVisibleCars(window.sortedCards);

    return;
  }

  if (!window.currentPriceOrder) {
    [...displayCars.querySelectorAll(".cardCar")].forEach((card) =>
      card.remove(),
    );

    filteredCards.forEach((card) => {
      card.style.display = "flex"; // ή "block" ανάλογα με το layout
      displayCars.appendChild(card);
    });

    currentPage = 1;
    paginateVisibleCars(filteredCards);
  }

  // updateAvailableOffers(filters, filteredCards);
}

//-------------------------------------------------//
//------------------Update Filters-----------------//
//-------------------------------------------------//
// function updateAvailableOffers(filters, cards) {
//   const offerCheckboxes = document.querySelectorAll(".offerTypeCheckbox");
//   const visibleOffers = new Set();

//   const priceIsRestricted = filters.minPrice > 0 || filters.maxPrice < 999999;

//   if (!priceIsRestricted) {
//     offerCheckboxes.forEach((checkbox) => {
//       checkbox.disabled = false;
//       checkbox.parentElement.style.opacity = "1";
//     });
//     return;
//   }

//   cards.forEach((card) => {
//     const badge = card.querySelector(".discount-badge");
//     const offerText = badge?.innerText.trim().toLowerCase() || "";

//     if (offerText === "έκπτωση") visibleOffers.add("discount");
//     if (offerText === "προσφορά") visibleOffers.add("offer");
//   });

//   offerCheckboxes.forEach((checkbox) => {
//     const value = checkbox.value;
//     const label = checkbox.parentElement;

//     if (visibleOffers.size === 0 || visibleOffers.has(value)) {
//       checkbox.disabled = false;
//       label.style.opacity = "1";
//     } else {
//       checkbox.disabled = true;
//       checkbox.checked = false;
//       label.style.opacity = "0.5";
//     }
//   });
// }

function updateAvailableBrands(filters, filteredCards) {
  const brandCheckboxes = document.querySelectorAll(".brandCheckbox");

  // helper: ίδιο normalization με το server
  const norm = (s) => (s || "").replace(/[^0-9a-z]/gi, "").toUpperCase();

  // Προετοιμασία: keys όλων των brands και map σε label
  const brandCounts = {};
  const labelByKey = {};
  brandCheckboxes.forEach((cb) => {
    const key = norm(cb.value);
    brandCounts[key] = 0; // μηδενισμός
    const label = cb.closest("label");
    if (label) labelByKey[key] = label;
  });

  const hasPriceFilter = filters.minPrice > 0 || filters.maxPrice < Infinity;

  // Πηγή καρτών
  const sourceCards = hasPriceFilter ? filteredCards : originalCardElements;

  // Re-count ανά κάρτα (χωρίς nested loops)
  sourceCards.forEach((card) => {
    const titleEl = card.querySelector(".maker-title");
    if (!titleEl) return;

    // maker = πρώτο text node πριν το <span class="card-title">
    const makerRaw =
      (titleEl.childNodes[0] && titleEl.childNodes[0].nodeValue) ||
      titleEl.textContent ||
      "";
    const makerKey = norm(makerRaw.trim());

    if (makerKey in brandCounts) {
      brandCounts[makerKey] += 1;
    }
  });

  // visible set από τα counts
  const visibleBrands = new Set(
    Object.keys(brandCounts).filter((k) => brandCounts[k] > 0),
  );

  // Ενημέρωση UI (μόνο αριθμοί, όχι ονόματα)
  brandCheckboxes.forEach((checkbox) => {
    const key = norm(checkbox.value);
    const label = labelByKey[key];
    const count = brandCounts[key] || 0;

    if (label) {
      const countEl = label.querySelector(".brand-count");
      if (countEl) countEl.textContent = String(count);

      if (hasPriceFilter) {
        if (visibleBrands.has(key)) {
          checkbox.disabled = false;
          label.style.opacity = "1";
        } else {
          checkbox.disabled = true;
          checkbox.checked = false;
          label.style.opacity = "0.5";
        }
      } else {
        checkbox.disabled = false;
        label.style.opacity = "1";
      }
    }
  });
}

function InitializeCounters(originalCardElements) {
  const brandCheckboxes = document.querySelectorAll(".brandCheckbox");

  const brandCounts = {};

  originalCardElements.forEach((card) => {
    const modelText =
      card.querySelector(".maker-title")?.innerText.toUpperCase() || "";

    brandCheckboxes.forEach((cb) => {
      const brand = cb.value.toUpperCase();
      if (modelText.includes(brand)) {
        brandCounts[brand] = (brandCounts[brand] || 0) + 1;
      }
    });
  });

  brandCheckboxes.forEach((checkbox) => {
    const label = checkbox.closest("label");
    const brand = checkbox.value.toUpperCase();
    const count = brandCounts[brand] || 0;

    const span = label.querySelector("span");
    if (span) {
      span.innerText = `${brand} (${count})`;
    }

    // Επανενεργοποιούμε τα πάντα
    checkbox.disabled = false;
    label.style.opacity = "1";
  });
}

function pickVisible(...ids) {
  // επέστρεψε το πρώτο ορατό element από τη λίστα IDs
  for (const id of ids) {
    const el = document.getElementById(id);
    if (el && el.offsetParent !== null) return el; // ορατό
  }
  // αλλιώς επέστρεψε όποιο υπάρχει (π.χ. σε SSR/hidden)
  for (const id of ids) {
    const el = document.getElementById(id);
    if (el) return el;
  }
  return null;
}

function readNumVisible(fallback, ...ids) {
  const el = pickVisible(...ids);
  if (!el) return fallback;

  const raw = String(el.value || "").trim();
  if (!raw) return fallback;

  // καθάρισμα: κρατάμε δεκαδικά, βγάζουμε χιλιάδες
  const cleaned = raw
    .replace(/\u00A0|\u202F/g, "") // σπάνια invisible spaces
    .replace(/[^\d]/g, "")
    .replace(/,/g, ".") // κόμμα → τελεία
    .replace(/[^\d.]/g, ""); // αφαιρεί οτιδήποτε άλλο

  const num = parseFloat(cleaned);
  return isNaN(num) ? fallback : num;
}

function collectFilters() {
  let minPrice = readNumVisible(0, "minPriceInputDesk", "minPriceInputMobile");
  let maxPrice = readNumVisible(
    Infinity,
    "maxPriceInputDesk",
    "maxPriceInputMobile",
  );

  // 🔥 MONO αυτό προσθέτουμε (και μόνο για maxPrice)
  // if (maxPrice !== Infinity && maxPrice !== null) {
  //   maxPrice = maxPrice + 1;
  // }

  return {
    // Τιμή
    minPrice,
    maxPrice,
    //
    // Προσφορά
    offerTypes: getCheckedValues(".offerTypeCheckbox"),
    //
    // Αύξουσα-Φθίνουσα τιμή
    //priceOrder: document.querySelector(".price-order-wrapper #priceOrderSelect")?.value,
    //
    // Έτος
    minYear: readNumVisible(0, "minYearInputDesk", "minYearInputMobile"),
    maxYear:
      readNumVisible(Infinity, "maxYearInputDesk", "maxYearInputMobile") ||
      new Date().getFullYear(),
    //
    // Χιλιόμετρα
    minKm: readNumVisible(0, "minKlmInputDesk", "minKlmInputMobile"),
    maxKm: readNumVisible(Infinity, "maxKlmInputDesk", "maxKlmInputMobile"),
    //
    // Κυβικά
    minCc: readNumVisible(0, "minCcInputDesk", "minCcInputMobile"),
    maxCc: readNumVisible(Infinity, "maxCcInputDesk", "maxCcInputMobile"),
    //
    // Ίπποι
    minhp: readNumVisible(0, "minHpInputDesk", "minHpInputMobile"),
    maxhp: readNumVisible(Infinity, "maxHpInputDesk", "maxHpInputMobile"),
    //
    // Κατασκευαστής
    brands: getCheckedValues(".brandCheckbox"),
    //
    // Καύσιμο
    fuel: getCheckedValues(".fuelCheckbox"),
    //
    // Κιβώτιο Ταχυτήτων
    transmission: getCheckedValues(".transmissionCheckbox"),
    //
    // Χρώμα
    color: getCheckedValues(".colorCheckbox"),
    //
    // Είδος Οχήματος
    carType: getCheckedValues(".carTypeCheckbox"),
  };
}

// αφορά την ταξινόμηση
function parsePrice(value) {
  if (!value) return null;

  return parseInt(
    value.replace(/[^\d]/g, ""), // κρατά ΜΟΝΟ ψηφία
    10,
  );
}

function getCheckedValues(selector) {
  return Array.from(document.querySelectorAll(`${selector}:checked`)).map(
    (cb) => cb.value,
  );
}

document.addEventListener("DOMContentLoaded", () => {
  displayCars = document.getElementById("displayCars");
  noResultsMsg = document.querySelector("#noResultsBox");
  paginationContainer = document.getElementById("paginationControls");

  const filterInputs = document.querySelectorAll(
    "input[type='checkbox'], input[type='number'], input[type='text'], select",
  );

  function getSelectedFilters() {
    const filters = collectFilters();
    filterCards(filters);
  }

  // ✅ Listeners σε όλα τα φίλτρα
  filterInputs.forEach((input) => {
    input.addEventListener("change", getSelectedFilters);
    input.addEventListener("input", getSelectedFilters);
  });

  //Fetch auth status ΓΙΑ ΝΑ ΜΑΣ ΠΗΓΑΙΝΕΙ ΕΙΤΕ ΣΤΗ carDetailsVisitor είτε στη carDetailsMember
  fetch("/umbraco/api/auth/status")
    .then((r) => r.json())
    .then((data) => {
      document.querySelectorAll(".cardCarLink").forEach((link) => {
        const currentHref = link.getAttribute("href"); // π.χ. /carDetailsVisitor?id=37

        const idPart = currentHref.split("?")[1] || ""; // παίρνουμε id=37

        const base = data.loggedIn ? "/carDetailsMember" : "/carDetailsVisitor";

        link.setAttribute("href", `${base}?${idPart}`);
      });
    })
    .catch((err) => console.error("Auth check error:", err));

  setTimeout(() => {
    originalCardElements = Array.from(document.querySelectorAll(".cardCar"));
    allCards = [...originalCardElements];

    resetDisplayCarsLayout();
    paginateVisibleCars(allCards);

    const urlParams = new URLSearchParams(window.location.search);
    let carTypeParam = urlParams.get("carType");

    if (carTypeParam) {
      const typeOfCarMap = {
        suv: "SUV",
        outofroad: "Εκτόσ Δρόμου", // επιτηδες ειναι με 'σ'!! Μην πειραχθεί!
        town: "Πόλης",
        sedan: "Sedan",
      };

      const mappedValue = typeOfCarMap[carTypeParam.toLowerCase()];

      let matchedCheckbox = null;

      document.querySelectorAll(".carTypeCheckbox").forEach((cb) => {
        const left = normalizeGreek(cb.value);
        const right = normalizeGreek(mappedValue);

        if (left === right) {
          matchedCheckbox = cb;
        }
      });

      if (matchedCheckbox) {
        matchedCheckbox.checked = true;
        matchedCheckbox.dispatchEvent(new Event("change", { bubbles: true })); // 🔔 τρέχει τους listeners σου
        getSelectedFilters();

        // Άνοιξε accordion
        const filterItem = matchedCheckbox.closest(".filter-item");
        const toggleButton = filterItem?.querySelector(".filter-toggle");
        if (toggleButton) toggleButton.click();

        // Άνοιξε sidebar
        document.getElementById("toggleSidebarBtn")?.click();
      } else {
        console.warn("⚠️ Δεν βρέθηκε checkbox για:", carTypeParam);
      }
    }
  }, 300);
});

//----------------Clear Filters-------------------//
function cleanupBackdrops() {
  // καθάρισε τυχόν Bootstrap backdrops
  document
    .querySelectorAll(".offcanvas-backdrop, .modal-backdrop")
    .forEach((n) => n.remove());
  // ξεκλείδωσε scroll αν έχει μείνει κλειδωμένο
  document.documentElement.style.overflow = "";
  document.body.style.overflow = "";
  document.body.classList.remove("modal-open");
  // αν (τυχαίνει) υπάρχει δικό σου overlay, κρύψ’ το
  const nxOv = document.getElementById("nxOverlay");
  if (nxOv) nxOv.hidden = true;
}

function closeAllFilterSections(root) {
  // Custom accordion
  root.querySelectorAll(".filter-item").forEach((item) => {
    item.classList.remove("active");

    const toggle = item.querySelector(".filter-toggle");
    if (toggle) toggle.setAttribute("aria-expanded", "false");

    const content = item.querySelector(".filter-content");
    if (content) {
      // Αφαίρεσε inline heights/dispays
      content.style.maxHeight = "";
      content.style.height = "";
      content.style.display = "";
      content.classList.remove("show");
      content.setAttribute("aria-hidden", "true");

      // Αν τυχόν είναι Bootstrap collapse, κλείστο σωστά
      try {
        if (
          content.classList.contains("collapse") ||
          content.classList.contains("show")
        ) {
          const inst = bootstrap.Collapse.getOrCreateInstance(content, {
            toggle: false,
          });
          inst.hide();
        }
      } catch (_) {}
    }
  });

  // Για την περίπτωση που τα sections είναι καθαρά .collapse χωρίς .filter-item
  root.querySelectorAll(".collapse.show").forEach((el) => {
    try {
      bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
    } catch (_) {}
  });
}

function clearAllFilters() {
  const mobilePanel = document.getElementById("filtersSidebar");
  const isMobileOpen = !!(
    mobilePanel && mobilePanel.classList.contains("show")
  );

  // Βρίσκει σε ποιο τμήμα της DOM θα γίνει το clear των φίλτρων.
  // Αν είμαστε σε mobile, παίρνει το sidebar του offcanvas.
  // Αν είμαστε σε desktop, παίρνει το aside.sidebar.
  const ROOT = (() => {
    if (isMobileOpen) {
      return (
        mobilePanel.querySelector("aside.sidebar") ||
        mobilePanel.querySelector(".offcanvas-body") ||
        mobilePanel
      );
    }
    const desk = document.querySelector("aside.sidebar");
    return desk && (!mobilePanel || !mobilePanel.contains(desk))
      ? desk
      : document;
  })();

  const runClear = () => {
    // Καθαρίζει όλα τα input πεδία: checkboxes, radios, selects, κ.λπ.
    ROOT.querySelectorAll("input, select, textarea").forEach((el) => {
      if (el.type === "checkbox" || el.type === "radio") el.checked = false;
      else el.value = "";
      el.disabled = false;
      if (el.parentElement) el.parentElement.style.opacity = "1";
    });

    // Κλείνει τα collapsible φίλτρα
    ROOT.querySelectorAll(".filter-item").forEach((item) => {
      item.classList.remove("active");
      const toggle = item.querySelector(".filter-toggle");
      if (toggle) toggle.setAttribute("aria-expanded", "false");
      //Επαναφέρει τα filter-content στην αρχική (κλειστή) κατάσταση.
      const content = item.querySelector(".filter-content");
      if (content) {
        content.style.maxHeight = "";
        content.style.height = "";
        content.style.display = "";
        content.classList.remove("show");
        content.setAttribute("aria-hidden", "true");
        try {
          if (
            content.classList.contains("collapse") ||
            content.classList.contains("show")
          ) {
            bootstrap.Collapse.getOrCreateInstance(content, {
              toggle: false,
            }).hide();
          }
        } catch (_) {}
      }
    });
    ROOT.querySelectorAll(".collapse.show").forEach((el) => {
      try {
        bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
      } catch (_) {}
    });

    // 3) Reset του filters object
    filters = {
      minPrice: null,
      maxPrice: null,
      AscDescPrice: [],
      priceOrder: null,
      brands: [],
      minYear: null,
      maxYear: null,
      minKm: null,
      maxKm: null,
      fuel: [],
      minCc: null,
      maxCc: null,
      minHp: null,
      maxHp: null,
      transmission: [],
      color: [],
      carType: [],
      offerTypes: [],
    };

    // 4) UI state
    if (noResultsMsg) noResultsMsg.style.display = "none";

    // 5) Επαναφορά καρτών & layout
    if (displayCars && originalCardElements) {
      // 1) Καθάρισε το container
      displayCars.innerHTML = "";

      // 2) Βάλε ΠΙΣΩ τις αρχικές κάρτες
      originalCardElements.forEach((card) => {
        card.style.display = ""; // αφήνουμε το CSS να ορίσει layout
        displayCars.appendChild(card);
      });

      // 3) Reset layout του displayCars
      displayCars.style.display = "";
      displayCars.style.justifyContent = "";
      displayCars.style.alignItems = "";
      displayCars.style.marginTop = "";
      displayCars.classList.remove("is-empty");

      // 4) RESET του pagination (wrapper + controls)
      const paginationWrapper = document.querySelector(".pagination-wrapper");
      const paginationControls = document.getElementById("paginationControls");

      if (paginationWrapper) paginationWrapper.style.display = ""; // π.χ. block
      if (paginationControls) paginationControls.style.display = ""; // αφήνουμε τις κλάσεις "pagination justify-content-center flex-wrap" να δουλέψουν

      // 5) Ξαναχτίσε το pagination με ΟΛΕΣ τις κάρτες
      const cardsArray = Array.from(originalCardElements);
      currentPage = 1;
      paginateVisibleCars(cardsArray);

      // 6) Counters
      updateAvailableBrands?.(filters, cardsArray);
      // updateAvailableOffers?.(filters, cardsArray);
    }

    // 6) Recompute counters & τρέξε κενό filter για συγχρονισμό UI
    // InitializeCounters?.(Array.from(originalCardElements));
    // filterCards?.(collectFilters?.() ?? {});

    // 7) Καθάρισε τυχόν leftover backdrops/scroll locks
    setTimeout(() => {
      try {
        document
          .querySelectorAll(".offcanvas-backdrop")
          .forEach((b) => b.remove());
        document.body.classList.remove("modal-open");
        document.body.style.overflow = "";
      } catch (_) {}
      cleanupBackdrops?.();
    }, 10);
  };

  // Αν είμαστε σε mobile, κλείσε πρώτα το offcanvas και μετά τρέξε το clear
  if (isMobileOpen) {
    try {
      const inst = bootstrap.Offcanvas.getOrCreateInstance(mobilePanel);
      mobilePanel.addEventListener("hidden.bs.offcanvas", runClear, {
        once: true,
      });
      inst.hide();
    } catch (_) {
      runClear();
    }
  } else {
    runClear();
  }
}

// ===== Infinite Scroll ΜΟΝΟ για κινητό (≤ 575px) =====
const MOBILE_QUERY = "(max-width: 575px)";
function isMobile() {
  return window.matchMedia(MOBILE_QUERY).matches;
}

let inf = {
  observer: null,
  batchSize: 12, // πόσες κάρτες να φορτώνει κάθε φορά
  offset: 0,
  source: [],
};

function destroyInfinite() {
  if (inf.observer) {
    inf.observer.disconnect();
    inf.observer = null;
  }
  const s = document.getElementById("infiniteSentinel");
  if (s) s.remove();
}

function appendNextBatch() {
  const sentinel = document.getElementById("infiniteSentinel");
  const end = Math.min(inf.offset + inf.batchSize, inf.source.length);
  for (let i = inf.offset; i < end; i++) {
    const card = inf.source[i];
    if (!card) continue;
    card.style.display = "block"; // 👉 αν οι κάρτες σου είναι flex, άλλαξέ το σε "flex"
    displayCars.insertBefore(card, sentinel || null);
  }
  inf.offset = end;
  if (inf.offset >= inf.source.length) {
    destroyInfinite(); // όλα φορτώθηκαν
  }
}

function initInfinite(sourceList) {
  destroyInfinite();

  inf.source = sourceList.slice();
  inf.offset = 0;

  // καθάρισε container & κρύψε pagination controls
  displayCars.innerHTML = "";
  const pc = document.getElementById("paginationControls");
  if (pc) {
    pc.innerHTML = "";
    pc.style.display = "none";
  }

  // sentinel
  const sentinel = document.createElement("div");
  sentinel.id = "infiniteSentinel";
  sentinel.style.cssText = "height:1px;width:100%;";
  displayCars.appendChild(sentinel);

  // πρώτο batch
  appendNextBatch();

  // observer για επόμενα batches
  inf.observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) appendNextBatch();
      });
    },
    { rootMargin: "200px" },
  );
  inf.observer.observe(sentinel);
}

let currentPage = 1;

function getCarsPerPage() {
  if (window.innerWidth < 1080) {
    return 4; // tablet
  } else if (window.innerWidth < 1200) {
    return 4; // μικρό desktop
  } else if (window.innerWidth < 1400) {
    return 4;
  } else if (window.innerWidth < 1600) {
    return 4;
  } else {
    return 9;
  }
}

function paginateVisibleCars(carList) {
  // --- Mobile: infinite scroll ---
  if (isMobile()) {
    initInfinite(carList);
    return;
  }

  const carsPerPage = getCarsPerPage();
  const totalPages = Math.ceil(carList.length / carsPerPage);
  const paginationContainer = document.getElementById("paginationControls");

  if (!paginationContainer) return;

  // Κρύψε όλες τις κάρτες
  carList.forEach((card) => (card.style.display = "none"));

  // Υπολογισμός ορατών καρτών για τη σελίδα
  const start = (currentPage - 1) * carsPerPage;
  const end = start + carsPerPage;
  carList.slice(start, end).forEach((card) => {
    card.style.display = "block";
  });

  // Καθαρισμός προηγούμενων κουμπιών
  paginationContainer.innerHTML = "";

  if (totalPages <= 1) return;

  // Προηγούμενο
  if (currentPage > 1) {
    const prev = document.createElement("button");
    prev.innerText = "«";
    prev.onclick = () => {
      currentPage--;
      paginateVisibleCars(carList);
    };
    paginationContainer.appendChild(prev);
  }

  // ----------- ΕΜΦΑΝΙΣΗ ΜΟΝΟ 2 ΣΕΛΙΔΩΝ -----------
  let startPage = Math.max(1, currentPage - 1);
  let endPage = Math.min(totalPages, startPage + 1);

  // αν είμαστε στην τελευταία σελίδα, μετακινείται το "παράθυρο"
  if (endPage - startPage < 1 && startPage > 1) {
    startPage = endPage - 1;
  }

  for (let i = startPage; i <= endPage; i++) {
    const pageBtn = document.createElement("button");
    pageBtn.innerText = i;
    if (i === currentPage) pageBtn.classList.add("active");
    pageBtn.onclick = () => {
      currentPage = i;
      paginateVisibleCars(carList);
    };
    paginationContainer.appendChild(pageBtn);
  }

  // Επόμενο
  if (currentPage < totalPages) {
    const next = document.createElement("button");
    next.innerText = "»";
    next.onclick = () => {
      currentPage++;
      paginateVisibleCars(carList);
    };
    paginationContainer.appendChild(next);
  }
}

// 🔹 Επαναυπολογισμός σε resize
window.addEventListener("resize", () => {
  paginateVisibleCars(filteredCards.length ? filteredCards : allCards);
});

function resetDisplayCarsLayout() {
  displayCars.style.justifyContent = "flex-start";
  displayCars.style.alignItems = "flex-start";
  displayCars.style.marginTop = "0";

  const navbar = document.querySelector(".navbar.fixed-top");
  const offset = navbar?.offsetHeight || 60;

  const topPos =
    displayCars.getBoundingClientRect().top + window.scrollY - offset;

  window.scrollTo({
    top: topPos,
    behavior: "smooth",
  });

  // 🟢 Εξαναγκασμός scroll
  document.body.style.setProperty("overflow-y", "auto", "important");
  document.documentElement.style.setProperty("overflow-y", "auto", "important");
}

function setCookie(name, value, minutes) {
  const d = new Date();
  d.setTime(d.getTime() + minutes * 60 * 1000);
  document.cookie = `${name}=${encodeURIComponent(
    value,
  )}; Expires=${d.toUTCString()}; Path=/; SameSite=Lax; Secure`;
}

// ---------------Favorites button--------------------

function getFavorites() {
  try {
    return JSON.parse(localStorage.getItem("favoriteCars")) || [];
  } catch {
    return [];
  }
}

function saveFavorites(arr) {
  localStorage.setItem("favoriteCars", JSON.stringify(arr));
}

document.addEventListener("click", async (e) => {
  const btn = e.target.closest(".favorite-btn");
  if (!btn) return;

  e.preventDefault();
  e.stopPropagation();

  const carId = btn.dataset.carId;
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

    // 🔁 UI update ΜΟΝΟ από server response
    btn.classList.toggle("is-favorite", isFavorite);
    btn.querySelector("i").className = isFavorite
      ? "fa-solid fa-heart"
      : "fa-regular fa-heart";

    // 🔥 ΕΚΠΟΜΠΗ EVENT
    document.dispatchEvent(new CustomEvent("favorites:changed"));
  } catch (err) {
    console.error("Favorite toggle error:", err);
  }
});

async function syncFavoriteHearts() {
  try {
    const r = await fetch("/umbraco/api/favorites/ids", {
      credentials: "same-origin",
    });
    if (!r.ok) return;

    const ids = await r.json(); // [12,45,88]

    document.querySelectorAll(".favorite-btn").forEach((btn) => {
      const id = Number(btn.dataset.carId);
      const isFav = ids.includes(id);

      btn.classList.toggle("is-favorite", isFav);
      btn.querySelector("i").className = isFav
        ? "fa-solid fa-heart"
        : "fa-regular fa-heart";
    });
  } catch (e) {
    console.warn("syncFavoriteHearts failed", e);
  }
}

document.addEventListener("DOMContentLoaded", syncFavoriteHearts);

// 👂 ΑΚΟΥΕΙ ΟΛΟ ΤΟ SITE
document.addEventListener("favorites:changed", syncFavoriteHearts);
