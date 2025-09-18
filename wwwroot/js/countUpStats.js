document.addEventListener("DOMContentLoaded", () => {
  const statsRow = document.querySelector(".stats-row");
  const counters = document.querySelectorAll(".stat-number");

  const startCounterAnimation = () => {
    counters.forEach((counter) => {
      const target = parseInt(counter.getAttribute("data-target"), 10);
      const showPlus = counter.getAttribute("data-plus") === "true";
      const unit = counter.getAttribute("data-unit");
      let current = 0;
      const increment = Math.ceil(target / 200);

      const updateCount = () => {
        if (current < target) {
          current += increment;
          if (current > target) current = target;

          let suffix = "";
          if (showPlus) {
            suffix = '<span class="stat-suffix">+</span>';
          } else if (unit) {
            suffix = `<span class="stat-suffix">${unit}</span>`;
          }

          counter.innerHTML = `${current}${suffix}`;
          requestAnimationFrame(updateCount);
        } else {
          let suffix = "";
          if (showPlus) {
            suffix = '<span class="stat-suffix">+</span>';
          } else if (unit) {
            suffix = `<span class="stat-suffix">${unit}</span>`;
          }

          counter.innerHTML = `${target}${suffix}`;
        }
      };

      updateCount();
    });
  };

  const observer = new IntersectionObserver(
    (entries, observer) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          startCounterAnimation();
          observer.disconnect(); // Μόνο μία φορά
        }
      });
    },
    { threshold: 0.5 } // 50% του στοιχείου πρέπει να είναι ορατό
  );

  if (statsRow) {
    observer.observe(statsRow);
  }
});
