
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
      '¿Qué productos llevan más tiempo sin movimiento?'
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
  var AVATAR_SRC = '/ia/assets/quantitary-ia-normal.svg';
  var STATUS_IDLE = 'Tu asistente inteligente';
  var STATUS_BUSY = 'Pensando…';
  var CONNECTION_ERROR = 'No pude conectarme con el servidor de Quantitary IA. Verificá que la aplicación siga en ejecución e intentá de nuevo.';
  var SVG_NS = 'http://www.w3.org/2000/svg';
  var COLORS = ['#1769e8', '#26e5ff', '#071a3d', '#7fa6d9'];

  var panel = document.getElementById('chatPanel');
  var messagesEl = document.getElementById('messages');
  var form = document.getElementById('chatForm');
  var input = document.getElementById('chatInput');
  var sendBtn = document.getElementById('sendButton');
  var newChatBtn = document.getElementById('newChatButton');
  var statusEl = document.getElementById('assistantStatus');
  var historyPanel = document.getElementById('chatHistoryPanel');
  var historyList = document.getElementById('chatHistoryList');
  var historySearch = document.getElementById('historySearch');
  var historyNewChatBtn = document.getElementById('historyNewChatButton');
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


  // Estado de la conversación actual (en memoria: se conserva al cerrar y abrir el panel,
  // y se pierde al recargar la página).
  var turns = [];            // intercambios completos: [{role, text}, ...]
  var hasSentMessage = false;
  var busy = false;
  var sessionId = 0;         // cambia con "Nuevo chat" para descartar respuestas viejas
  var pendingText = null;    // último mensaje que falló, para reintentar
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

  function addMessage(role, text) {
    var row = createEl('div', 'msg-row ' + role);
    if (role === 'assistant') row.appendChild(avatar());
    row.appendChild(createEl('div', 'msg ' + role, text));
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
      if (pendingText !== null) sendMessage(pendingText, true);
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
    if (!hasCharts && !hasTables) return;

    var wrap = createEl('div', 'blocks');
    (charts || []).forEach(function (chart) { wrap.appendChild(renderChart(chart)); });
    (tables || []).forEach(function (table) { wrap.appendChild(renderTable(table)); });
    messagesEl.appendChild(wrap);
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
    var card = createEl('div', 'block-card');
    if (chart.title) card.appendChild(createEl('div', 'block-title', chart.title));

    var W = 320;
    var H = chart.type === 'pie' ? 200 : 180;
    var svg = svgEl('svg', {
      viewBox: '0 0 ' + W + ' ' + H,
      role: 'img',
      'class': 'chart-svg',
      'aria-label': chart.title || 'Gráfico'
    });

    if (chart.type === 'pie') drawPie(svg, chart, W, H);
    else drawCartesian(svg, chart, W, H);

    card.appendChild(svg);
    card.appendChild(renderLegend(chart));
    return card;
  }

  function formatNumber(value) {
    return Number(value).toLocaleString('es-AR', { maximumFractionDigits: 1 });
  }

  function shortLabel(label) {
    return label.length > 10 ? label.slice(0, 9) + '…' : label;
  }

  function drawCartesian(svg, chart, W, H) {
    var pad = { l: 36, r: 8, t: 10, b: 30 };
    var innerW = W - pad.l - pad.r;
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

    var count = chart.labels.length;
    var step = innerW / count;

    chart.labels.forEach(function (label, i) {
      var x = pad.l + step * i + step / 2;
      var text = svgEl('text', { x: x, y: H - 10, 'text-anchor': 'middle' });
      text.textContent = shortLabel(label);
      var tip = svgEl('title');
      tip.textContent = label;
      text.appendChild(tip);
      svg.appendChild(text);
    });

    if (chart.type === 'bar') {
      var groupWidth = step * 0.7;
      var barWidth = groupWidth / chart.series.length;
      var zeroY = yOf(0);

      chart.series.forEach(function (serie, si) {
        serie.values.forEach(function (v, i) {
          if (v === null) return;
          var x = pad.l + step * i + step * 0.15 + barWidth * si;
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
          points.push((pad.l + step * i + step / 2).toFixed(1) + ',' + yOf(v).toFixed(1));
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
            cx: pad.l + step * i + step / 2, cy: yOf(v), r: 3.2, fill: color
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

  function updateSendState() {
    sendBtn.disabled = busy || input.value.trim() === '';
  }

  function setBusy(value) {
    busy = value;
    input.disabled = value;
    statusEl.textContent = value ? STATUS_BUSY : STATUS_IDLE;
    updateSendState();
  }

  function autoResize() {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 120) + 'px';
  }

  function sendMessage(rawText, isRetry) {
    var text = String(rawText || '').trim();
    if (busy || !text) return;
    if (!isRetry && /\b(elimin|borr|sac[aá]|quit[aá])\b/i.test(text) && /producto|sku|stock|precio|cat[aá]logo/i.test(text)) {
      addMessage('user',text);
      addMessage('assistant','Para eliminar necesito el nombre exacto o el SKU. Por seguridad, la eliminación todavía no está habilitada en esta versión de la integración; no modifiqué nada.');
      return;
    }
    if (!isRetry && /\b(cre[aá]|agreg[aá]|nuevo producto|modific[aá]|cambi[aá]|sub[ií]|baj[aá]|aument[aá])\b/i.test(text) && /producto|sku|precio|stock/i.test(text)) {
      addMessage('user',text);
      addMessage('assistant','Abrí el formulario para identificar el producto por SKU y preparar el cambio. Antes de guardar te pediré dos confirmaciones.');
      manageProduct(''); return;
    }
    if (text.length > MAX_LENGTH) text = text.slice(0, MAX_LENGTH);

    if (!currentChatId) { showError('Primero creá o elegí un chat.'); return; }
    var session = sessionId;
    removeErrorRow();
    removeSuggestions();
    pendingText = text;

    if (!isRetry) {
      addMessage('user', text);
      hasSentMessage = true;
    }

    setBusy(true);
    var typingRow = showTyping();

    fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('#chatForm input[name="__RequestVerificationToken"]').value },
      body: JSON.stringify({ message: text, chatId: currentChatId })
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
            charts: Array.isArray(r.data.charts) ? r.data.charts : []
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
        // Si se inició un chat nuevo mientras esperábamos, descartamos esta respuesta.
        if (session !== sessionId) return;

        typingRow.remove();
        if (result.ok) {
          // En el historial se guarda solo el texto: así Gemini recibe el contexto sin JSON de bloques.
          turns.push({ role: 'user', text: text }, { role: 'assistant', text: result.reply });
          pendingText = null;
          if (result.reply) addMessage('assistant', result.reply);
          renderBlocks(result.tables, result.charts);
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
    if (!text) return;
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
    hasSentMessage = false;
    pendingText = null;
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
      var open = createEl('button', 'ia-chat-open', item.titulo || 'Nuevo chat');
      open.type = 'button';
      if (item.id === currentChatId) open.classList.add('active');
      open.addEventListener('click', function () { openChat(item.id).catch(report); });
      var rename = createEl('button', '', '✎');
      rename.type = 'button'; rename.title = 'Renombrar';
      rename.addEventListener('click', async function () {
        var name = window.prompt('Nuevo nombre del chat', item.titulo || '');
        if (!name || !name.trim()) return;
        try { await api('/api/ia/chats/' + item.id + '/renombrar', 'POST', { titulo: name.trim().slice(0,120) }); await loadHistory(); }
        catch (err) { report(err); }
      });
      var remove = createEl('button', '', '×');
      remove.type = 'button'; remove.title = 'Eliminar chat';
      remove.addEventListener('click', async function () {
        if (!window.confirm('¿Eliminar definitivamente este chat y sus mensajes?')) return;
        try {
          await api('/api/ia/chats/' + item.id + '/eliminar', 'POST');
          if (item.id === currentChatId) await newChat();
          else await loadHistory();
        } catch (err) { report(err); }
      });
      line.append(open, rename, remove);
      historyList.appendChild(line);
    });
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
    messagesEl.textContent = '';
    if (!rows.length) addMessage('assistant', GREETING);
    rows.forEach(function (r) {
      var role = r.role === 'user' ? 'user' : 'assistant';
      addMessage(role, r.text || '');
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
  function manageProduct(initial) {
    removeSuggestions();
    var form = createEl('form', 'ia-product-form');
    var title = createEl('strong', '', 'Administrar producto');
    form.appendChild(title);
    var select = createEl('select');
    [['crear','Crear producto'],['precio','Modificar precio'],['stock','Modificar stock']].forEach(function (pair) {
      var opt = createEl('option', '', pair[1]); opt.value = pair[0]; select.appendChild(opt);
    });
    form.appendChild(select);
    var nombre = formRow(form, 'Nombre (para crear)', 'nombre', 'text', initial || '');
    var sku = formRow(form, 'SKU exacto (obligatorio)', 'sku', 'text');
    var categoria = formRow(form, 'Categoría (opcional)', 'categoria', 'text');
    var precio = formRow(form, 'Precio nuevo / inicial', 'precio', 'number');
    var stock = formRow(form, 'Stock nuevo / inicial (opcional al crear)', 'stock', 'number');
    var minimo = formRow(form, 'Stock mínimo (opcional)', 'minimo', 'number');
    var costo = formRow(form, 'Costo (opcional)', 'costo', 'number');
    function updateFields() {
      nombre.parentElement.hidden = categoria.parentElement.hidden = minimo.parentElement.hidden = costo.parentElement.hidden = select.value !== 'crear';
      precio.parentElement.hidden = select.value === 'stock';
      stock.parentElement.hidden = select.value === 'precio';
    }
    select.addEventListener('change', updateFields); updateFields();
    var button = createEl('button', '', 'Revisar antes de guardar'); button.type='submit';form.appendChild(button);
    form.addEventListener('submit', async function (event) {
      event.preventDefault();
      var data = {tipo: select.value, sku: sku.value.trim(), nombre: nombre.value.trim(), categoria: categoria.value.trim(),
        precio: precio.value === '' ? null : Number(precio.value), stock: stock.value === '' ? null : Number(stock.value),
        stockMinimo: minimo.value === '' ? null : Number(minimo.value), costo: costo.value === '' ? null : Number(costo.value)};
      button.disabled = true;
      try {
        var result = await api('/api/ia/productos/preparar', 'POST', data);
        var preview = createEl('div', 'ia-product-form');
        preview.appendChild(createEl('strong','', 'Confirmación 1 de 2'));
        preview.appendChild(createEl('div','',JSON.stringify(result.resumen, null, 2)));
        var confirm = createEl('button', '', 'Confirmar datos'); confirm.type='button';
        var cancel = createEl('button','', 'Cancelar'); cancel.type='button';
        preview.append(confirm,cancel); messagesEl.appendChild(preview); scrollToEnd();
        cancel.addEventListener('click', async function () {await api('/api/ia/productos/cancelar','POST');preview.remove();});
        confirm.addEventListener('click', async function () {
          confirm.disabled = true;
          try {
            var stage = await api('/api/ia/productos/confirmar','POST',{token:result.token});
            preview.appendChild(createEl('strong','',stage.mensaje));
            var final = createEl('button','', 'Confirmar definitivamente');final.type='button';preview.appendChild(final);
            final.addEventListener('click', async function () {
              final.disabled=true;
              try {var done=await api('/api/ia/productos/confirmar','POST',{token:result.token});addMessage('assistant',done.mensaje);preview.remove();form.remove();}
              catch(e){addMessage('assistant',e.message);}
            });
          }catch(e){addMessage('assistant',e.message);}
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
