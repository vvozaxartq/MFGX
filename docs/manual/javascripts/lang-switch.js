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

  function hideOtherLanguageNav(isEn) {
    var primary = document.querySelector('nav.md-nav--primary');
    if (!primary) return;

    var nested = primary.querySelectorAll('li.md-nav__item--nested');
    for (var i = 0; i < nested.length; i++) {
      var li = nested[i];
      var label = li.querySelector('label.md-nav__link');
      if (!label) continue;

      var text = (label.textContent || '').replace(/\s+/g, ' ').trim();
      if (isEn && text === '中文') li.style.display = 'none';
      if (!isEn && text === 'English') li.style.display = 'none';
    }
  }

  function removeInlineLanguageLinks() {
    var content = document.querySelector('.md-content');
    if (!content) return;

    var ps = content.querySelectorAll('p');
    for (var i = 0; i < ps.length; i++) {
      var p = ps[i];
      var a = p.querySelector('a');
      if (!a) continue;

      var pText = (p.textContent || '').trim();
      var aText = (a.textContent || '').trim();
      if (pText !== aText) continue;

      var href = (a.getAttribute('href') || '').trim();
      // Chinese pages previously had: <a href="en/...">English</a>
      if (aText === 'English' && href.indexOf('en/') === 0) {
        p.remove();
        continue;
      }
      // English pages previously had: <a href="../...">中文</a>
      if (aText === '中文' && href.indexOf('../') === 0) {
        p.remove();
      }
    }
  }

  function injectHeaderToggle(isEn) {
    var header = document.querySelector('nav.md-header__inner');
    if (!header) return;
    if (document.getElementById('lang-switch')) return;

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
  }

  onReady(function () {
    var isEn = isEnglishPath(window.location.pathname);
    hideOtherLanguageNav(isEn);
    removeInlineLanguageLinks();
    injectHeaderToggle(isEn);
  });
})();
