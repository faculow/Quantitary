
  const button = document.getElementById('robotButton');
  const icon = document.getElementById('robotIcon');
  const panel = document.getElementById('chatPanel');
  const closeButton = document.getElementById('closeButton');
  const minimizeButton = document.getElementById('minimizeButton');
  const fullscreenButton = document.getElementById('fullscreenButton');

  function setOpen(open) {
    panel.classList.toggle('visible', open);
    button.classList.toggle('is-open', open);
    document.getElementById('quantitary-ia').classList.toggle('is-chat-open', open);
    button.setAttribute('aria-expanded', String(open));
    button.setAttribute('aria-label', open ? 'Cerrar Quantitary IA' : 'Abrir Quantitary IA');
    icon.src = open ? '/ia/assets/quantitary-ia-active.svg' :
      (button.matches(':hover') ? '/ia/assets/quantitary-ia-hover.svg' : '/ia/assets/quantitary-ia-normal.svg');
    if (!open) setFullscreen(false);
    if (open) panel.dispatchEvent(new Event('qty-open'));
  }

  function setFullscreen(on) {
    var root = document.getElementById('quantitary-ia');
    root.classList.toggle('fullscreen', on);
    fullscreenButton.setAttribute('aria-pressed', String(on));
    fullscreenButton.setAttribute('aria-label', on ? 'Achicar' : 'Agrandar');
    fullscreenButton.setAttribute('title', on ? 'Achicar' : 'Agrandar');
    panel.dispatchEvent(new CustomEvent('qty-fullscreen', { detail: { fullscreen: on } }));
  }

  button.addEventListener('mouseenter', () => {
    if (!button.classList.contains('is-open')) icon.src = '/ia/assets/quantitary-ia-hover.svg';
  });
  button.addEventListener('mouseleave', () => {
    if (!button.classList.contains('is-open')) icon.src = '/ia/assets/quantitary-ia-normal.svg';
  });
  button.addEventListener('click', () => setOpen(!panel.classList.contains('visible')));
  closeButton.addEventListener('click', () => setOpen(false));
  minimizeButton.addEventListener('click', () => setOpen(false));
  fullscreenButton.addEventListener('click', () => {
    setFullscreen(!document.getElementById('quantitary-ia').classList.contains('fullscreen'));
  });
  document.getElementById('assistantBackdrop').addEventListener('click', () => setFullscreen(false));
  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape' || !panel.classList.contains('visible')) return;
    var dialog = document.getElementById('chatDialog');
    if (dialog && !dialog.hidden) return;
    if (document.getElementById('quantitary-ia').classList.contains('fullscreen')) setFullscreen(false);
    else setOpen(false);
  });

