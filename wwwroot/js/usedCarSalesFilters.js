let sidebar;

function toggleFilters() {
  sidebar = document.getElementById("filterSidebar");
  sidebar.classList.toggle("show");
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
  //displayCars.querySelectorAll(".cardCar").forEach((card) => card.remove());

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
    carKlm = parseFloat(klmText);

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
    container.innerHTML = "";

    // ✅ Καθάρισε παλιές κάρτες
    //container.querySelectorAll(".cardCar").forEach((card) => card.remove());

    filteredCards.forEach((card) => container.appendChild(card));
    currentPage = 1;
    paginateVisibleCars(filteredCards);
  }

  if (anyMatch) {
    if (noResultsMsg) noResultsMsg.style.display = "none";
    resetDisplayCarsLayout();
  } else {
    if (noResultsMsg) noResultsMsg.style.display = "block";
    displayCars.style.justifyContent = "center";
    displayCars.style.alignItems = "center";
  }

  // displayCars.innerHTML = "";

  if (!filters.priceOrder) {
    displayCars.innerHTML = ""; // ✅ Καθαρισμός container

    filteredCards.forEach((card) => {
      card.style.display = "flex"; // ή "block" ανάλογα με το layout
      displayCars.appendChild(card); // ✅ Re-append για να εμφανιστούν σωστά
    });

    currentPage = 1;
    paginateVisibleCars(filteredCards);
  }

  updateAvailableOffers(filters, filteredCards);
  //updateAvailableBrands(filters, filteredCards);
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

function collectFilters() {
  return {
    // Τιμή
    minPrice: parseFloat(document.getElementById("minPriceInput")?.value) || 0,
    maxPrice:
      parseFloat(document.getElementById("maxPriceInput")?.value) || Infinity,
    //
    // Προσφορά
    offerTypes: getCheckedValues(".offerTypeCheckbox"),
    //
    // Αύξουσα-Φθίνουσα τιμή
    priceOrder: document.getElementById("priceOrderSelect")?.value || null,
    //
    // Έτος
    minYear: parseInt(document.getElementById("minYearInput")?.value) || 1990,
    maxYear:
      parseInt(document.getElementById("maxYearInput")?.value) ||
      new Date().getFullYear(),
    //
    // Χιλιόμετρα
    minKm: parseFloat(document.getElementById("minKlmInput")?.value) || 0,
    maxKm:
      parseFloat(document.getElementById("maxKlmInput")?.value) || Infinity,
    //
    // Κυβικά
    minCc: parseInt(document.getElementById("minCcInput")?.value) || 0,
    maxCc: parseInt(document.getElementById("maxCcInput")?.value) || Infinity,
    //
    // Ίπποι
    minhp: parseInt(document.getElementById("minHpInput")?.value) || 0,
    maxhp: parseInt(document.getElementById("maxHpInput")?.value) || Infinity,
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
  noResultsMsg = document.querySelector(".noResultsMsg");
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
  });

  const sidebar = document.getElementById("filterSidebar");
  const openBtn = document.getElementById("toggleSidebarBtn");
  const closeBtn = document.getElementById("closeSidebarBtn");

  openBtn.addEventListener("click", () => {
    sidebar.classList.add("open");
    document.body.style.overflow = "hidden";
  });

  closeBtn.addEventListener("click", () => {
    sidebar.classList.remove("open");
    document.body.style.overflow = "";
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
        console.log("✅ Τικάραμε το φίλτρο:", matchedCheckbox.value);

        // Άνοιξε accordion
        const filterItem = matchedCheckbox.closest(".filter-item");
        const toggleButton = filterItem?.querySelector(".filter-toggle");
        if (toggleButton) toggleButton.click();

        // Εφάρμοσε φιλτράρισμα
        const filters = collectFilters();
        console.log("NAV πριν:", document.getElementById("navbarCollapse"));
        filterCards(filters);
        console.log("NAV μετά:", document.getElementById("navbarCollapse"));
        const navbar = document.getElementById("navbarCollapse");
        console.log(
          "Navbar visible?",
          !!navbar,
          "offsetTop:",
          navbar?.offsetTop,
          "height:",
          navbar?.offsetHeight
        );
        console.log("Scroll position:", window.scrollY);

        // Άνοιξε sidebar
        document.getElementById("toggleSidebarBtn")?.click();
      } else {
        console.warn("⚠️ Δεν βρέθηκε checkbox για:", carTypeParam);
      }
    }
  }, 300);
});

//----------------Clear Filters-------------------//
function clearAllFilters() {
  // Καθαρισμός όλων των input fields
  document.querySelectorAll("input, select").forEach((input) => {
    if (input.type === "checkbox" || input.type === "radio") {
      input.checked = false;
    } else {
      input.value = "";
    }
  });

  document.querySelectorAll("input[type='checkbox']").forEach((checkbox) => {
    checkbox.disabled = false;
    checkbox.parentElement.style.opacity = "1";
  });

  // Επαναφορά του filters object στην αρχική του μορφή
  filters = {
    minPrice: null,
    maxPrice: null,
    AscDescPrice: [],
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
  };

  // Κρύψε το μήνυμα "Δεν υπάρχουν αποτελέσματα"
  if (noResultsMsg) noResultsMsg.style.display = "none";

  displayCars.querySelectorAll(".cardCar").forEach((card) => card.remove());

  // Επαναφορά κάρτας & layout μετά από καθυστέρηση
  setTimeout(() => {
    allCards.forEach((card) => {
      card.style.display = "flex"; // ή "block" αν χρησιμοποιείται block layout
      displayCars.appendChild(card);
    });

    resetDisplayCarsLayout();

    currentPage = 1;
    paginateVisibleCars(Array.from(originalCardElements));
  }, 300);

  // Κλείσιμο όλων των φίλτρων και επαναφορά βελών
  document.querySelectorAll(".filter-item").forEach((item) => {
    item.classList.remove("active");
  });

  InitializeCounters(originalCardElements);
}

let currentPage = 1;
const carsPerPage = 6;

function paginateVisibleCars(carList) {
  const totalPages = Math.ceil(carList.length / carsPerPage);
  const paginationContainer = document.getElementById("paginationControls");

  // Κρύψε όλες τις κάρτες
  carList.forEach((card) => (card.style.display = "none"));

  // Υπολογισμός ορατών καρτών για τη σελίδα
  const start = (currentPage - 1) * carsPerPage;
  const end = start + carsPerPage;

  carList.slice(start, end).forEach((card) => {
    card.style.display = "flex"; // ή "block" ανάλογα το layout σου
  });

  // Καθαρισμός προηγούμενων κουμπιών
  paginationContainer.innerHTML = "";

  // Δεν χρειάζεται pagination αν έχουμε μόνο 1 σελίδα
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

  // Σελίδες
  for (let i = 1; i <= totalPages; i++) {
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

function resetDisplayCarsLayout() {
  displayCars.style.justifyContent = "flex-start";
  displayCars.style.alignItems = "flex-start";
  displayCars.style.marginTop = "0";

  const navbar = document.getElementById("navbarCollapse");
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
