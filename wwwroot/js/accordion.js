document.addEventListener("DOMContentLoaded", function () {
  const questions = document.querySelectorAll(".faq-question");

  questions.forEach((button) => {
    button.addEventListener("click", () => {
      const faqItem = button.parentElement;
      faqItem.classList.toggle("active");
      const answer = faqItem.querySelector(".faq-answer");
      answer.classList.toggle("show");
    });
  });
});
