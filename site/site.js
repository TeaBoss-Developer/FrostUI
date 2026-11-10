/* FrostUI website — page behaviour on top of the library */
(function () {
  'use strict';

  var root = document.documentElement;
  var header = document.querySelector('.site-header');

  /* header elevation */
  function onScroll() {
    if (header) header.classList.toggle('is-stuck', window.scrollY > 6);
  }
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  /* ambient background */
  var ambient = document.querySelector('.f-ambient');
  var ambientToggle = document.querySelector('[data-f-ambient-toggle]');
  function setAmbient(on, persist) {
    if (ambient) ambient.style.display = on ? '' : 'none';
    if (ambientToggle) ambientToggle.checked = !!on;
    if (persist) { try { localStorage.setItem('frostui.ambient', on ? '1' : '0'); } catch (e) {} }
  }
  var ambientOn = true;
  try { ambientOn = localStorage.getItem('frostui.ambient') !== '0'; } catch (e) {}
  setAmbient(ambientOn, false);
  if (ambientToggle) ambientToggle.addEventListener('change', function () { setAmbient(ambientToggle.checked, true); });

  /* reduced motion */
  var motionToggle = document.querySelector('[data-f-motion-toggle]');
  function setMotion(on, persist) {
    root.classList.toggle('reduce-motion', !on);
    if (motionToggle) motionToggle.checked = !!on;
    if (persist) { try { localStorage.setItem('frostui.motion', on ? '1' : '0'); } catch (e) {} }
  }
  var motionOn = true;
  try { motionOn = localStorage.getItem('frostui.motion') !== '0'; } catch (e) {}
  setMotion(motionOn, false);
  if (motionToggle) motionToggle.addEventListener('change', function () { setMotion(motionToggle.checked, true); });

  /* scrollspy */
  var navLinks = Array.prototype.slice.call(document.querySelectorAll('.site-nav a[href^="#"]'));
  var sections = navLinks
    .map(function (a) { return document.querySelector(a.getAttribute('href')); })
    .filter(Boolean);

  if ('IntersectionObserver' in window && sections.length) {
    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        navLinks.forEach(function (a) {
          a.classList.toggle('is-active', a.getAttribute('href') === '#' + entry.target.id);
        });
      });
    }, { rootMargin: '-45% 0px -50% 0px', threshold: 0 });
    sections.forEach(function (s) { spy.observe(s); });
  }

  /* live labels in the theming playground */
  var ACCENT_NAMES = { azure: 'Azure', mint: 'Mint', violet: 'Violet', sakura: 'Sakura', amber: 'Amber' };
  var accentNameEl = document.getElementById('accentName');
  var themeNameEl = document.getElementById('themeName');
  function syncLabels() {
    if (!window.FrostUI) return;
    if (accentNameEl) accentNameEl.textContent = ACCENT_NAMES[FrostUI.accent] || 'Azure';
    if (themeNameEl) themeNameEl.textContent = FrostUI.theme === 'light' ? 'Light' : 'Dark';
  }
  document.addEventListener('frostui:accent', syncLabels);
  document.addEventListener('frostui:theme', syncLabels);
  document.addEventListener('frostui:ready', syncLabels);
  syncLabels();

  /* year */
  var year = document.getElementById('year');
  if (year) year.textContent = String(new Date().getFullYear());
})();
