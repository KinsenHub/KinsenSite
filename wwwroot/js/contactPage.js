document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("contactForm");
  const btn = document.getElementById("contactSubmit");
  const status = document.getElementById("contactStatus");
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
      status.textContent = "Συμπλήρωσε σωστά όλα τα πεδία.";
      status.className = "mt-2 small text-danger";
      return;
    }

    const original = btn.textContent;
    btn.disabled = true;
    btn.textContent = "Αποστολή…";

    try {
      const res = await fetch("/umbraco/api/contactapi/submit", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ firstName, lastName, email, message }),
        credentials: "same-origin", // αν χρειαστεί cookies/preview
      });

      if (!res.ok) throw new Error(await res.text());

      status.textContent = "Το μήνυμα στάλθηκε! Θα επικοινωνήσουμε σύντομα.";
      status.className = "mt-2 small text-success";
      form.reset();
    } catch (err) {
      console.error("Contact submit error:", err);
      status.textContent = "Κάτι πήγε στραβά. Δοκίμασε ξανά.";
      status.className = "mt-2 small text-danger";
    } finally {
      btn.disabled = false;
      btn.textContent = original;
    }
  });
});
