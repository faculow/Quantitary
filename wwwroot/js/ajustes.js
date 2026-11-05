(function () {
  'use strict';

  var page = document.querySelector('.settings-page');
  if (!page) return;

  var tabs = Array.from(page.querySelectorAll('[data-settings-target]'));
  var panels = Array.from(page.querySelectorAll('[data-settings-panel]'));

  function showSection(section, updateUrl) {
    if (!panels.some(function (panel) { return panel.dataset.settingsPanel === section; })) {
      section = 'perfil';
    }

    tabs.forEach(function (tab) {
      var active = tab.dataset.settingsTarget === section;
      tab.classList.toggle('active', active);
      tab.setAttribute('aria-current', active ? 'page' : 'false');
    });
    panels.forEach(function (panel) {
      panel.hidden = panel.dataset.settingsPanel !== section;
    });

    if (updateUrl) {
      var url = new URL(window.location.href);
      url.searchParams.set('seccion', section);
      window.history.replaceState({}, '', url);
    }
  }

  tabs.forEach(function (tab) {
    tab.addEventListener('click', function () {
      showSection(tab.dataset.settingsTarget, true);
    });
  });

  function preference(key, fallback) {
    var value = window.localStorage.getItem('quantitary.' + key);
    return value === null ? fallback : value;
  }

  page.querySelectorAll('[data-preference]').forEach(function (checkbox) {
    checkbox.checked = preference(checkbox.dataset.preference, checkbox.checked ? 'true' : 'false') === 'true';
  });

  document.getElementById('saveNotifications').addEventListener('click', function () {
    page.querySelectorAll('[data-preference]').forEach(function (checkbox) {
      window.localStorage.setItem('quantitary.' + checkbox.dataset.preference, String(checkbox.checked));
    });
    var message = document.getElementById('notificationsSaved');
    message.hidden = false;
    window.setTimeout(function () { message.hidden = true; }, 3000);
  });

  var theme = document.getElementById('themePreference');
  var density = document.getElementById('densityPreference');
  theme.value = preference('theme', 'system');
  density.value = preference('density', 'comfortable');

  document.getElementById('saveAppearance').addEventListener('click', function () {
    window.localStorage.setItem('quantitary.theme', theme.value);
    window.localStorage.setItem('quantitary.density', density.value);
    applyAppearance(theme.value, density.value);
    var message = document.getElementById('appearanceSaved');
    message.hidden = false;
    window.setTimeout(function () { message.hidden = true; }, 3000);
  });

  function applyAppearance(themeValue, densityValue) {
    var root = document.documentElement;
    var resolvedTheme = themeValue;
    if (themeValue === 'system') {
      resolvedTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    root.dataset.quantitaryTheme = resolvedTheme;
    root.dataset.quantitaryDensity = densityValue;
  }

  showSection(page.dataset.activeSection || 'perfil', false);
})();
