document.addEventListener("DOMContentLoaded", () => {
  setTimeout(() => {
    const items = document.querySelectorAll(".dropdown-item");
    const dropdownButton = document.querySelector(".custom-dropdown-button");
    const resultSpan = document.getElementById("installmentValue");
    console.log("🔁 Retried dropdown items:", items.length);

    const PriceText = document.querySelector(".price-value")?.innerText || "";
    let price = parseFloat(
      PriceText.replace(/\./g, "")
        .replace(",", ".")
        .replace(/[^\d.]/g, ""),
    );

    const vatMultiplier = 1.24;

    items.forEach((item) => {
      item.addEventListener("click", function () {
        const selectedText = this.innerText;
        const button = this.closest(".btn-group").querySelector(
          ".custom-dropdown-button",
        );

        const selectedValue = this.value;

        dropdownButton.textContent = selectedText;

        if (selectedValue === "efapaks") {
          resultSpan.textContent = "-";
        } else {
          const months = parseInt(selectedValue);
          if (!isNaN(months)) {
            const totalWithVAT = price * vatMultiplier;
            const perMonth = totalWithVAT / months;
            resultSpan.innerHTML = `<strong>${perMonth.toFixed(
              2,
            )} €</strong> / μήνα (με ΦΠΑ)`;
          }
        }
      });
    });
  }, 500);
});

