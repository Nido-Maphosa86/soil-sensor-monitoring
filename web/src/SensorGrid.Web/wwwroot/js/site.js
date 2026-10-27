/* What this file is for: the small bits of browser code: menus, the bell panel, dialogs, the moisture chart, auto-submitting filters.
   How it fits in: loaded by the shared layout on every page. Pages call SensorGrid.initXxx() for their own parts.
   All text coming from the server is put on the page with textContent, never as HTML, so it cannot inject markup. */
(function () {
  'use strict';
  var csrf = (document.querySelector('meta[name="csrf"]') || {}).content || '';

  function $(sel, root) { return (root || document).querySelector(sel); }
  function $all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }
  function el(tag, props, children) {
    var e = document.createElement(tag);
    Object.keys(props || {}).forEach(function (k) {
      if (k === 'text') e.textContent = props[k];
      else if (k === 'style') e.setAttribute('style', props[k]);
      else e.setAttribute(k, props[k]);
    });
    (children || []).forEach(function (c) { e.appendChild(c); });
    return e;
  }
  function post(url) {
    return fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': csrf }, credentials: 'same-origin' });
  }

  /* ---------- Menus: account dropdown and bell panel ---------- */
  var accBtn = $('#accBtn'), accMenu = $('#accMenu'), bellBtn = $('#bellBtn'), bellMenu = $('#bellMenu');
  function closeMenus() { [accMenu, bellMenu].forEach(function (m) { if (m) m.classList.remove('open'); }); }
  if (accBtn) accBtn.addEventListener('click', function (e) { e.stopPropagation(); var open = accMenu.classList.contains('open'); closeMenus(); if (!open) accMenu.classList.add('open'); });
  if (bellBtn) bellBtn.addEventListener('click', function (e) {
    e.stopPropagation();
    var open = bellMenu.classList.contains('open'); closeMenus();
    if (!open) { bellMenu.classList.add('open'); loadBell(); }
  });
  document.addEventListener('click', function (e) { if (!e.target.closest('.menu')) closeMenus(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') { closeMenus(); $all('.modal.open').forEach(function (m) { m.classList.remove('open'); }); } });

  function loadBell() {
    fetch('/Notifications/List', { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(function (items) {
      var list = $('#bellList'); list.textContent = '';
      if (!items.length) { list.appendChild(el('div', { class: 'cap', style: 'padding:18px', text: 'No notifications yet.' })); return; }
      items.forEach(function (n) {
        var colour = n.kind === 0 ? '#C0392F' : n.kind === 1 ? '#B87A15' : '#8A9691';
        var row = el('a', { class: 'bellitem' + (n.isRead ? '' : ' unread'), href: '/Alerts' }, [
          el('s', { style: 'background:' + colour }),
          el('div', {}, [
            el('div', { style: 'font-size:12.5px;font-weight:600', text: n.title }),
            el('div', { style: 'font-size:11.5px;color:var(--tx2);margin-top:2px', text: n.detail }),
            el('div', { style: 'font-size:11px;color:var(--tx3);margin-top:3px', text: n.when })])]);
        row.addEventListener('click', function () { if (!n.isRead) post('/Notifications/Read?id=' + encodeURIComponent(n.notificationId)); });
        list.appendChild(row);
      });
      var dot = $('#bellDot'); if (dot) dot.style.display = items.some(function (n) { return !n.isRead; }) ? 'block' : 'none';
    });
  }
  var readAll = $('#bellReadAll');
  if (readAll) readAll.addEventListener('click', function () { post('/Notifications/ReadAll').then(function () { loadBell(); var d = $('#bellDot'); if (d) d.style.display = 'none'; }); });

  /* ---------- Small screens: the sidebar is a drawer ---------- */
  var side = $('#side'), scrim = $('#scrim'), burger = $('#burger');
  function drawer(open) { if (!side) return; side.classList.toggle('open', open); scrim.classList.toggle('open', open); }
  if (burger) burger.addEventListener('click', function () { drawer(!side.classList.contains('open')); });
  if (scrim) scrim.addEventListener('click', function () { drawer(false); });

  /* ---------- Dialogs ---------- */
  document.addEventListener('click', function (e) {
    var opener = e.target.closest('[data-open]');
    if (opener) { var m = document.getElementById(opener.getAttribute('data-open')); if (m) m.classList.add('open'); }
    if (e.target.closest('[data-close]')) { var open = e.target.closest('.modal'); if (open) open.classList.remove('open'); }
  });
  $all('.modal[data-open-on-load="1"]').forEach(function (m) { m.classList.add('open'); });

  /* ---------- Styled selects and dates keep their label in step; filters submit by themselves ---------- */
  $all('.selectbox').forEach(function (box) {
    var label = $('[data-select-label]', box), ctl = $('select,input', box);
    if (!ctl || !label) return;
    ctl.addEventListener('change', function () {
      if (ctl.tagName === 'SELECT') label.textContent = ctl.options[ctl.selectedIndex].text;
      else label.textContent = ctl.value ? ctl.value.replace('T', ' ') : 'Any';
      if (ctl.hasAttribute('data-autosubmit') && ctl.form) ctl.form.submit();
    });
  });
  $all('[data-confirm]').forEach(function (b) { b.addEventListener('click', function (e) { if (!window.confirm(b.getAttribute('data-confirm'))) e.preventDefault(); }); });
  $all('[data-set-value]').forEach(function (b) { b.addEventListener('click', function () { var t = document.getElementById(b.getAttribute('data-set-value')); if (t) t.value = b.getAttribute('data-value'); }); });
  var flash = $('#flash'); if (flash) setTimeout(function () { flash.style.display = 'none'; }, 6000);

  /* ---------- Export button follows the range select ---------- */
  function initExportRange() {
    var sel = $('#rangeSel'), btn = $('#exportBtn');
    if (!sel || !btn) return;
    sel.addEventListener('change', function () { btn.href = btn.href.replace(/range=[^&]*/, 'range=' + sel.value); });
  }

  /* ---------- Dashboard moisture chart (same drawing rules as the prototype) ---------- */
  function initDashboard() {
    initExportRange();
    var sel = $('#rangeSel');
    function draw(data) {
      var svg = $('#moistureSvg'), NS = 'http://www.w3.org/2000/svg';
      svg.textContent = '';
      function add(tag, attrs, text) { var n = document.createElementNS(NS, tag); Object.keys(attrs).forEach(function (k) { n.setAttribute(k, attrs[k]); }); if (text) n.textContent = text; svg.appendChild(n); return n; }
      var y = function (v) { return 20 + (35 - Math.max(15, Math.min(35, v))) * 8.1; };
      add('rect', { x: 72, y: y(30), width: 632, height: y(20) - y(30), fill: 'rgba(97,201,160,.10)' });
      add('text', { x: 644, y: y(30) + 12, 'font-family': 'Inter', 'font-size': 9, 'font-weight': 500, fill: '#61C9A0' }, 'target band');
      [35, 30, 25, 20, 15].forEach(function (v) {
        add('line', { x1: 72, y1: y(v), x2: 704, y2: y(v), stroke: '#EEF3F0' });
        add('text', { x: 30, y: y(v) + 4, 'font-family': 'Inter', 'font-size': 10, 'font-weight': 500, fill: '#A9B8B1' }, v + ' %');
      });
      data.series.forEach(function (s) {
        var pts = s.values.map(function (v, i) { return (72 + i * 52.67).toFixed(1) + ',' + y(v).toFixed(1); }).join(' ');
        add('polyline', { points: pts, fill: 'none', stroke: s.colour, 'stroke-width': 2.5, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
        add('circle', { cx: 704, cy: y(s.values[s.values.length - 1]).toFixed(1), r: 5, fill: '#fff', stroke: s.colour, 'stroke-width': 3 });
      });
      data.labels.forEach(function (lb, i) { add('text', { x: 68 + i * 90, y: 212, 'font-family': 'Inter', 'font-size': 10, 'font-weight': 500, fill: '#A9B8B1' }, lb); });
      var legend = $('#chartLegend'); legend.textContent = '';
      data.series.forEach(function (s) {
        legend.appendChild(el('span', { style: 'display:flex;align-items:center;gap:6px' }, [el('s', { style: 'width:12px;height:3px;border-radius:2px;background:' + s.colour + ';display:block' }), document.createTextNode(s.name)]));
      });
      $('#chartCaption').textContent = data.caption;
    }
    function load() { fetch('/Dashboard/MoistureChart?range=' + encodeURIComponent(sel.value), { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(draw); }
    sel.addEventListener('change', load);
    load();
  }

  /* ---------- Alerts: note dialog and view note ---------- */
  function initAlerts() {
    $all('[data-note-action]').forEach(function (b) {
      b.addEventListener('click', function () {
        var action = b.getAttribute('data-note-action');
        $('#noteForm').action = '/Alerts/' + action;
        $('#noteId').value = b.getAttribute('data-id');
        $('#noteTitle').textContent = action + ' alert';
        $('#noteSubmit').textContent = action;
        $('#noteSub').textContent = 'Sensor ' + b.getAttribute('data-sensor') + '. Your name and the time are saved with the note.';
        $('#noteText').value = '';
        $('#noteModal').classList.add('open');
        $('#noteText').focus();
      });
    });
    $all('[data-view-note]').forEach(function (b) {
      b.addEventListener('click', function () {
        fetch('/Alerts/Note?id=' + encodeURIComponent(b.getAttribute('data-view-note')), { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(function (n) {
          $('#vnTitle').textContent = 'Note for ' + n.sensor;
          $('#vnSub').textContent = n.status + ' by ' + n.owner + (n.when ? ' · ' + n.when : '');
          $('#vnText').textContent = n.note;
          $('#viewNoteModal').classList.add('open');
        });
      });
    });
  }

  /* ---------- Users: fill the edit dialog ---------- */
  function initUsers() {
    $all('[data-edit-user]').forEach(function (b) {
      b.addEventListener('click', function () {
        $('#editUserId').value = b.getAttribute('data-edit-user');
        $('#editName').value = b.getAttribute('data-name');
        $('#editEmail').value = b.getAttribute('data-email');
        $('#editRole').value = b.getAttribute('data-role');
        $('#editStatus').value = b.getAttribute('data-status');
        var sites = (b.getAttribute('data-sites') || '').split(',');
        $all('#editModal [data-site]').forEach(function (c) { c.checked = sites.indexOf(c.getAttribute('data-site')) >= 0; });
        $('#editModal').classList.add('open');
      });
    });
  }

  /* ---------- Profile: notification toggles save straight away ---------- */
  $all('[data-toggle]').forEach(function (t) {
    t.addEventListener('click', function () {
      var key = t.getAttribute('data-toggle'), input = document.getElementById('n_' + key);
      input.value = input.value === 'true' ? 'false' : 'true';
      document.getElementById('notifForm').submit();
    });
  });

  window.SensorGrid = { initDashboard: initDashboard, initExportRange: initExportRange, initAlerts: initAlerts, initUsers: initUsers };
})();