(function () {
  'use strict';

  var GREETING = '¡Hola! Soy tu asistente personal de Quantitary. Podés preguntarme lo que necesites sobre tu negocio y te voy a ayudar a encontrar respuestas.';
  var SUGGESTIONS_BY_SECTION = {
    stock: [
      '¿Qué productos tengo que reponer primero?',
      'Mostrame los productos con stock bajo.',
      'Mostrame una tabla del stock.'
    ],
    pdv: [
      '¿Qué productos conviene ofrecer en esta venta?',
      '¿Cuál es el precio y stock de un producto?',
      'Ayudame a revisar el rendimiento de las ventas.'
    ],
    home: [
      'Resumime el estado actual de mi negocio.',
      '¿Cómo vienen las ventas y ganancias?',
      '¿Qué debería revisar hoy?'
    ],
    ajustes: [
      '¿Qué información puede analizar Quantitary IA?',
      '¿Cómo se protegen los datos de mi negocio?',
      '¿Cómo puedo aprovechar mejor el asistente?'
    ]
  };
  var MAX_LENGTH = 2000;
  var MAX_HISTORY = 20;
  var MIN_TYPING_MS = 1000;
  var AVATAR_SRC = '/ia/assets/quantitary-ia-normal.svg';
  var STATUS_IDLE = 'Tu asistente inteligente';
  var STATUS_BUSY = 'Pensando…';
  var CONNECTION_ERROR = 'No pude conectarme con el servidor de Quantitary IA. Verificá que la aplicación siga en ejecución e intentá de nuevo.';
  var SVG_NS = 'http://www.w3.org/2000/svg';
  var COLORS = ['#1769e8', '#26e5ff', '#071a3d', '#7fa6d9'];
  var ALLOWED_ACTION_PATHS = [
    '/Stock/Index', '/Stock/Crear', '/Stock/Importar', '/Pdv/Index',
    '/Home/Historial', '/Home/IndicadoresYOportunidades',
    '/Home/AnalisisVentasGanancias', '/Home/AnalisisProducto', '/Ajustes/Index'
  ];

  var panel = document.getElementById('chatPanel');
  var messagesEl = document.getElementById('messages');
  var form = document.getElementById('chatForm');
  var input = document.getElementById('chatInput');
  var sendBtn = document.getElementById('sendButton');
  var attachBtn = document.getElementById('attachButton');
  var imageInput = document.getElementById('chatImageInput');
  var attachmentPreview = document.getElementById('attachmentPreview');
  var attachmentThumbnail = document.getElementById('attachmentThumbnail');
  var attachmentName = document.getElementById('attachmentName');
  var attachmentMeta = document.getElementById('attachmentMeta');
  var newChatBtn = document.getElementById('newChatButton');
  var statusEl = document.getElementById('assistantStatus');
  var historyPanel = document.getElementById('chatHistoryPanel');
  var historyList = document.getElementById('chatHistoryList');
  var historySearch = document.getElementById('historySearch');
  var historyNewChatBtn = document.getElementById('historyNewChatButton');
  var dialogBackdrop = document.getElementById('chatDialog');
  var dialogTitle = document.getElementById('chatDialogTitle');
  var dialogText = document.getElementById('chatDialogText');
  var dialogInput = document.getElementById('chatDialogInput');
  var dialogConfirm = document.getElementById('chatDialogConfirm');
  var dialogCancel = document.getElementById('chatDialogCancel');
  var currentChatId = null;
  var assistantRoot = document.getElementById('quantitary-ia');
  var currentSection = assistantRoot.dataset.section || 'home';
  var currentAction = assistantRoot.dataset.action || 'index';
  function suggestionsForPage() {
    if (currentSection === 'home' && (currentAction.indexOf('analisis') >= 0 || currentAction.indexOf('indicadores') >= 0)) {
      return [
        '¿Qué tendencia muestran mis ventas?',
        'Compará mis ventas con mis gastos.',
        '¿Qué productos o categorías rinden mejor?'
      ];
    }
    if (currentSection === 'home' && currentAction === 'historial') {
      return ['Resumime las últimas ventas.', '¿Qué días vendo más?', 'Detectá cambios importantes en mis ventas.'];
    }
    return SUGGESTIONS_BY_SECTION[currentSection] || SUGGESTIONS_BY_SECTION.home;
  }
  function csrf() { return document.querySelector('#chatForm input[name="__RequestVerificationToken"]').value; }
  async function api(url, method, data) {
    var headers = { 'RequestVerificationToken': csrf() };
    if (data !== undefined) headers['Content-Type'] = 'application/json';
    var response = await fetch(url, {
      method: method || 'GET',
      headers: headers,
      body: data === undefined ? undefined : JSON.stringify(data)
    });
    var result = await response.json().catch(function () { return {}; });
    if (!response.ok) throw new Error(result.error || result.message || 'No se pudo completar la operación.');
    return result;
  }

  function showDialog(options) {
    return new Promise(function (resolve) {
      dialogTitle.textContent = options.title || 'Confirmar';
      dialogText.textContent = options.text || '';
      dialogText.hidden = !options.text;
      dialogInput.hidden = !options.input;
      dialogInput.value = options.value || '';
      dialogInput.placeholder = options.placeholder || '';
      dialogConfirm.textContent = options.confirmLabel || 'Confirmar';
      dialogConfirm.classList.toggle('danger', !!options.danger);
      dialogBackdrop.hidden = false;

      function finish(value) {
        dialogBackdrop.hidden = true;
        dialogConfirm.classList.remove('danger');
        dialogConfirm.removeEventListener('click', confirm);
        dialogCancel.removeEventListener('click', cancel);
        dialogBackdrop.removeEventListener('click', outside);
        document.removeEventListener('keydown', keyboard);
        resolve(value);
      }
      function confirm() {
        var value = options.input ? dialogInput.value.trim() : true;
        if (options.input && !value) { dialogInput.focus(); return; }
        finish(value);
      }
      function cancel() { finish(null); }
      function outside(event) { if (event.target === dialogBackdrop) cancel(); }
      function keyboard(event) {
        if (event.key === 'Escape') { event.stopPropagation(); cancel(); }
        if (event.key === 'Enter' && (options.input || event.target !== dialogCancel)) { event.preventDefault(); confirm(); }
      }
      dialogConfirm.addEventListener('click', confirm);
      dialogCancel.addEventListener('click', cancel);
      dialogBackdrop.addEventListener('click', outside);
      document.addEventListener('keydown', keyboard);
      setTimeout(function () { (options.input ? dialogInput : dialogConfirm).focus(); if (options.input) dialogInput.select(); }, 0);
    });
  }


  // Estado de la conversación actual (en memoria: se conserva al cerrar y abrir el panel,
  // y se pierde al recargar la página).
  var turns = [];            // intercambios completos: [{role, text}, ...]
  var lastStockComparisonChart = null;
  var hasSentMessage = false;
  var busy = false;
  var sessionId = 0;         // cambia con "Nuevo chat" para descartar respuestas viejas
  var pendingRequest = null; // última consulta completa, incluida la imagen, para reintentar
  var selectedImage = null;
  var errorRow = null;
  var suggestionsEl = null;

  // ---------- Utilidades de DOM (siempre texto plano, nunca HTML) ----------

  function createEl(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function svgEl(tag, attrs) {
    var node = document.createElementNS(SVG_NS, tag);
    Object.keys(attrs || {}).forEach(function (k) { node.setAttribute(k, attrs[k]); });
    return node;
  }

  function avatar() {
    var img = createEl('img', 'msg-avatar');
    img.src = AVATAR_SRC;
    img.alt = '';
    return img;
  }

  function scrollToEnd() {
    messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  // ---------- Mensajes ----------

  var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Formato mínimo y seguro: permite negritas de Claude sin insertar HTML.
  function appendFormattedText(container, text) {
    String(text || '').split(/(\*\*[^*\n]+\*\*)/g).forEach(function (part) {
      if (!part) return;
      if (part.length >= 4 && part.slice(0, 2) === '**' && part.slice(-2) === '**') {
        container.appendChild(createEl('strong', null, part.slice(2, -2)));
      } else {
        container.appendChild(document.createTextNode(part));
      }
    });
  }

  function copyText(text, button) {
    var value = String(text || '');
    var done = function () {
      button.textContent = 'Copiado';
      button.classList.add('copied');
      setTimeout(function () {
        button.textContent = 'Copiar';
        button.classList.remove('copied');
      }, 1200);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(value).then(done).catch(function () {});
      return;
    }
    var helper = document.createElement('textarea');
    helper.value = value;
    helper.style.position = 'fixed';
    helper.style.opacity = '0';
    document.body.appendChild(helper);
    helper.select();
    try { document.execCommand('copy'); done(); } catch (_) {}
    helper.remove();
  }

  function addCopyButton(bubble, text) {
    var copy = createEl('button', 'copy-message', 'Copiar');
    copy.type = 'button';
    copy.setAttribute('aria-label', 'Copiar respuesta');
    copy.addEventListener('click', function (event) {
      event.stopPropagation();
      copyText(text, copy);
    });
    bubble.classList.add('has-copy');
    bubble.appendChild(copy);
  }

  function addMessage(role, text) {
    var row = createEl('div', 'msg-row ' + role);
    if (role === 'assistant') row.appendChild(avatar());
    var bubble = createEl('div', 'msg ' + role);
    if (role === 'assistant') {
      var content = createEl('div', 'msg-content');
      appendFormattedText(content, text);
      bubble.appendChild(content);
      addCopyButton(bubble, text);
    }
    else bubble.textContent = text;
    row.appendChild(bubble);
    messagesEl.appendChild(row);
    scrollToEnd();
    return row;
  }

  function addAssistantMessageProgressively(text) {
    if (reduceMotion) {
      addMessage('assistant', text);
      return Promise.resolve();
    }

    var row = createEl('div', 'msg-row assistant');
    row.appendChild(avatar());
    var bubble = createEl('div', 'msg assistant is-writing');
    var content = createEl('div', 'msg-content');
    var cursor = createEl('span', 'typing-cursor');
    bubble.append(content, cursor);
    row.appendChild(bubble);
    messagesEl.appendChild(row);
    scrollToEnd();

    var visibleText = String(text || '').replace(/\*\*/g, '');
    var words = visibleText.match(/\S+\s*/g) || [visibleText];
    var wordsPerStep = Math.max(1, Math.ceil(words.length / 45));
    var steps = Math.max(1, Math.ceil(words.length / wordsPerStep));
    var totalDuration = Math.min(1400, Math.max(500, words.length * 34));
    var interval = Math.max(18, Math.round(totalDuration / steps));

    return new Promise(function (resolve) {
      var index = 0;
      var finished = false;
      var timer = null;

      function finish() {
        if (finished) return;
        finished = true;
        if (timer) clearTimeout(timer);
        bubble.removeEventListener('click', finish);
        content.textContent = '';
        appendFormattedText(content, text);
        cursor.remove();
        bubble.classList.remove('is-writing');
        addCopyButton(bubble, text);
        scrollToEnd();
        resolve();
      }

      function writeNext() {
        if (finished) return;
        var end = Math.min(words.length, index + wordsPerStep);
        content.appendChild(document.createTextNode(words.slice(index, end).join('')));
        index = end;
        scrollToEnd();
        if (index >= words.length) { finish(); return; }
        timer = setTimeout(writeNext, interval);
      }

      bubble.addEventListener('click', finish);
      writeNext();
    });
  }

  function addUserMessage(text, image) {
    var row = createEl('div', 'msg-row user');
    var bubble = createEl('div', 'msg user');
    if (image) {
      var preview = createEl('img', 'msg-image');
      preview.src = image.previewUrl;
      preview.alt = image.fileName || 'Imagen adjunta';
      bubble.appendChild(preview);
    }
    if (text) bubble.appendChild(createEl('div', 'msg-text', text));
    row.appendChild(bubble);
    messagesEl.appendChild(row);
    scrollToEnd();
    return row;
  }

  function showTyping() {
    var row = createEl('div', 'msg-row assistant');
    row.appendChild(avatar());
    var bubble = createEl('div', 'msg assistant typing');
    for (var i = 0; i < 3; i++) bubble.appendChild(createEl('span'));
    row.appendChild(bubble);
    messagesEl.appendChild(row);
    scrollToEnd();
    return row;
  }

  function removeSuggestions() {
    if (suggestionsEl) {
      suggestionsEl.remove();
      suggestionsEl = null;
    }
  }

  // Las sugerencias aparecen solo al empezar un chat, y desaparecen al escribir o al enviar.
  function refreshSuggestions() {
    var shouldShow = !hasSentMessage && !busy && input.value.trim() === '';
    if (!shouldShow) {
      removeSuggestions();
      return;
    }
    if (suggestionsEl) return;

    suggestionsEl = createEl('div', 'suggestions');
    suggestionsForPage().forEach(function (text) {
      var btn = createEl('button', 'suggestion', text);
      btn.type = 'button';
      btn.addEventListener('click', function () {
        sendMessage(text, false);
      });
      suggestionsEl.appendChild(btn);
    });
    messagesEl.appendChild(suggestionsEl);
    scrollToEnd();
  }

  function removeErrorRow() {
    if (errorRow) {
      errorRow.remove();
      errorRow = null;
    }
  }

  function showError(message) {
    errorRow = createEl('div', 'msg-row error');
    var box = createEl('div', 'msg error', message);
    var retry = createEl('button', 'retry-button', 'Reintentar');
    retry.type = 'button';
    retry.addEventListener('click', function () {
      if (pendingRequest !== null) sendMessage(pendingRequest.text, true, pendingRequest.image);
    });
    box.appendChild(retry);
    errorRow.appendChild(box);
    messagesEl.appendChild(errorRow);
    scrollToEnd();
  }

  // ---------- Tablas y gráficos ----------

  function renderBlocks(tables, charts) {
    var hasCharts = charts && charts.length > 0;
    var hasTables = tables && tables.length > 0;
    lastStockComparisonChart = (charts || []).find(isStockComparisonChart) || null;
    if (!hasCharts && !hasTables) return;

    var wrap = createEl('div', 'blocks');
    (charts || []).forEach(function (chart) { wrap.appendChild(renderChart(chart)); });
    (tables || []).forEach(function (table) { wrap.appendChild(renderTable(table)); });
    messagesEl.appendChild(wrap);
    scrollToEnd();
  }

  function renderActions(actions) {
    if (!Array.isArray(actions) || !actions.length) return;
    var wrap = createEl('div', 'chat-actions');
    actions.forEach(function (action) {
      if (!action || typeof action.url !== 'string' || action.url.charAt(0) !== '/') return;
      var path = action.url.split('?')[0].replace(/\/$/, '');
      if (ALLOWED_ACTION_PATHS.indexOf(path) === -1) return;
      var link = createEl('a', 'chat-action', action.label || 'Abrir');
      link.href = action.url;
      link.appendChild(document.createTextNode(' →'));
      wrap.appendChild(link);
    });
    if (wrap.childNodes.length) messagesEl.appendChild(wrap);
    scrollToEnd();
  }

  var NUMERIC = /^[-+]?[\d.,%]+$/;

  function renderTable(table) {
    var card = createEl('div', 'block-card');
    if (table.title) card.appendChild(createEl('div', 'block-title', table.title));

    var scroll = createEl('div', 'table-scroll');
    var tableEl = createEl('table', 'data-table');

    var thead = createEl('thead');
    var headRow = createEl('tr');
    (table.columns || []).forEach(function (col) {
      headRow.appendChild(createEl('th', null, col));
    });
    thead.appendChild(headRow);
    tableEl.appendChild(thead);

    var tbody = createEl('tbody');
    (table.rows || []).forEach(function (row) {
      var tr = createEl('tr');
      row.forEach(function (value) {
        var td = createEl('td', NUMERIC.test(value) ? 'num' : null, value);
        tr.appendChild(td);
      });
      tbody.appendChild(tr);
    });
    tableEl.appendChild(tbody);

    scroll.appendChild(tableEl);
    card.appendChild(scroll);
    return card;
  }

  function renderChart(chart) {
    if (isStockComparisonChart(chart)) return renderStockColumnChart(chart);

    var card = createEl('div', 'block-card');
    if (chart.title) card.appendChild(createEl('div', 'block-title', chart.title));

    var layout = chart.type === 'pie' ? null : buildCategoryLayout(chart.labels);
    var W = chart.type === 'pie' ? 320 : Math.max(320, 54 + layout.totalWidth);
    var H = chart.type === 'pie' ? 200 : 184 + Math.max(0, layout.maxLines - 1) * 12;
    var svg = svgEl('svg', {
      viewBox: '0 0 ' + W + ' ' + H,
      role: 'img',
      'class': 'chart-svg',
      'aria-label': chart.title || 'Gráfico'
    });
    svg.style.width = W + 'px';
    svg.style.minWidth = '100%';

    if (chart.type === 'pie') drawPie(svg, chart, W, H);
    else drawCartesian(svg, chart, W, H, layout);

    var scroll = createEl('div', 'chart-scroll');
    scroll.appendChild(svg);
    card.appendChild(scroll);
    card.appendChild(renderLegend(chart));
    return card;
  }

  function normalizedChartText(value) {
    return String(value || '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase();
  }

  function isStockComparisonChart(chart) {
    if (!chart || chart.type !== 'bar' || !Array.isArray(chart.series) || chart.series.length < 2) return false;
    var names = chart.series.map(function (serie) { return normalizedChartText(serie.name); });
    var hasMinimum = names.some(function (name) { return name.indexOf('minimo') !== -1; });
    var hasCurrent = names.some(function (name) {
      return name.indexOf('actual') !== -1 || name.indexOf('disponible') !== -1;
    });
    return hasMinimum && hasCurrent;
  }

  function stockSeries(chart) {
    var currentIndex = chart.series.findIndex(function (serie) {
      var name = normalizedChartText(serie.name);
      return name.indexOf('actual') !== -1 || name.indexOf('disponible') !== -1;
    });
    var minimumIndex = chart.series.findIndex(function (serie) {
      return normalizedChartText(serie.name).indexOf('minimo') !== -1;
    });
    return { current: chart.series[currentIndex], minimum: chart.series[minimumIndex] };
  }

  function renderStockColumnChart(chart) {
    var card = createEl('div', 'block-card stock-column-chart');
    if (chart.title) card.appendChild(createEl('div', 'block-title', chart.title));

    var series = stockSeries(chart);
    var values = series.current.values.concat(series.minimum.values).map(Number).filter(Number.isFinite);
    var maxValue = Math.max.apply(null, values.concat([1]));
    var step = Math.max(1, Math.ceil(maxValue / 4));
    var scaleMax = step * 4;
    var categoryWidth = 112;
    var W = Math.max(350, 46 + chart.labels.length * categoryWidth + 12);
    var H = 230;
    var pad = { l: 38, r: 12, t: 16, b: 48 };
    var plotHeight = H - pad.t - pad.b;
    var svg = svgEl('svg', {
      viewBox: '0 0 ' + W + ' ' + H,
      role: 'img',
      'class': 'chart-svg stock-column-svg',
      'aria-label': chart.title || 'Comparación de stock actual y mínimo'
    });
    svg.style.width = W + 'px';
    svg.style.minWidth = '100%';

    function yOf(value) { return pad.t + plotHeight - (Math.max(0, Number(value) || 0) / scaleMax) * plotHeight; }

    for (var tick = 0; tick <= 4; tick++) {
      var tickValue = step * tick;
      var y = yOf(tickValue);
      svg.appendChild(svgEl('line', { x1:pad.l, x2:W-pad.r, y1:y, y2:y, 'class':'axis stock-grid-line' }));
      var tickLabel = svgEl('text', { x:pad.l-5, y:y+3, 'text-anchor':'end', 'class':'stock-axis-label' });
      tickLabel.textContent = formatNumber(tickValue);
      svg.appendChild(tickLabel);
    }

    chart.labels.forEach(function (label, index) {
      var centerX = pad.l + (index + 0.5) * ((W-pad.l-pad.r) / chart.labels.length);
      var labelLines = wrapLabel(label, categoryWidth-12).slice(0, 2);
      var labelText = svgEl('text', { x:centerX, y:H-pad.b+15, 'text-anchor':'middle', 'class':'stock-category-label' });
      labelLines.forEach(function (line, lineIndex) {
        var tspan = svgEl('tspan', { x:centerX, dy:lineIndex === 0 ? 0 : 11 });
        tspan.textContent = line;
        labelText.appendChild(tspan);
      });
      var labelTip = svgEl('title'); labelTip.textContent = label; labelText.appendChild(labelTip);
      svg.appendChild(labelText);

      var actual = Number(series.current.values[index]) || 0;
      var minimo = Number(series.minimum.values[index]) || 0;
      var actualColor = actual <= minimo ? '#e24b5b' : '#1769e8';
      drawStockColumn(svg, centerX-24, yOf(actual), yOf(0)-yOf(actual), actual, actualColor);
      drawStockColumn(svg, centerX+3, yOf(minimo), yOf(0)-yOf(minimo), minimo, '#f0a11a');
    });

    var legend = createEl('div', 'legend stock-chart-legend');
    legend.appendChild(legendItem('#1769e8', 'Stock actual'));
    legend.appendChild(legendItem('#f0a11a', 'Stock mínimo'));
    legend.appendChild(legendItem('#e24b5b', 'Necesita reposición'));
    card.appendChild(legend);
    var scroll = createEl('div', 'chart-scroll');
    scroll.appendChild(svg);
    card.appendChild(scroll);
    return card;
  }

  function drawStockColumn(svg, x, y, height, value, color) {
    svg.appendChild(svgEl('rect', { x:x, y:y, width:21, height:Math.max(height, value > 0 ? 2 : 0), rx:4, fill:color }));
    var valueText = svgEl('text', { x:x+10.5, y:Math.max(10, y-5), 'text-anchor':'middle', 'class':'stock-value-label' });
    valueText.textContent = formatNumber(value);
    svg.appendChild(valueText);
  }

  function renderStockComparison(chart) {
    var card = createEl('div', 'block-card stock-comparison');
    if (chart.title) card.appendChild(createEl('div', 'block-title', chart.title));

    var series = stockSeries(chart);
    var current = series.current;
    var minimum = series.minimum;
    var values = current.values.concat(minimum.values).filter(function (value) {
      return value !== null && Number.isFinite(Number(value));
    });
    var globalMax = Math.max.apply(null, values.concat([1]));
    var scaleMax = globalMax * 1.08;
    var list = createEl('div', 'stock-comparison-list');

    chart.labels.forEach(function (label, index) {
      var actual = Number(current.values[index]) || 0;
      var minimo = Number(minimum.values[index]) || 0;
      var state = actual <= 0 ? 'empty' : actual <= minimo ? 'low' : 'ok';
      var stateText = state === 'empty' ? 'Agotado' : state === 'low' ? 'Stock bajo' : 'Disponible';
      var row = createEl('div', 'stock-comparison-row is-' + state);

      var heading = createEl('div', 'stock-comparison-heading');
      var product = createEl('strong', 'stock-product-name', label);
      product.title = label;
      heading.appendChild(product);
      heading.appendChild(createEl('span', 'stock-status', stateText));
      row.appendChild(heading);

      var metrics = createEl('div', 'stock-metrics');
      metrics.setAttribute('role', 'img');
      metrics.setAttribute('aria-label', label + ': stock actual ' + formatNumber(actual) + '; stock mínimo ' + formatNumber(minimo));

      var actualMetric = createEl('div', 'stock-metric stock-metric-current');
      actualMetric.appendChild(createEl('span', 'stock-metric-label', 'Stock actual'));
      var actualTrack = createEl('span', 'stock-track');
      var actualFill = createEl('span', 'stock-fill');
      actualFill.style.width = Math.max(actual > 0 ? 2 : 0, Math.min(100, (actual / scaleMax) * 100)) + '%';
      actualTrack.appendChild(actualFill);
      actualMetric.appendChild(actualTrack);
      actualMetric.appendChild(createEl('b', 'stock-metric-value', formatNumber(actual)));

      var minimumMetric = createEl('div', 'stock-metric stock-metric-minimum');
      minimumMetric.appendChild(createEl('span', 'stock-metric-label', 'Stock mínimo'));
      var minimumTrack = createEl('span', 'stock-track');
      var minimumFill = createEl('span', 'stock-fill');
      minimumFill.style.width = Math.max(minimo > 0 ? 2 : 0, Math.min(100, (minimo / scaleMax) * 100)) + '%';
      minimumTrack.appendChild(minimumFill);
      minimumMetric.appendChild(minimumTrack);
      minimumMetric.appendChild(createEl('b', 'stock-metric-value', formatNumber(minimo)));

      metrics.appendChild(actualMetric);
      metrics.appendChild(minimumMetric);
      row.appendChild(metrics);
      list.appendChild(row);
    });

    card.appendChild(list);
    return card;
  }

  function formatNumber(value) {
    return Number(value).toLocaleString('es-AR', { maximumFractionDigits: 1 });
  }

  var labelMeasureContext = null;

  function measureLabel(text) {
    if (!labelMeasureContext) {
      var canvas = document.createElement('canvas');
      labelMeasureContext = canvas.getContext('2d');
      if (labelMeasureContext) labelMeasureContext.font = '10px Arial, sans-serif';
    }
    return labelMeasureContext ? labelMeasureContext.measureText(String(text)).width : String(text).length * 6;
  }

  function wrapLabel(label, maxWidth) {
    var words = String(label).trim().split(/\s+/).filter(Boolean);
    if (!words.length) return [''];
    var lines = [];
    var line = '';

    words.forEach(function (word) {
      var candidate = line ? line + ' ' + word : word;
      if (!line || measureLabel(candidate) <= maxWidth) {
        line = candidate;
        return;
      }
      lines.push(line);
      line = word;
    });
    if (line) lines.push(line);
    return lines;
  }

  function buildCategoryLayout(labels) {
    var total = 0;
    var maxLines = 1;
    var entries = labels.map(function (label) {
      var fullWidth = measureLabel(label) + 24;
      var width = Math.max(76, Math.min(fullWidth, 190));
      var lines = wrapLabel(label, width - 16);
      maxLines = Math.max(maxLines, lines.length);
      var entry = { label: label, width: width, center: total + width / 2, lines: lines };
      total += width;
      return entry;
    });
    return { entries: entries, totalWidth: total, maxLines: maxLines };
  }

  function drawCartesian(svg, chart, W, H, layout) {
    var pad = { l: 42, r: 12, t: 10, b: 22 + layout.maxLines * 12 };
    var innerH = H - pad.t - pad.b;

    var min = 0;
    var max = 0;
    chart.series.forEach(function (serie) {
      serie.values.forEach(function (v) {
        if (v !== null) {
          if (v > max) max = v;
          if (v < min) min = v;
        }
      });
    });
    if (max === min) max = min + 1;
    var range = max - min;

    function yOf(v) {
      return pad.t + innerH - ((v - min) / range) * innerH;
    }

    // Líneas de referencia y valores del eje Y
    for (var g = 0; g <= 4; g++) {
      var value = min + (range * g) / 4;
      var y = yOf(value);
      svg.appendChild(svgEl('line', { x1: pad.l, x2: W - pad.r, y1: y, y2: y, 'class': 'axis' }));
      var yLabel = svgEl('text', { x: pad.l - 4, y: y + 3, 'text-anchor': 'end' });
      yLabel.textContent = formatNumber(value);
      svg.appendChild(yLabel);
    }

    layout.entries.forEach(function (entry) {
      var x = pad.l + entry.center;
      var firstY = H - 9 - (entry.lines.length - 1) * 11;
      var text = svgEl('text', { x: x, y: firstY, 'text-anchor': 'middle', 'class': 'category-label' });
      entry.lines.forEach(function (line, lineIndex) {
        var tspan = svgEl('tspan', { x: x, dy: lineIndex === 0 ? 0 : 11 });
        tspan.textContent = line;
        text.appendChild(tspan);
      });
      var tip = svgEl('title');
      tip.textContent = entry.label;
      text.appendChild(tip);
      svg.appendChild(text);
    });

    if (chart.type === 'bar') {
      var zeroY = yOf(0);

      chart.series.forEach(function (serie, si) {
        serie.values.forEach(function (v, i) {
          if (v === null) return;
          var entry = layout.entries[i];
          var groupWidth = entry.width * 0.7;
          var barWidth = groupWidth / chart.series.length;
          var x = pad.l + entry.center - groupWidth / 2 + barWidth * si;
          var top = Math.min(yOf(v), zeroY);
          var height = Math.abs(zeroY - yOf(v));
          var rect = svgEl('rect', {
            x: x, y: top, width: Math.max(barWidth - 2, 1), height: Math.max(height, 0.5),
            rx: 2, fill: COLORS[si % COLORS.length]
          });
          var tip = svgEl('title');
          tip.textContent = chart.labels[i] + ' · ' + serie.name + ': ' + formatNumber(v);
          rect.appendChild(tip);
          svg.appendChild(rect);
        });
      });
    } else if (chart.type === 'line') {
      chart.series.forEach(function (serie, si) {
        var color = COLORS[si % COLORS.length];
        var points = [];
        serie.values.forEach(function (v, i) {
          if (v === null) return;
          points.push((pad.l + layout.entries[i].center).toFixed(1) + ',' + yOf(v).toFixed(1));
        });
        if (points.length > 1) {
          svg.appendChild(svgEl('polyline', {
            points: points.join(' '), fill: 'none', stroke: color, 'stroke-width': 2.2,
            'stroke-linejoin': 'round', 'stroke-linecap': 'round'
          }));
        }
        serie.values.forEach(function (v, i) {
          if (v === null) return;
          var dot = svgEl('circle', {
            cx: pad.l + layout.entries[i].center, cy: yOf(v), r: 3.2, fill: color
          });
          var tip = svgEl('title');
          tip.textContent = chart.labels[i] + ' · ' + serie.name + ': ' + formatNumber(v);
          dot.appendChild(tip);
          svg.appendChild(dot);
        });
      });
    }
  }

  function drawPie(svg, chart, W, H) {
    var serie = chart.series[0];
    var total = 0;
    serie.values.forEach(function (v) { if (v !== null && v > 0) total += v; });

    if (total <= 0) {
      var empty = svgEl('text', { x: W / 2, y: H / 2, 'text-anchor': 'middle' });
      empty.textContent = 'Sin datos para mostrar';
      svg.appendChild(empty);
      return;
    }

    var cx = W / 2;
    var cy = H / 2;
    var r = Math.min(W, H) / 2 - 12;
    var angle = -Math.PI / 2;

    chart.labels.forEach(function (label, i) {
      var v = serie.values[i];
      if (v === null || v <= 0) return;

      var fraction = v / total;
      var color = COLORS[i % COLORS.length];
      var slice;

      if (fraction >= 0.9999) {
        slice = svgEl('circle', { cx: cx, cy: cy, r: r, fill: color });
      } else {
        var next = angle + fraction * Math.PI * 2;
        var x0 = cx + r * Math.cos(angle);
        var y0 = cy + r * Math.sin(angle);
        var x1 = cx + r * Math.cos(next);
        var y1 = cy + r * Math.sin(next);
        var largeArc = fraction > 0.5 ? 1 : 0;
        slice = svgEl('path', {
          d: 'M ' + cx + ' ' + cy + ' L ' + x0.toFixed(2) + ' ' + y0.toFixed(2) +
             ' A ' + r + ' ' + r + ' 0 ' + largeArc + ' 1 ' + x1.toFixed(2) + ' ' + y1.toFixed(2) + ' Z',
          fill: color, stroke: '#fff', 'stroke-width': 1.5
        });
        angle = next;
      }

      var tip = svgEl('title');
      tip.textContent = label + ': ' + formatNumber(v) + ' (' + (fraction * 100).toFixed(1) + '%)';
      slice.appendChild(tip);
      svg.appendChild(slice);
    });
  }

  function renderLegend(chart) {
    var legend = createEl('div', 'legend');
    if (chart.type === 'pie') {
      chart.labels.forEach(function (label, i) {
        legend.appendChild(legendItem(COLORS[i % COLORS.length], label));
      });
    } else {
      chart.series.forEach(function (serie, si) {
        legend.appendChild(legendItem(COLORS[si % COLORS.length], serie.name));
      });
    }
    return legend;
  }

  function legendItem(color, text) {
    var item = createEl('span', 'legend-item');
    var swatch = createEl('span', 'swatch');
    swatch.style.background = color;
    item.appendChild(swatch);
    item.appendChild(createEl('span', null, text));
    return item;
  }

  // ---------- Envío de consultas ----------

  function clearAttachment() {
    selectedImage = null;
    imageInput.value = '';
    attachmentPreview.hidden = true;
    attachmentThumbnail.removeAttribute('src');
    updateSendState();
  }

  function formatBytes(bytes) {
    return bytes < 1024 * 1024 ? Math.round(bytes / 1024) + ' KB' : (bytes / 1024 / 1024).toFixed(1) + ' MB';
  }

  function readFileAsDataUrl(file) {
    return new Promise(function (resolve, reject) {
      var reader = new FileReader();
      reader.onload = function () { resolve(String(reader.result || '')); };
      reader.onerror = function () { reject(new Error('No se pudo leer la imagen.')); };
      reader.readAsDataURL(file);
    });
  }

  function loadBrowserImage(url) {
    return new Promise(function (resolve, reject) {
      var img = new Image();
      img.onload = function () { resolve(img); };
      img.onerror = function () { reject(new Error('El archivo no contiene una imagen válida.')); };
      img.src = url;
    });
  }

  async function prepareImage(file) {
    var allowed = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowed.includes(file.type)) throw new Error('Solo podés adjuntar imágenes JPG, PNG o WebP.');
    if (file.size > 15 * 1024 * 1024) throw new Error('La imagen original supera el límite de 15 MB.');
    var originalUrl = await readFileAsDataUrl(file);
    var img = await loadBrowserImage(originalUrl);
    var scale = Math.min(1, 1600 / Math.max(img.naturalWidth, img.naturalHeight));
    var width = Math.max(1, Math.round(img.naturalWidth * scale));
    var height = Math.max(1, Math.round(img.naturalHeight * scale));
    var canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, width, height);
    ctx.drawImage(img, 0, 0, width, height);
    var dataUrl = canvas.toDataURL('image/jpeg', 0.86);
    var base64 = dataUrl.split(',')[1] || '';
    var bytes = Math.floor(base64.length * 3 / 4);
    if (bytes > 4 * 1024 * 1024) throw new Error('No se pudo reducir la imagen por debajo de 4 MB.');
    return { mediaType:'image/jpeg', base64Data:base64, fileName:file.name, width:width, height:height, previewUrl:dataUrl, bytes:bytes };
  }

  attachBtn.addEventListener('click', function () { if (!busy) imageInput.click(); });
  document.getElementById('removeAttachment').addEventListener('click', clearAttachment);
  imageInput.addEventListener('change', async function () {
    var file = imageInput.files && imageInput.files[0];
    if (!file) return;
    attachBtn.disabled = true;
    try {
      selectedImage = await prepareImage(file);
      attachmentThumbnail.src = selectedImage.previewUrl;
      attachmentName.textContent = selectedImage.fileName;
      attachmentMeta.textContent = selectedImage.width + ' × ' + selectedImage.height + ' · ' + formatBytes(selectedImage.bytes);
      attachmentPreview.hidden = false;
      updateSendState();
    } catch (error) {
      clearAttachment();
      addMessage('assistant', error.message || 'No se pudo preparar la imagen.');
    } finally { attachBtn.disabled = false; }
  });

  function updateSendState() {
    sendBtn.disabled = busy || (input.value.trim() === '' && !selectedImage);
  }

  function statusForQuestion(text, image) {
    if (image) return 'Analizando imagen…';
    var value = String(text || '').toLowerCase();
    if (/stock|inventario|reponer|agotad/.test(value)) return 'Consultando stock…';
    if (/venta|vend[ií]|ganancia|margen|facturaci[oó]n/.test(value)) return 'Analizando ventas…';
    if (/producto|sku|precio|categor[ií]a/.test(value)) return 'Revisando productos…';
    return STATUS_BUSY;
  }

  function setBusy(value, busyLabel) {
    busy = value;
    input.disabled = value;
    attachBtn.disabled = value;
    statusEl.textContent = value ? (busyLabel || STATUS_BUSY) : STATUS_IDLE;
    updateSendState();
  }

  function autoResize() {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 120) + 'px';
  }

  function sendMessage(rawText, isRetry, imageOverride) {
    var text = String(rawText || '').trim();
    var image = imageOverride || selectedImage;
    if (busy || (!text && !image)) return;
    var intentText = normalizedChartText(text);
    var asksForSimplerChart = /no entiendo|no se entiende|mostra(lo|me) mas simple|explica(me)? el grafico|simplifica/.test(intentText);
    if (!image && !isRetry && asksForSimplerChart && lastStockComparisonChart) {
      addMessage('user', text);
      addMessage('assistant', 'Te lo muestro de una forma más directa, separando el stock actual del mínimo recomendado.');
      var simpleWrap = createEl('div', 'blocks');
      simpleWrap.appendChild(renderStockComparison(lastStockComparisonChart));
      messagesEl.appendChild(simpleWrap);
      scrollToEnd();
      return;
    }
    var normalizedQuestion = text.replace(/^[¿¡\s]+/, '');
    var asksHow = /^(c[oó]mo|d[oó]nde|puedo|se puede|qu[eé] pasa|qu[eé] necesito|cu[aá]l es)/i.test(normalizedQuestion);
    var skuMatch = text.match(/sku\s*[:#-]?\s*([a-z0-9._-]+)/i);
    var initialSku = skuMatch ? skuMatch[1] : '';
    var requestsCreation = /\b(crea|crear|creame|creo|agrega|agregar|agregame|carga|cargar|registra|registrar|alta|nuevo)\b/.test(intentText) &&
      /\b(producto|articulo|item)\b/.test(intentText) && !/\b(no|sin)\s+(crear|agregar|cargar|registrar)\b/.test(intentText);
    if (!image && !isRetry && requestsCreation) {
      addMessage('user', text);
      addMessage('assistant', 'Sí, podés crearlo desde acá. Completá los datos básicos y revisalos antes de guardarlo.');
      manageProduct('crear', '', initialSku);
      return;
    }
    if (!image && !isRetry && !asksHow && /\b(elimin|borr|sac[aá]|quit[aá])\b/i.test(text) && /producto|sku|cat[aá]logo/i.test(text)) {
      addMessage('user',text);
      addMessage('assistant','Abrí la eliminación segura. Identificá el producto por SKU y revisá las dos confirmaciones antes de eliminarlo.');
      manageProduct('eliminar', '', initialSku);
      return;
    }
    if (!image && !isRetry && !asksHow && /\b(cre[aá]|agreg[aá]|nuevo producto|modific[aá]|cambi[aá]|sub[ií]|baj[aá]|aument[aá])\b/i.test(text) && /producto|sku|precio|stock/i.test(text)) {
      addMessage('user',text);
      addMessage('assistant','Abrí el formulario para identificar el producto por SKU y preparar el cambio. Antes de guardar te pediré dos confirmaciones.');
      var operation = /\b(cre[aá]|agreg[aá]|nuevo producto)\b/i.test(text) ? 'crear' : (/stock/i.test(text) ? 'stock' : 'precio');
      manageProduct(operation, '', initialSku); return;
    }
    if (text.length > MAX_LENGTH) text = text.slice(0, MAX_LENGTH);

    if (!currentChatId) { showError('Primero creá o elegí un chat.'); return; }
    var session = sessionId;
    removeErrorRow();
    removeSuggestions();
    pendingRequest = { text:text, image:image };

    if (!isRetry) {
      addUserMessage(text, image);
      hasSentMessage = true;
      clearAttachment();
    }

    setBusy(true, statusForQuestion(text, image));
    var typingRow = showTyping();
    var requestStartedAt = Date.now();

    fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('#chatForm input[name="__RequestVerificationToken"]').value },
      body: JSON.stringify({
        message: text,
        chatId: currentChatId,
        image: image ? { mediaType:image.mediaType, base64Data:image.base64Data, fileName:image.fileName, width:image.width, height:image.height } : null,
        section: currentSection,
        action: currentAction
      })
    })
      .then(function (res) {
        return res.json()
          .catch(function () { return null; })
          .then(function (data) { return { status: res.status, data: data }; });
      })
      .then(function (r) {
        if (r.data && r.data.ok === true && typeof r.data.reply === 'string') {
          return {
            ok: true,
            reply: r.data.reply,
            tables: Array.isArray(r.data.tables) ? r.data.tables : [],
            charts: Array.isArray(r.data.charts) ? r.data.charts : [],
            actions: Array.isArray(r.data.actions) ? r.data.actions : []
          };
        }
        var serverMessage = r.data && r.data.error && r.data.error.message;
        return {
          ok: false,
          message: serverMessage || 'Quantitary IA no pudo responder en este momento. Intentá de nuevo.'
        };
      })
      .catch(function () {
        return { ok: false, message: CONNECTION_ERROR };
      })
      .then(function (result) {
        var remaining = MIN_TYPING_MS - (Date.now() - requestStartedAt);
        if (remaining <= 0) return result;
        return new Promise(function (resolve) {
          setTimeout(function () { resolve(result); }, remaining);
        });
      })
      .then(async function (result) {
        // Si se inició un chat nuevo mientras esperábamos, descartamos esta respuesta.
        if (session !== sessionId) return;

        typingRow.remove();
        if (result.ok) {
          // El servidor conserva texto, tablas, gráficos y acciones. La imagen no se vuelve a enviar ni facturar.
          turns.push({ role: 'user', text: text || '[Imagen adjunta]' }, { role: 'assistant', text: result.reply });
          pendingRequest = null;
          if (result.reply) {
            statusEl.textContent = 'Escribiendo…';
            await addAssistantMessageProgressively(result.reply);
          }
          renderBlocks(result.tables, result.charts);
          renderActions(result.actions);
          loadHistory().catch(function () {});
        } else {
          showError(result.message);
        }

        setBusy(false);
        refreshSuggestions();
        if (panel.classList.contains('visible')) input.focus();
      });
  }

  function submitFromInput() {
    if (busy) return;
    var text = input.value.trim();
    if (!text && !selectedImage) return;
    input.value = '';
    autoResize();
    sendMessage(text, false);
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    submitFromInput();
  });

  input.addEventListener('keydown', function (e) {
    // Enter envía; Shift + Enter agrega un salto de línea.
    if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
      e.preventDefault();
      submitFromInput();
    }
  });

  input.addEventListener('input', function () {
    autoResize();
    updateSendState();
    refreshSuggestions();
  });

  function resetChat() {
    sessionId++;
    turns = [];
    lastStockComparisonChart = null;
    hasSentMessage = false;
    pendingRequest = null;
    clearAttachment();
    errorRow = null;
    suggestionsEl = null;
    busy = false;
    messagesEl.textContent = '';
    addMessage('assistant', GREETING);
    input.value = '';
    autoResize();
    setBusy(false);
    refreshSuggestions();
  }

  async function loadHistory() {
    var data = await api('/api/ia/chats');
    historyList.textContent = '';
    data.forEach(function (item) {
      var line = createEl('div', 'ia-history-entry');
      line.dataset.chatId = item.id;
      var open = createEl('button', 'ia-chat-open', item.titulo || 'Nuevo chat');
      open.type = 'button';
      if (item.id === currentChatId) open.classList.add('active');
      open.addEventListener('click', function () { openChat(item.id).catch(report); });
      var rename = createEl('button', '', '✎');
      rename.type = 'button'; rename.title = 'Renombrar';
      rename.addEventListener('click', async function () {
        var name = await showDialog({ title:'Renombrar conversación', text:'Elegí un nombre corto que te ayude a encontrarla después.', input:true, value:item.titulo || '', confirmLabel:'Guardar' });
        if (!name) return;
        try { await api('/api/ia/chats/' + item.id + '/renombrar', 'POST', { titulo: name.slice(0,120) }); await loadHistory(); }
        catch (err) { report(err); }
      });
      var remove = createEl('button', '', '×');
      remove.type = 'button'; remove.title = 'Eliminar chat';
      remove.addEventListener('click', async function () {
        var confirmed = await showDialog({ title:'Eliminar conversación', text:'Se eliminarán definitivamente este chat y todos sus mensajes.', confirmLabel:'Eliminar', danger:true });
        if (!confirmed) return;
        try {
          await api('/api/ia/chats/' + item.id + '/eliminar', 'POST');
          var remaining = await loadHistory();
          if (item.id === currentChatId) {
            if (remaining.length) await openChat(remaining[0].id);
            else { currentChatId = null; await newChat(); }
          }
        } catch (err) { report(err); }
      });
      line.append(open, rename, remove);
      historyList.appendChild(line);
    });
    if (!data.length) historyList.appendChild(createEl('div', 'ia-history-empty', 'Todavía no hay conversaciones.'));
    filterHistory();
    return data;
  }

  function filterHistory() {
    var term = (historySearch.value || '').trim().toLocaleLowerCase('es');
    historyList.querySelectorAll('.ia-history-entry').forEach(function (entry) {
      entry.hidden = term !== '' && !entry.textContent.toLocaleLowerCase('es').includes(term);
    });
  }

  function report(err) { addMessage('assistant', err.message || 'No se pudo cargar el historial.'); }

  async function openChat(id) {
    var rows = await api('/api/ia/chats/' + id);
    resetChat();
    currentChatId = id;
    historyList.querySelectorAll('.ia-history-entry').forEach(function (entry) {
      var openButton = entry.querySelector('.ia-chat-open');
      if (openButton) openButton.classList.toggle('active', entry.dataset.chatId === String(id));
    });
    messagesEl.textContent = '';
    if (!rows.length) addMessage('assistant', GREETING);
    rows.forEach(function (r) {
      var role = r.role === 'user' ? 'user' : 'assistant';
      addMessage(role, r.text || '');
      if (role === 'assistant') {
        renderBlocks(
          Array.isArray(r.tables) ? r.tables : [],
          Array.isArray(r.charts) ? r.charts : []
        );
        renderActions(Array.isArray(r.actions) ? r.actions : []);
      }
      turns.push({ role: role, text: r.text || '' });
    });
    hasSentMessage = rows.length > 0;
    removeSuggestions();
    refreshSuggestions();
    historyPanel.hidden = !(document.getElementById('quantitary-ia').classList.contains('fullscreen') &&
      !window.matchMedia('(max-width: 540px)').matches);
    input.focus();
  }

  async function newChat() {
    if (currentChatId && !hasSentMessage) {
      resetChat();
      historyPanel.hidden = true;
      return;
    }
    var item = await api('/api/ia/chats', 'POST');
    await openChat(item.id);
    await loadHistory();
  }

  newChatBtn.addEventListener('click', function () { newChat().catch(report); });
  historyNewChatBtn.addEventListener('click', function () { newChat().catch(report); });
  historySearch.addEventListener('input', filterHistory);
  document.getElementById('chatsButton').addEventListener('click', function () {
    historyPanel.hidden = !historyPanel.hidden;
    if (!historyPanel.hidden) loadHistory().catch(report);
  });
  document.getElementById('historyClose').addEventListener('click', function () { historyPanel.hidden = true; });
  panel.addEventListener('qty-open', function () { input.focus(); });
  panel.addEventListener('qty-fullscreen', function (event) {
    historyPanel.hidden = !event.detail.fullscreen;
    if (event.detail.fullscreen) loadHistory().catch(report);
  });

  // El formulario se genera dentro del chat: los cambios se ejecutan solo por el servidor.
  function formRow(parent, title, name, type, value) {
    var label = createEl('label', '', title);
    var field = createEl('input'); field.name = name; field.type = type || 'text';
    if (value !== undefined) field.value = value;
    if (type === 'number') {field.min = '0';field.step = name === 'precio' || name === 'costo' ? '0.01' : '1';}
    label.appendChild(field); parent.appendChild(label);
    return field;
  }
  function formTextArea(parent, title, name) {
    var label = createEl('label', '', title);
    var field = createEl('textarea'); field.name = name; field.rows = 2;
    label.appendChild(field); parent.appendChild(label);
    return field;
  }
  function manageProduct(initialType, initialName, initialSku) {
    removeSuggestions();
    var dedicatedCreate = initialType === 'crear';
    var form = createEl('form', 'ia-product-form');
    form.appendChild(createEl('strong', '', dedicatedCreate ? 'Nuevo producto' : 'Administrar producto'));
    form.appendChild(createEl('p', 'form-intro', dedicatedCreate
      ? 'Completá nombre, SKU y precio. Después vas a poder revisar todo antes de crear el producto.'
      : 'El cambio se valida en el servidor antes de guardarse.'));
    var select = createEl('select');
    [['crear','Crear producto'],['precio','Modificar precio'],['stock','Modificar stock'],['eliminar','Eliminar producto']].forEach(function (pair) {
      var opt = createEl('option', '', pair[1]); opt.value = pair[0]; select.appendChild(opt);
    });
    select.value = initialType || 'crear';
    select.hidden = dedicatedCreate;
    form.appendChild(select);
    var nombre = formRow(form, 'Nombre', 'nombre', 'text', initialName || '');
    var sku = formRow(form, 'SKU exacto', 'sku', 'text', initialSku || '');
    var categoria = formRow(form, 'Categoría (opcional)', 'categoria', 'text');
    var precio = formRow(form, dedicatedCreate ? 'Precio de venta' : 'Precio nuevo / inicial', 'precio', 'number');
    var stock = formRow(form, dedicatedCreate ? 'Stock inicial (opcional)' : 'Stock nuevo / inicial', 'stock', 'number');
    var minimo = formRow(form, 'Stock mínimo (opcional)', 'minimo', 'number');
    var costo = formRow(form, 'Costo (opcional)', 'costo', 'number');
    var proveedor = formRow(form, 'Proveedor (opcional)', 'proveedor', 'text');
    var descripcion = formTextArea(form, 'Descripción (opcional)', 'descripcion');
    nombre.required = sku.required = precio.required = dedicatedCreate;

    var advanced = createEl('div', 'product-advanced-fields');
    [categoria.parentElement, minimo.parentElement, costo.parentElement, proveedor.parentElement, descripcion.parentElement].forEach(function (field) {
      advanced.appendChild(field);
    });
    form.appendChild(advanced);
    var advancedToggle = createEl('button', 'product-advanced-toggle', '＋ Agregar categoría, costo y otros datos');
    advancedToggle.type = 'button';
    advancedToggle.setAttribute('aria-expanded', 'false');
    advancedToggle.addEventListener('click', function () {
      var open = advanced.classList.toggle('visible');
      advancedToggle.setAttribute('aria-expanded', String(open));
      advancedToggle.textContent = open ? '− Ocultar datos opcionales' : '＋ Agregar categoría, costo y otros datos';
    });
    form.insertBefore(advancedToggle, advanced);

    function updateFields() {
      var creating = select.value === 'crear';
      nombre.parentElement.hidden = !creating;
      advancedToggle.hidden = !creating;
      advanced.hidden = !creating;
      precio.parentElement.hidden = select.value === 'stock' || select.value === 'eliminar';
      stock.parentElement.hidden = select.value === 'precio' || select.value === 'eliminar';
    }
    select.addEventListener('change', updateFields); updateFields();
    var actions = createEl('div', 'form-actions');
    var close = createEl('button', 'secondary', 'Cancelar'); close.type='button';
    var button = createEl('button', '', dedicatedCreate ? 'Revisar producto' : 'Revisar operación'); button.type='submit';
    actions.append(close, button); form.appendChild(actions);
    close.addEventListener('click', function () { form.remove(); });
    form.addEventListener('submit', async function (event) {
      event.preventDefault();
      var data = {tipo: select.value, sku: sku.value.trim(), nombre: nombre.value.trim(), categoria: categoria.value.trim(),
        precio: precio.value === '' ? null : Number(precio.value), stock: stock.value === '' ? null : Number(stock.value),
        stockMinimo: minimo.value === '' ? null : Number(minimo.value), costo: costo.value === '' ? null : Number(costo.value),
        proveedor: proveedor.value.trim(), descripcion: descripcion.value.trim()};
      button.disabled = true;
      try {
        var result = await api('/api/ia/productos/preparar', 'POST', data);
        var preview = createEl('div', 'ia-product-form');
        preview.appendChild(createEl('strong','', select.value === 'crear' ? 'Revisá el producto' : 'Confirmación 1 de 2'));
        preview.appendChild(createEl('p','form-intro', select.value === 'crear'
          ? 'Si está todo bien, confirmá para crearlo.'
          : 'Revisá cuidadosamente los datos antes de continuar.'));
        var grid = createEl('div','preview-grid');
        Object.keys(result.resumen || {}).forEach(function (key) {
          var item = createEl('div','preview-item');
          item.appendChild(createEl('small','',key.replace(/([A-Z])/g,' $1')));
          item.appendChild(createEl('strong','',String(result.resumen[key] ?? '—')));
          grid.appendChild(item);
        });
        preview.appendChild(grid);
        var previewActions = createEl('div','form-actions');
        var cancel = createEl('button','secondary','Cancelar'); cancel.type='button';
        var confirm = createEl('button', select.value === 'eliminar' ? 'danger' : '', select.value === 'crear' ? 'Crear producto' : 'Confirmar datos'); confirm.type='button';
        previewActions.append(cancel,confirm);preview.appendChild(previewActions);messagesEl.appendChild(preview);scrollToEnd();
        cancel.addEventListener('click', async function () {await api('/api/ia/productos/cancelar','POST');preview.remove();button.disabled=false;});
        confirm.addEventListener('click', async function () {
          confirm.disabled = true;
          try {
            var stage = await api('/api/ia/productos/confirmar','POST',{token:result.token});
            if (select.value === 'crear') {
              var created = await api('/api/ia/productos/confirmar','POST',{token:result.token});
              addMessage('assistant', created.mensaje + ' Para la próxima, también podés cargarlo desde Stock; ahí es más fácil si querés completar todos los datos.');
              renderActions([{label:'Agregar otro desde Stock',url:'/Stock/Crear'}]);
              preview.remove();
              form.remove();
              return;
            }
            previewActions.remove();
            preview.appendChild(createEl('p','form-intro',stage.mensaje));
            var finalActions = createEl('div','form-actions');
            var finalCancel = createEl('button','secondary','Cancelar');finalCancel.type='button';
            var final = createEl('button',select.value === 'eliminar' ? 'danger' : '', select.value === 'eliminar' ? 'Eliminar definitivamente' : 'Confirmar definitivamente');
            final.type='button';finalActions.append(finalCancel,final);preview.appendChild(finalActions);
            finalCancel.addEventListener('click', async function () {await api('/api/ia/productos/cancelar','POST');preview.remove();button.disabled=false;});
            final.addEventListener('click', async function () {
              final.disabled=true;
              try {var done=await api('/api/ia/productos/confirmar','POST',{token:result.token});addMessage('assistant',done.mensaje);preview.remove();form.remove();}
              catch(e){addMessage('assistant',e.message);final.disabled=false;}
            });
          }catch(e){addMessage('assistant',e.message);confirm.disabled=false;}
        });
      } catch(e){addMessage('assistant',e.message);}finally{button.disabled=false;}
    });
    messagesEl.appendChild(form); scrollToEnd();
  }

  async function loadAlerts() {
    var container = document.getElementById('iaStockDashboard');
    if (!container) return;
    if (window.localStorage.getItem('quantitary.notifyStock') === 'false') {
      container.hidden = true;
      return;
    }
    try {
      var data = await api('/api/ia/alertas-stock');
      container.hidden = false;
      document.getElementById('iaStockDashboardText').textContent = (data.agotados + data.bajos) ?
        data.agotados + ' agotados · ' + data.bajos + ' con stock bajo' : 'No hay productos por debajo del mínimo.';
      document.getElementById('iaStockAnalyze').addEventListener('click', async function () {
        setOpen(true);
        if (!currentChatId) await newChat();
        var sample = data.productos.slice(0, 12).map(function(p){return p.nombre+' (SKU '+p.sku+'): '+p.stock+' unidades, mínimo '+p.minimo;});
        sendMessage('Analizá estas alertas de stock reales y recomendá prioridades sin modificar datos: '+sample.join('; '),false);
      });
    } catch(e) { container.hidden = true; }
  }
  loadAlerts();
  loadHistory().then(async function (data) {
    if (data.length) await openChat(data[0].id);
    else await newChat();
  }).catch(report);
})();
