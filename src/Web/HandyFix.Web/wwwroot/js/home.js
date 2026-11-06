// The home page (Views/Home/Index.cshtml): each section fades in as it comes into view.
document.addEventListener("DOMContentLoaded", function() {
    const observerOptions = {
      threshold: 0.1
    };

    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.style.opacity = '1';
          entry.target.style.transform = 'translateY(0)';
        }
      });
    }, observerOptions);

    document.querySelectorAll('section').forEach(section => {
      section.style.transition = 'all 0.7s ease';
      section.style.opacity = '0';
      section.style.transform = 'translateY(2.5rem)';
      observer.observe(section);
    });
});
