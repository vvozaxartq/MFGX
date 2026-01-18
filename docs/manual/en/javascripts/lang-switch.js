(function () {
  function onReady(fn) {
    if (document.readyState !== 'loading') fn();
    else document.addEventListener('DOMContentLoaded', fn);
  }

  function isEnglishPath(pathname) {
    // Treat "/en/" as the English site root
    return /\/en\//.test(pathname || '');
  }

  function computeCounterpartUrl(isEn) {
    var loc = window.location;
    var pathname = loc.pathname || '';
    var search = loc.search || '';
    var hash = loc.hash || '';

    // Normalize ".../" => ".../index.html" for consistent mapping
    if (pathname.endsWith('/')) pathname = pathname + 'index.html';

    var swapped;
    if (isEn) {
      // /.../en/xxx.html => /.../xxx.html
      swapped = pathname.replace(/\/en\//, '/');
    } else {
      // /.../xxx.html => /.../en/xxx.html
      swapped = pathname.replace(/\/([^\/]+)$/, '/en/$1');
    }

    return swapped + search + hash;
  }

  // -------- MkDocs (built-in) theme injection (Bootstrap navbar) --------
  function injectMkDocsNavbarToggle(isEn) {
    // MkDocs built-in theme uses: <div class="navbar ..."> ... <ul class="nav navbar-nav ms-md-auto"> ...
    var navbar = document.querySelector('div.navbar');
    if (!navbar) return false;
    if (document.getElementById('lang-switch')) return true;

    var rightUl = navbar.querySelector('ul.nav.navbar-nav.ms-md-auto') || navbar.querySelector('ul.nav.navbar-nav.navbar-right');
    if (!rightUl) {
      // Fallback: append to the main nav list
      rightUl = navbar.querySelector('ul.nav.navbar-nav');
      if (!rightUl) return false;
    }

    var li = document.createElement('li');
    li.className = 'nav-item';

    var a = document.createElement('a');
    a.id = 'lang-switch';
    a.className = 'nav-link lang-switch-btn';
    a.href = computeCounterpartUrl(isEn);
    a.title = isEn ? '切換到中文' : 'Switch to English';
    a.setAttribute('aria-label', a.title);
    a.textContent = isEn ? '中文' : 'EN';

    li.appendChild(a);
    rightUl.insertBefore(li, rightUl.firstChild);
    return true;
  }

  // -------- Material theme injection (md-header) --------
  function injectMaterialHeaderToggle(isEn) {
    var header = document.querySelector('nav.md-header__inner');
    if (!header) return false;
    if (document.getElementById('lang-switch')) return true;

    var btn = document.createElement('a');
    btn.id = 'lang-switch';
    btn.className = 'lang-switch-btn md-header__button';
    btn.href = computeCounterpartUrl(isEn);
    btn.title = isEn ? '切換到中文' : 'Switch to English';
    btn.setAttribute('aria-label', btn.title);
    btn.textContent = isEn ? '中文' : 'EN';

    // Put it next to the search button for visibility
    var searchBtn = header.querySelector('label[for="__search"]');
    if (searchBtn && searchBtn.parentNode === header) header.insertBefore(btn, searchBtn);
    else header.appendChild(btn);
    return true;
  }

  onReady(function () {
    var isEn = isEnglishPath(window.location.pathname);
    // Prefer MkDocs theme injection; fallback to Material
    if (!injectMkDocsNavbarToggle(isEn)) {
      injectMaterialHeaderToggle(isEn);
    }
  });
})();
