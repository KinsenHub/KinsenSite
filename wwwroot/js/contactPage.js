document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("contactForm");
  const btn = document.getElementById("contactSubmit");
  const status = document.getElementById("formMsgContact");
  if (!form || !btn) return;

  // Μην επιτρέπεις ΠΟΤΕ native submit (sandbox issue)
  form.addEventListener("submit", (e) => e.preventDefault());
  form.noValidate = true;

  btn.addEventListener("click", async () => {
    const firstName = document.getElementById("first-name").value.trim();
    const lastName = document.getElementById("last-name").value.trim();
    const email = document.getElementById("email").value.trim();
    const message = document.getElementById("message").value.trim();

    const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    if (!firstName || !lastName || !isEmail || !message) {
      status.textContent = "Συμπληρώστε σωστά όλα τα πεδία.";
      status.classList.add("is-error"); // ✅ κάνει το pill κόκκινο
      status.style.opacity = "1";
      status.style.visibility = "visible";
      clearTimeout(window.__formMsgTimer);
      window.__formMsgTimer = setTimeout(() => {
        status.textContent = "";
        status.style.opacity = "0";
        status.style.visibility = "hidden";
        status.classList.remove("is-error"); // ✅ καθάρισμα
      }, 3000);
      return;
    }

    const original = btn.textContent;
    btn.disabled = true;
    btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Αποστολή…`;

    try {
      const res = await fetch("/umbraco/api/contactapi/submit", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ firstName, lastName, email, message }),
        credentials: "same-origin",
      });

      if (!res.ok) throw new Error(await res.text());

      status.textContent =
        "Το μήνυμα στάλθηκε! Θα επικοινωνήσουμε σύντομα μαζί σας!";
      status.style.opacity = "1";
      status.style.visibility = "visible";
      form.reset();

      // 🔹 Σβήσε το μήνυμα μετά από 3"
      setTimeout(() => {
        status.textContent = "";
        status.style.opacity = "0";
        status.style.visibility = "hidden";
      }, 4000);
    } catch (err) {
      console.error("Contact submit error:", err);
      status.textContent = "Κάτι πήγε στραβά. Δοκίμασε ξανά.";
      // status.className = "mt-2 small text-danger";
    } finally {
      btn.disabled = false;
      btn.textContent = original;
    }
  });
});
