/* ==========================================================================
   FrostUI for the Web · v0.1.0 — runtime
   Theme, accent, glass-blur preferences + tiny helpers. No dependencies.
   ========================================================================== */
(function (global) {
  'use strict';

  var VERSION = '0.1.0';
  var STORE = { theme: 'frostui.theme', accent: 'frostui.accent', blur: 'frostui.blur' };
  var ACCENTS = ['azure', 'mint', 'violet', 'sakura', 'amber'];
  var root = document.documentElement;
  var media = global.matchMedia ? global.matchMedia('(prefers-color-scheme: light)') : null;
  var state = { theme: 'dark', accent: 'azure', blur: 24 };

  function read(key, fallback) {
    try {
      var value = localStorage.getItem(key);
      return value === null ? fallback : value;
    } catch (e) { return fallback; }
  }
  function write(key, value) {
    try { localStorage.setItem(key, value); } catch (e) { /* private mode */ }
  }
  function resolve(theme) {
    if (theme === 'auto') return media && media.matches ? 'light' : 'dark';
    return theme === 'light' ? 'light' : 'dark';
  }
  function emit(name, detail) {
    document.dispatchEvent(new CustomEvent('frostui:' + name, { detail: detail }));
  }

  /* ---------- theme ---------- */
  function setTheme(theme, persist) {
    state.theme = theme === 'light' || theme === 'auto' ? theme : 'dark';
    var resolved = resolve(state.theme);
    root.setAttribute('data-f-theme', resolved);
    if (persist !== false) write(STORE.theme, state.theme);
    document.querySelectorAll('[data-f-theme-set]').forEach(function (btn) {
      btn.setAttribute('aria-pressed', String(btn.getAttribute('data-f-theme-set') === state.theme));
    });
    emit('theme', { theme: state.theme, resolved: resolved });
    return resolved;
  }
  function toggleTheme() {
    return setTheme(resolve(state.theme) === 'dark' ? 'light' : 'dark');
  }

  /* ---------- accent ---------- */
  function setAccent(name, persist) {
    if (ACCENTS.indexOf(name) < 0) name = 'azure';
    state.accent = name;
    root.setAttribute('data-f-accent', name);
    if (persist !== false) write(STORE.accent, name);
    document.querySelectorAll('[data-f-accent]').forEach(function (btn) {
      btn.setAttribute('aria-pressed', String(btn.getAttribute('data-f-accent') === name));
    });
    emit('accent', { accent: name });
    return name;
  }

  /* ---------- blur ---------- */
  function setBlur(value, persist) {
    var n = Math.max(0, Math.min(48, parseInt(value, 10) || 0));
    state.blur = n;
    root.style.setProperty('--f-blur', n + 'px');
    if (persist !== false) write(STORE.blur, String(n));
    document.querySelectorAll('[data-f-blur]').forEach(function (input) {
      if (input.value !== String(n)) input.value = String(n);
      input.style.setProperty('--f-fill', (n / 48) * 100 + '%');
    });
    document.querySelectorAll('[data-f-blur-out]').forEach(function (out) {
      out.textContent = n + ' px';
    });
    emit('blur', { blur: n });
    return n;
  }

  /* ---------- toast ---------- */
  var toastEl = null;
  var toastTimer = null;
  function toast(message) {
    if (!toastEl) {
      toastEl = document.createElement('div');
      toastEl.className = 'f-toast';
      toastEl.setAttribute('role', 'status');
      toastEl.setAttribute('aria-live', 'polite');
      document.body.appendChild(toastEl);
    }
    toastEl.textContent = message;
    toastEl.classList.add('is-on');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('is-on'); }, 2200);
  }

  /* ---------- copy ---------- */
  var IS_ZH = (document.documentElement.lang || 'en').indexOf('zh') === 0;
  function copy(text, message) {
    var done = function () { toast(message || (IS_ZH ? '已复制到剪贴板' : 'Copied to clipboard')); };
    var fallback = function () {
      var area = document.createElement('textarea');
      area.value = text;
      area.setAttribute('readonly', '');
      area.style.cssText = 'position:fixed;top:-100px;opacity:0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); done(); }
      catch (e) { toast(IS_ZH ? '复制失败，请手动选择' : 'Copy failed'); }
      document.body.removeChild(area);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done, fallback);
    } else {
      fallback();
    }
  }

  /* ---------- bind ---------- */
  function bind() {
    document.querySelectorAll('[data-f-theme-toggle]').forEach(function (btn) {
      if (btn.__fBound) return;
      btn.__fBound = true;
      btn.addEventListener('click', function () { toggleTheme(); });
    });
    document.querySelectorAll('[data-f-theme-set]').forEach(function (btn) {
      if (btn.__fBound) return;
      btn.__fBound = true;
      btn.addEventListener('click', function () { setTheme(btn.getAttribute('data-f-theme-set')); });
    });
    document.querySelectorAll('[data-f-accent]').forEach(function (btn) {
      if (btn.__fBound || btn === root) return;
      btn.__fBound = true;
      btn.addEventListener('click', function () { setAccent(btn.getAttribute('data-f-accent')); });
    });
    document.querySelectorAll('[data-f-blur]').forEach(function (input) {
      if (input.__fBound) return;
      input.__fBound = true;
      input.addEventListener('input', function () { setBlur(input.value); });
    });
    document.querySelectorAll('[data-f-copy]').forEach(function (btn) {
      if (btn.__fBound) return;
      btn.__fBound = true;
      btn.addEventListener('click', function () {
        var selector = btn.getAttribute('data-f-copy');
        var target = selector && selector !== 'self' ? document.querySelector(selector) : null;
        copy(target ? target.textContent.trim() : btn.getAttribute('data-f-copy-text') || '');
      });
    });
  }

  /* ---------- init ---------- */
  function init(options) {
    options = options || {};
    if (options.prefix) {
      STORE.theme = options.prefix + '.theme';
      STORE.accent = options.prefix + '.accent';
      STORE.blur = options.prefix + '.blur';
    }
    setTheme(read(STORE.theme, 'dark'), false);
    setAccent(read(STORE.accent, 'azure'), false);
    setBlur(read(STORE.blur, '24'), false);

    if (media) {
      var onChange = function () {
        if (state.theme === 'auto') setTheme('auto', false);
      };
      if (media.addEventListener) media.addEventListener('change', onChange);
      else if (media.addListener) media.addListener(onChange);
    }

    bind();
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', bind);
    }
    emit('ready', { version: VERSION });
    return API;
  }

  var API = {
    version: VERSION,
    accents: ACCENTS.slice(),
    init: init,
    bind: bind,
    setTheme: setTheme,
    toggleTheme: toggleTheme,
    setAccent: setAccent,
    setBlur: setBlur,
    toast: toast,
    copy: copy,
    get theme() { return resolve(state.theme); },
    get themePreference() { return state.theme; },
    get accent() { return state.accent; },
    get blur() { return state.blur; }
  };

  global.FrostUI = API;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(); });
  } else {
    init();
  }
})(typeof window !== 'undefined' ? window : this);