(function () {
  const API_BASE = "/umbraco/api/CarApiVisitor";
  if (window.__offerBtnBound) return;
  window.__offerBtnBound = true;

  // --- Helpers για έλεγχο email/κινητού ---
  const isValidEmail = (s) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s || "");
  const normalizeGreekMobile = (s) => {
    let d = (s || "").replace(/\D/g, ""); // κράτα μόνο ψηφία
    if (d.startsWith("0030"))
      d = d.slice(4); // 0030 -> κόψ' το
    else if (d.startsWith("30")) d = d.slice(2);
    return d; // π.χ. 69XXXXXXXX
  };
  const isValidGreekMobile = (s) => /^69\d{8}$/.test(normalizeGreekMobile(s));
  const setValidity = (id, ok) => {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle("is-invalid", !ok);
    el.classList.toggle("is-valid", ok);
  };

  document.addEventListener("click", async (e) => {
    const item = e.target.closest(".dropdown-menu .dropdown-item");
    if (item) {
      const val = item.getAttribute("value") || item.value || "efapaks"; // "efapaks", "6", "12", ...
      const label = (item.textContent || "").trim() || "Εφάπαξ χωρίς Τόκους";

      const ddBtn =
        item.closest(".btn-group")?.querySelector(".custom-dropdown-button") ||
        document.querySelector(".custom-dropdown-button");
      if (ddBtn) {
        ddBtn.textContent = label;
        ddBtn.dataset.plan = val; // 👈 κρατάμε την επιλογή εδώ
      }
      return;
    }

    const btn = e.target.closest("#offerSubmitBtnVisitor");
    const statusEl = document.getElementById("offerStatus");

    if (!btn) return;

    const carId =
      parseInt(sessionStorage.getItem("selectedCarId") || "0", 10) ||
      parseInt(new URLSearchParams(location.search).get("id") || "0", 10) ||
      parseInt(
        document.querySelector("[data-car-id]")?.dataset.carId || "0",
        10,
      );

    console.log("🔎 CarId που θα σταλεί στο submit:", carId);

    if (!carId) {
      console.error("❌ Δεν βρέθηκε έγκυρο CarId");
      return;
    }

    const firstName = document.getElementById("firstName")?.value.trim() || "";
    const lastName = document.getElementById("lastName")?.value.trim() || "";
    const email = document.getElementById("email")?.value.trim() || "";
    const phone = document.getElementById("phone")?.value.trim() || "";

    const maker =
      document.querySelector("[data-maker]")?.dataset.maker ||
      document.querySelector(".car-maker")?.innerText?.trim() ||
      "";

    const model =
      document.querySelector("[data-model]")?.dataset.model ||
      document.querySelector(".car-model")?.innerText?.trim() ||
      "";

    const priceText =
      document.querySelector(".price-value")?.innerText?.trim() || "";

    // ✅ (#efapaks, #toko)
    const planBtn = document.querySelector(".custom-dropdown-button");
    const paymentPlan = planBtn?.dataset.plan || "efapaks"; // "efapaks" ή "6/12/..."
    const interestCode =
      paymentPlan === "efapaks"
        ? document.getElementById("efapaks")?.value || "efapaks"
        : document.getElementById("toko")?.value || "toko";

    // const emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

    // --- Validation ---
    const emailOk = isValidEmail(email);
    const phoneOk = isValidGreekMobile(phone);
    const normalizedPhone = phoneOk
      ? `+30${normalizeGreekMobile(phone)}`
      : phone;

    setValidity("email", emailOk);
    setValidity("phone", phoneOk);

    if (!carId || !firstName || !lastName || !emailOk || !phoneOk) {
      const s = document.getElementById("offerStatus");
      if (s) {
        s.style.display = "block";
        s.textContent = "Συμπλήρωσε σωστά όλα τα πεδία.";
        s.className = "small mt-2 text-danger";
      }
      return;
    }

    const original = btn.innerText;
    btn.disabled = true;
    btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Αποστολή`;

    setTimeout(async () => {
      try {
        // ============================
        // 1️⃣ FETCH → UMBRACO (EMAIL)
        // ============================
        const res = await fetch(`${API_BASE}/submitofferVisitor`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            carId,
            firstName,
            lastName,
            email,
            phone: normalizedPhone,
            paymentPlan,
            interestCode,
          }),
        });

        if (!res.ok) throw new Error(`${res.status} ${await res.text()}`);

        // ============================
        // 2️⃣ FETCH → CRM (INTERACTION)
        // ============================
        const crmPayload = {
          FlowId: 2401,
          AccountId: 0,
          Id: 0,
          StatusId: 0,
          Title: `Αίτημα προσφοράς – ${maker} ${model}`,
          Comments:
            `Αίτημα προσφοράς από επισκέπτη\n` +
            `Όνομα: ${firstName} ${lastName}\n` +
            `Email: ${email}\n` +
            `Τηλέφωνο: ${phone}\n\n` +
            `Όχημα: ${maker} ${model}\n` +
            `Τιμή: ${priceText}\n` +
            `Πλάνο: ${paymentPlan}`,

          Account: {
            Email: email,
            AFM: "",
            PhoneNumber: normalizedPhone,
            Name: firstName,
            Surname: lastName,
            CompanyName: "",
            CustomerType: "Visitor",
            Address: {
              City: "",
              Address: "",
              PostalCode: "",
              CountryCode: "GR",
              County: "",
            },
          },

          CustomFields: [],
        };

        const crmRes = await fetch(
          "https://kineticsuite.saracakis.gr/api/InteractionAPI/CreateInteraction",
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(crmPayload),
          },
        );

        if (!crmRes.ok) {
          console.error("CRM ERROR:", await crmRes.text());
        }

        const successNote = document.getElementById("offerSuccessNote");
        if (successNote) {
          successNote.style.display = "block";
        }

        // ⏳ χρόνος να διαβαστεί
        await new Promise((r) => setTimeout(r, 2200));

        // 🌫️ απαλό fade out
        if (successNote) {
          successNote.classList.add("fade-out");
        }
        if (statusEl) {
          statusEl.classList.add("fade-out");
        }

        await new Promise((r) => setTimeout(r, 800));

        const modalEl = document.getElementById("offerModal");

        // ✅ ΚΛΕΙΣΕ ρητά το modal
        bootstrap.Modal.getInstance(modalEl)?.hide();

        // περίμενε να κλείσει
        await waitModalHidden(modalEl);

        // reset
        document.getElementById("offerForm")?.reset();

        if (modalEl) {
          modalEl.addEventListener("show.bs.modal", () => {
            const note = document.getElementById("offerSuccessNote");
            note?.classList.remove("fade-out");
            if (note) note.style.display = "none";
          });
        }
      } catch (err) {
        console.error("❌ SubmitOffer error:", err);
        const status = document.getElementById("offerStatus");
        if (status) {
          status.textContent = "Κάτι πήγε στραβά. Δοκίμασε ξανά.";
          status.className = "small mt-2 text-danger";
        }
      } finally {
        btn.disabled = false;
        btn.innerText = original;
        cleanupBootstrapArtifacts();
      }
    }, 1000);
  });

  document.addEventListener("submit", (e) => {
    if (e.target?.id === "offerForm") e.preventDefault();
  });
})();

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

function cleanupBootstrapArtifacts() {
  // ΜΟΝΟ Bootstrap backdrops + body lock. Δεν αγγίζουμε άλλα overlays του site.
  document.body.classList.remove("modal-open");
  document.body.style.overflow = "";
  document
    .querySelectorAll(".modal-backdrop, .offcanvas-backdrop")
    .forEach((el) => {
      try {
        el.remove();
      } catch {}
    });
}
