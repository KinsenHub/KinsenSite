const codeInput = document.getElementById("resetCode");
const newPasswordInput = document.getElementById("newPassword");
const confirmPasswordInput = document.getElementById("confirmPassword");
const statusMsg = document.getElementById("statusMsg");
const actionBtn = document.getElementById("actionBtn");
const strongPasswordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;
let isResetCodeValid = false;
let isNewPasswordValid = false;
let isConfirmMatch = false;
let debounceTimer = null;

// Αφορά τον έλεγχο για τον κωδικό Αποστολής:
codeInput.addEventListener("input", () => {
  const code = codeInput.value.trim();

  clearTimeout(debounceTimer);

  if (code.length < 8) {
    statusMsg.innerHTML = "";
    return;
  }

  debounceTimer = setTimeout(() => {
    verifyCodeLive(code);
  }, 300); // ⏱ debounce 400ms
});

async function verifyCodeLive(code) {
  const formData = new FormData();
  formData.append("code", code);

  try {
    const res = await fetch("/umbraco/api/forgotpassword/verify", {
      method: "POST",
      body: formData,
    });

    const data = await res.json();

    if (res.ok && data.success) {
      showMsg("Ο κωδικός είναι έγκυρος ✔", "success");
      isResetCodeValid = true;
    } else {
      isResetCodeValid = false;
      showMsg("Μη έγκυρος κωδικός ✖", "error");
    }
  } catch {
    showMsg("Σφάλμα ελέγχου", "error");
  }
  updateActionButton();
}

// Αφορά τον έλεγχο για τον νέο κωδικό που θα βάλει ο χρήστης
newPasswordInput.addEventListener("input", () => {
  clearTimeout(debounceTimer);

  debounceTimer = setTimeout(() => {
    validatePassword();
  }, 400);
});

function validatePassword() {
  const pass = (newPasswordInput.value || "").trim();

  // ✅ ΜΗΝ ενοχλείς πριν τους 8 χαρακτήρες
  if (pass.length < 8) {
    hideMsg();
    return;
  }

  const hasUpper = /[A-ZΑ-Ω]/.test(pass);
  const hasDigit = /\d/.test(pass);

  if (hasUpper && hasDigit && pass.length >= 10) {
    isNewPasswordValid = true;
    showMsg("Ισχυρός κωδικός ✔", "success");
  } else {
    showMsg(
      `Ο κωδικός πρέπει να έχει: 10+ χαρακτήρες, 1 κεφαλαίο και 1 αριθμό`,
      "error",
    );
    isNewPasswordValid = false;
  }
  updateActionButton();
}

// Αφορά τον έλεγχο για τον κωδικό επιβεβαίωσης που θα βάλει ο χρήστης
confirmPasswordInput.addEventListener("input", () => {
  clearTimeout(debounceTimer);

  debounceTimer = setTimeout(() => {
    validateConfirmPassword();
  }, 300);
});

function validateConfirmPassword() {
  const pass = (newPasswordInput.value || "").trim();
  const confirm = (confirmPasswordInput.value || "").trim();

  //🔒 Μην ελέγχεις confirm αν το password ΔΕΝ είναι valid ακόμα
  if (confirm.length < 10) {
    hideMsg();
    isConfirmMatch = false;
    updateActionButton();
    return;
  }

  // 🔕 Μην ενοχλείς αν δεν έχουν αρχίσει να γράφουν και τα δύο
  if (!pass || !confirm) {
    hideMsg();
    return;
  }

  if (pass !== confirm) {
    showMsg("Οι κωδικοί δεν ταιριάζουν ✖", "error");
    isConfirmMatch = false;
  } else {
    showMsg("Οι κωδικοί ταιριάζουν ✔", "success");
    isConfirmMatch = true;
  }
  updateActionButton();
}

function hideMsg() {
  if (!statusMsg) return;
  statusMsg.classList.remove("show", "success", "error");
  statusMsg.innerHTML = "";
}

function showMsg(text, type) {
  statusMsg.innerHTML = text;
  statusMsg.className = `status-msg ${type} show`;

  setTimeout(() => {
    statusMsg.classList.remove("show");
  }, 3000);
}

// κουμπί "Επικύρωση Κωδικού"
function updateActionButton() {
  if (isResetCodeValid && isNewPasswordValid && isConfirmMatch) {
    actionBtn.disabled = false;
    actionBtn.innerHTML = `<i class="fa-solid fa-check fa-beat-fade"></i> Αποθήκευση`;
  } else {
    actionBtn.disabled = true;
    actionBtn.innerHTML = `Αποθήκευση`;
  }
}

async function submitNewPassword() {
  if (isResetCodeValid && isNewPasswordValid && isConfirmMatch) {
    const resetCode = document.getElementById("resetCode").value.trim();
    const newPassword = document.getElementById("newPassword").value.trim();

    const formData = new FormData();
    formData.append("code", resetCode);
    formData.append("newPassword", newPassword);

    actionBtn.disabled = true;
    try {
      const res = await fetch("/umbraco/api/forgotpassword/reset", {
        method: "POST",
        body: formData,
      });

      const data = await res.json();

      if (res.ok && data.success) {
        showMsg("Ο κωδικός άλλαξε επιτυχώς ✔", "success");

        setTimeout(() => {
          window.location.href = "/login";
        }, 2000);
      } else {
        showMsg(data.message || "Αποτυχία αλλαγής κωδικού", "error");
        actionBtn.disabled = false;
      }
    } catch {
      showMsg("Σφάλμα σύνδεσης", "error");
      actionBtn.disabled = false;
    }
  }
}

document.addEventListener("DOMContentLoaded", function () {
  // Πιάνουμε όλα τα blocks που έχουν password + eye button
  const blocks = document.querySelectorAll(".new-pass");

  blocks.forEach((block) => {
    const passwordInput = block.querySelector(
      'input[type="password"], input[type="text"]',
    );
    const toggleButton = block.querySelector(".toggle-btn");
    const toggleIcon = toggleButton
      ? toggleButton.querySelector(".icon i")
      : null;

    if (!passwordInput || !toggleButton || !toggleIcon) return;

    toggleButton.addEventListener("click", function () {
      const isPassword = passwordInput.type === "password";
      passwordInput.type = isPassword ? "text" : "password";

      if (isPassword) {
        toggleIcon.classList.remove("fa-eye");
        toggleIcon.classList.add("fa-eye-slash");
      } else {
        toggleIcon.classList.remove("fa-eye-slash");
        toggleIcon.classList.add("fa-eye");
      }
    });
  });
});
