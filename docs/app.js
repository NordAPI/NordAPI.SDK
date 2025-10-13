//
// Copy buttons
//
document.querySelectorAll('.copy').forEach(btn => {
  btn.addEventListener('click', async () => {
    try {
      await navigator.clipboard.writeText(btn.dataset.copy);
      btn.textContent = 'Copied!';
      setTimeout(() => (btn.textContent = 'Copy'), 1200);
    } catch {
      btn.textContent = 'Copy failed';
      setTimeout(() => (btn.textContent = 'Copy'), 1500);
    }
  });
});

//
// Mobile menu toggle
//
const toggle = document.querySelector('.menu-toggle');
const nav = document.querySelector('.topnav');
const navLinks = document.querySelectorAll('#nav-links a');

toggle?.addEventListener('click', () => {
  const open = nav.classList.toggle('open');
  toggle.setAttribute('aria-expanded', String(open));
});

navLinks.forEach(a => a.addEventListener('click', () => {
  if (nav.classList.contains('open')) {
    nav.classList.remove('open');
    toggle.setAttribute('aria-expanded', 'false');
  }
}));
