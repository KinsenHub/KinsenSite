async function sendCode() {
  const emailEl = document.getElementById("email");
  const msgEl = document.getElementById("statusMsg");
  const btn = document.getElementById("sendButton");

  const email = (emailEl?.value || "").trim();
  if (!email) {
    showMsg("Συμπληρώστε το email σας.", "error");
    return;
  }

  // // 🔄 Spinner ON
  const originalBtnHtml = btn ? btn.innerHTML : "";

  const formData = new FormData();
  formData.append("email", email);

  try {
    const res = await fetch("/umbraco/api/getcode/send", {
      method: "POST",
      body: formData,
    });

    const data = await res.json();

    if (res.ok && data.success) {
      showMsg(data.message, "success");
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Αποστολή Κωδικού`;
      }
      showMsg("Θα ειδοποιηθείτε σύντομα στο email σας.", "success");

      setTimeout(() => {
        resetSendButton();
        window.location.href = "/forgotpassword";
        btn.innerHTML = `Ο κωδικός στάλθηκε`;
      }, 4000);
    } else {
      // ❌ ΑΠΟΤΥΧΙΑ (\Αν το email ΔΕΝ υπάρχει)
      showMsg(data.message || "Το email δεν βρέθηκε σε κάποιο μέλος.", "error");
    }
  } catch (e) {
    showMsg("Σφάλμα σύνδεσης!", "error");
  }
}

function showMsg(text, type) {
  const msgEl = document.getElementById("statusMsg");
  if (!msgEl) return;

  msgEl.textContent = text;
  msgEl.classList.remove("success", "error", "show");
  msgEl.classList.add(type, "show");

  setTimeout(() => {
    msgEl.classList.remove("show");
  }, 3000);
}

function resetSendButton() {
  const btn = document.getElementById("sendButton");
  if (!btn) return;

  btn.disabled = false;
  btn.innerHTML = "Αποστολή κωδικού";
}

window.addEventListener("pageshow", () => {
  resetSendButton();
  const msgEl = document.getElementById("statusMsg");
  if (msgEl) {
    msgEl.textContent = "";
    msgEl.classList.remove("success", "error", "show");
  }
});
