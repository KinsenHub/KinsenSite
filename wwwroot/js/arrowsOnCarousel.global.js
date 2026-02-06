document.addEventListener("DOMContentLoaded", () => {
  const track = document.querySelector(".tesla-track");
  if (!track) return;

  if (track.dataset.inited === "true") return;
  track.dataset.inited = "true";

  const prev = document.querySelector(".tesla-arrow.prev");
  const next = document.querySelector(".tesla-arrow.next");

  const originalCards = Array.from(track.children);
  const realCount = originalCards.length;

  if (!realCount || !prev || !next) return;

  const gap = 24;
  let index = 0;
  let isAnimating = false;

  const wrapper = track.closest(".tesla-carousel-wrapper");

  // =========================
  // Βοηθητικά
  // =========================

  const cardWidth = () => originalCards[0].getBoundingClientRect().width + gap; //Παίρνει το πλάτος μιας κάρτας (της πρώτης), και προσθέτει και το gap

  function cardsPerView() {
    const w = wrapper.getBoundingClientRect().width; //Παίρνει το πραγματικό πλάτος (σε px) του wrapper που “κρατάει” το carousel. Π.χ. σε κινητό μπορεί να είναι 360px, σε desktop 1200px.
    return Math.max(1, Math.floor((w + gap) / cardWidth())); //Υπολογίζει “πόσες φορές χωράει το cardWidth μεσα στον διαθεσιμο χώρο
  }

  function canScroll() {
    return realCount > cardsPerView(); //πόσες κάρτες υπάρχουν συνολικά και πόσες χωράνε ταυτόχρονα. Επιστρεφει true/false
  }

  function updateArrows() {
    // δείξε ή κρύψε arrows ανάλογα με το canScroll
    const show = canScroll();
    prev.style.display = show ? "" : "none";
    next.style.display = show ? "" : "none";
    return show;
  }

  // =========================
  // Αρχική απόφαση
  // =========================

  const scrollable = updateArrows();

  if (!scrollable) {
    // Όλα χωράνε → καμία κίνηση
    track.style.transform = "none";
    track.style.transition = "none";
    track.style.justifyContent = "center";
    return;
  }

  track.style.justifyContent = "";

  // =========================
  // Infinite setup
  // =========================

  index = realCount;

  originalCards.forEach((card) => {
    const clone = card.cloneNode(true);
    clone.classList.add("clone");
    track.appendChild(clone);
  });

  [...originalCards].reverse().forEach((card) => {
    const clone = card.cloneNode(true);
    clone.classList.add("clone");
    track.prepend(clone);
  });

  function move(withTransition = true) {
    track.style.transition = withTransition ? "transform 0.6s ease" : "none";

    track.style.transform = `translateX(-${index * cardWidth()}px)`;
  }

  move(false);

  // =========================
  // Controls
  // =========================

  next.addEventListener("click", () => {
    if (isAnimating) return;
    isAnimating = true;

    index++;
    move(true);

    setTimeout(() => {
      if (index >= realCount * 2) {
        index = realCount;
        move(false);
      }
      isAnimating = false;
    }, 600);
  });

  prev.addEventListener("click", () => {
    if (isAnimating) return;
    isAnimating = true;

    index--;
    move(true);

    setTimeout(() => {
      if (index < realCount) {
        index = realCount * 2 - 1;
        move(false);
      }
      isAnimating = false;
    }, 600);
  });

  // =========================
  // Resize
  // =========================

  window.addEventListener("resize", () => {
    const stillScrollable = updateArrows();
    if (!stillScrollable) {
      track.style.transform = "none";
      track.style.transition = "none";
      track.style.justifyContent = "center";
    } else {
      track.style.justifyContent = "";
      move(false);
    }
  });
});
