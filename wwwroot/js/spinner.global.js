//************* SPINNER ***************
let loader = document.getElementById("globalLoader");

function showLoading() {
  if (!loader) return;

  document.body.classList.add("loading");
  loader.classList.remove("hidden");
}

function hideLoading(delay = 0) {
  if (!loader) return;

  setTimeout(() => {
    loader.classList.add("hidden");
    document.body.classList.remove("loading");
  }, delay);
}

document.addEventListener("click", function (e) {
  const link = e.target.closest("a");
  if (!link) return;

  const href = link.getAttribute("href");
  if (!href || href.startsWith("#") || link.target === "_blank") return;

  e.preventDefault();
  showLoading();

  setTimeout(() => {
    window.location.href = href;
  }, 150);
});

window.addEventListener("pageshow", function () {
  hideLoading(0);
});
