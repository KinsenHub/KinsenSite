let sidebar;

// function toggleFilters() {
//   sidebar = document.getElementById("filterSidebar");
//   sidebar.classList.toggle("show");
// }

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
let originalCardElements = Array.from(
  document.querySelectorAll(".cardCarLink")
);
let allCards;
let filteredCards = [];

// const offerMap = {
//   offer: "προσφορά",
//   discount: "έκπτωση",
// };

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
    modelPrice = parseFloat(
      modelPriceText
        .replace(/\./g, "")
        .replace(",", ".")
        .replace(/[^\d.]/g, "")
    );

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
      klmText
        .replace(/\u00A0|\u202F/g, "") // non-breaking spaces
        .replace(/[^\d]/g, ""), // κράτα μόνο ψηφία
      10
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
              .trim()
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
              .trim()
        ),

      filters.color.length === 0 ||
        filters.color.some(
          (c) =>
            (c || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim() ===
            (colorName || "")
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .trim()
        ),

      filters.carType.length === 0 ||
        filters.carType.some((t) => {
          const left = normalizeGreek(t);
          const right = normalizeGreek(typeOfCarName);

          console.log("Σύγκριση typeOfCar:", { t, left, typeOfCarName, right });

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

  if (filters.priceOrder === "asc" || filters.priceOrder === "desc") {
    filteredCards.sort((a, b) => {
      const priceA = parseFloat(
        a
          .querySelector(".card-text")
          ?.innerText.replace(/\./g, "")
          .replace(",", ".")
          .replace(/[^\d.]/g, "")
      );
      const priceB = parseFloat(
        b
          .querySelector(".card-text")
          ?.innerText.replace(/\./g, "")
          .replace(",", ".")
          .replace(/[^\d.]/g, "")
      );
      if (isNaN(priceA) || isNaN(priceB)) return 0;

      return filters.priceOrder === "asc" ? priceA - priceB : priceB - priceA;
    });

    const container = document.getElementById("displayCars");
    // container.innerHTML = "";

    // ✅ Καθάρισε παλιές κάρτες
    container.querySelectorAll(".cardCar").forEach((card) => card.remove());

    filteredCards.forEach((card) => container.appendChild(card));
    currentPage = 1;
    paginateVisibleCars(filteredCards);
  }

  // displayCars.innerHTML = "";

  if (!filters.priceOrder) {
    [...displayCars.querySelectorAll(".cardCar")].forEach((card) =>
      card.remove()
    );

    filteredCards.forEach((card) => {
      card.style.display = "flex"; // ή "block" ανάλογα με το layout
      displayCars.appendChild(card);
    });

    currentPage = 1;
    paginateVisibleCars(filteredCards);
  }

  updateAvailableOffers(filters, filteredCards);
}

//-------------------------------------------------//
//------------------Update Filters-----------------//
//-------------------------------------------------//
function updateAvailableOffers(filters, cards) {
  const offerCheckboxes = document.querySelectorAll(".offerTypeCheckbox");
  const visibleOffers = new Set();

  const priceIsRestricted = filters.minPrice > 0 || filters.maxPrice < 999999;

  if (!priceIsRestricted) {
    offerCheckboxes.forEach((checkbox) => {
      checkbox.disabled = false;
      checkbox.parentElement.style.opacity = "1";
    });
    return;
  }

  cards.forEach((card) => {
    const badge = card.querySelector(".discount-badge");
    const offerText = badge?.innerText.trim().toLowerCase() || "";

    if (offerText === "έκπτωση") visibleOffers.add("discount");
    if (offerText === "προσφορά") visibleOffers.add("offer");
  });

  offerCheckboxes.forEach((checkbox) => {
    const value = checkbox.value;
    const label = checkbox.parentElement;

    if (visibleOffers.size === 0 || visibleOffers.has(value)) {
      checkbox.disabled = false;
      label.style.opacity = "1";
    } else {
      checkbox.disabled = true;
      checkbox.checked = false;
      label.style.opacity = "0.5";
    }
  });
}

function updateAvailableBrands(filters, filteredCards) {
  const brandCheckboxes = document.querySelectorAll(".brandCheckbox");

  const visibleBrands = new Set();
  const brandCounts = {};

  // ✅ Επιλέγεις σωστά την πηγή με βάση το αν υπάρχει φίλτρο τιμής
  const sourceCards =
    filters.minPrice > 0 || filters.maxPrice < Infinity
      ? filteredCards
      : originalCardElements;

  sourceCards.forEach((card) => {
    const modelText = (makerName =
      card
        .querySelector(".maker-title")
        ?.childNodes[0]?.nodeValue.trim()
        .toLowerCase() || "");

    brandCheckboxes.forEach((cb) => {
      const brand = cb.value.toUpperCase();
      if (modelText.includes(brand)) {
        visibleBrands.add(brand);
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

    const hasPriceFilter = filters.minPrice > 0 || filters.maxPrice < Infinity;

    if (hasPriceFilter) {
      if (visibleBrands.has(brand)) {
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
  const s = String(el.value || "")
    .replace(/\u00A0|\u202F/g, "")
    .replace(/[^\d]/g, "");
  return s ? parseInt(s, 10) : fallback;
}

function collectFilters() {
  return {
    // Τιμή
    minPrice: readNumVisible(0, "minPriceInputDesk", "minPriceInputMobile"),
    maxPrice: readNumVisible(
      Infinity,
      "maxPriceInputDesk",
      "maxPriceInputMobile"
    ),
    //
    // Προσφορά
    offerTypes: getCheckedValues(".offerTypeCheckbox"),
    //
    // Αύξουσα-Φθίνουσα τιμή
    priceOrder: document.getElementById("priceOrderSelect")?.value || null,
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

function getCheckedValues(selector) {
  return Array.from(document.querySelectorAll(`${selector}:checked`)).map(
    (cb) => cb.value
  );
}

document.addEventListener("DOMContentLoaded", () => {
  displayCars = document.getElementById("displayCars");
  noResultsMsg = document.querySelector("#noResultsBox");
  paginationContainer = document.getElementById("paginationControls");

  const filterInputs = document.querySelectorAll(
    "input[type='checkbox'], input[type='number'], input[type='text'], select"
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

  //Fetch auth status ΓΙΑ ΝΑ ΜΑΣ ΠΗΓΑΙΝΕΙ ΕΙΤΕ ΣΤΗ carDetails είτε στη carDetailsAnonymous
  fetch("/umbraco/api/auth/status")
    .then((r) => r.json())
    .then((data) => {
      console.log("LoggedIn:", data.loggedIn);

      document.querySelectorAll(".cardCarLink").forEach((link) => {
        if (data.loggedIn) {
          link.setAttribute("href", "/carDetailsMember/");
        } else {
          link.setAttribute("href", "/carDetailsVisitor/");
        }
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

    console.log("🔎 Διαβάζουμε από URL carType:", carTypeParam);

    if (carTypeParam) {
      const typeOfCarMap = {
        suv: "SUV",
        outofroad: "Εκτόσ Δρόμου", // επιτηδες ειναι με 'σ'!! Μην πειραχθεί!
        town: "Πόλης",
        sedan: "Sedan",
      };

      const mappedValue = typeOfCarMap[carTypeParam.toLowerCase()];
      console.log("🎯 Αντιστοιχημένο value:", mappedValue);

      let matchedCheckbox = null;

      document.querySelectorAll(".carTypeCheckbox").forEach((cb) => {
        const left = normalizeGreek(cb.value);
        const right = normalizeGreek(mappedValue);
        console.log("👉 Σύγκριση:", cb.value, "=>", left, "vs", right);

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
      displayCars.innerHTML = "";
      Array.from(originalCardElements).forEach((card) => {
        card.style.display = "flex"; // ή "block", ανάλογα με το layout σου
        displayCars.appendChild(card);
      });
      resetDisplayCarsLayout?.();
      currentPage = 1;
      paginateVisibleCars?.(Array.from(originalCardElements));
    }

    // 6) Recompute counters & τρέξε κενό filter για συγχρονισμό UI
    InitializeCounters?.(Array.from(originalCardElements));
    filterCards?.(collectFilters?.() ?? {});

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
    { rootMargin: "200px" }
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

function storeCarId(event, carId) {
  sessionStorage.setItem("selectedCarId", carId);
}
