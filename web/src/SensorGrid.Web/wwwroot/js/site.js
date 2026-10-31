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
    // the date box is invisible, and a click in its middle does not open the calendar by itself, so open it here
    if (ctl.tagname === 'input' && ctl.showpicker) box.addeventlistener('click', function () { try { ctl.showpicker(); } catch (e) { } });
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

  /* ---------- Dashboard trend chart: one line per province; "Last 15 minutes" has a point every 10 seconds and reloads every 10 seconds ---------- */
  function initDashboard() {
    initExportRange();
    var rangeSel = $('#rangeSel'), paramSel = $('#paramSel'), timer = null;
    var L = 72, R = 704, T = 20, B = 182;                          // the plotting area inside the 736 x 220 picture
    function draw(data) {
      var svg = $('#trendSvg'), NS = 'http://www.w3.org/2000/svg';
      svg.textContent = '';
      function add(tag, attrs, text) { var n = document.createElementNS(NS, tag); Object.keys(attrs).forEach(function (k) { n.setAttribute(k, attrs[k]); }); if (text) n.textContent = text; svg.appendChild(n); return n; }
      var font = { 'font-family': 'Inter', 'font-size': 10, 'font-weight': 500, fill: '#A9B8B1' };
      function text(x, y, s, anchor) { return add('text', Object.assign({ x: x, y: y, 'text-anchor': anchor || 'start' }, font), s); }

      // the value axis fits the data (and the target band), with a little room above and below
      var values = [];
      data.series.forEach(function (s) { s.points.forEach(function (p) { values.push(p.v); }); });
      if (data.bandMin !== null) values.push(data.bandMin, data.bandMax);
      var lo = Math.min.apply(null, values), hi = Math.max.apply(null, values);
      if (!values.length) { lo = 0; hi = 1; }
      if (hi - lo < 1e-9) { lo -= 1; hi += 1; }
      var pad = (hi - lo) * 0.1; lo -= pad; hi += pad;
      var x = function (t) { return L + (t - data.from) / (data.to - data.from) * (R - L); };
      var y = function (v) { return B - (v - lo) / (hi - lo) * (B - T); };

      if (data.bandMin !== null) {
        add('rect', { x: L, y: y(data.bandMax), width: R - L, height: y(data.bandMin) - y(data.bandMax), fill: 'rgba(97,201,160,.10)' });
        add('text', { x: R - 60, y: y(data.bandMax) + 12, 'font-family': 'Inter', 'font-size': 9, 'font-weight': 500, fill: '#61C9A0' }, 'target band');
      }
      for (var i = 0; i <= 4; i++) {
        var v = lo + (hi - lo) * i / 4;
        add('line', { x1: L, y1: y(v), x2: R, y2: y(v), stroke: '#EEF3F0' });
        text(L - 8, y(v) + 4, v.toFixed(data.decimals) + (data.unit === 'pH' ? '' : ' ' + data.unit), 'end');
      }
      data.labels.forEach(function (lb, i) { text(x(lb.t), 206, lb.text, i === 0 ? 'start' : i === data.labels.length - 1 ? 'end' : 'middle'); });

      data.series.forEach(function (s) {
        var pts = s.points.map(function (p) { return x(p.t).toFixed(1) + ',' + y(p.v).toFixed(1); }).join(' ');
        add('polyline', { points: pts, fill: 'none', stroke: s.colour, 'stroke-width': 2, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
        var last = s.points[s.points.length - 1];
        add('circle', { cx: x(last.t).toFixed(1), cy: y(last.v).toFixed(1), r: 4.5, fill: '#fff', stroke: s.colour, 'stroke-width': 3 });
      });
      if (!data.series.length) text((L + R) / 2, (T + B) / 2, 'No readings in this period', 'middle');

      var legend = $('#chartLegend'); legend.textContent = '';
      data.series.forEach(function (s) {
        var last = s.points[s.points.length - 1];
        legend.appendChild(el('span', { style: 'display:flex;align-items:center;gap:6px' }, [el('s', { style: 'width:12px;height:3px;border-radius:2px;background:' + s.colour + ';display:block' }),
          document.createTextNode(s.name + '  ' + last.v.toFixed(data.decimals) + (data.unit === 'pH' ? '' : ' ' + data.unit))]));
      });
      $('#chartCaption').textContent = data.caption;
    }
    var card = $('#trendSvg').closest('.chart');
    // quiet = the automatic 10-second reload: no loading skeleton, the new line simply replaces the old one
    function load(quiet) {
      clearTimeout(timer);
      if (!quiet) { $('#trendSvg').textContent = ''; card.classList.add('loading'); }
      var url = '/Dashboard/Trend?range=' + encodeURIComponent(rangeSel.value) + '&parameter=' + encodeURIComponent(paramSel.value);
      fetch(url, { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(function (data) {
        card.classList.remove('loading');
        draw(data);
        if (data.live) timer = setTimeout(function () { load(true); }, 10000);   // "Last 15 minutes": fetch the newest readings again in 10 seconds
      }).catch(function () { card.classList.remove('loading'); });
    }
    rangeSel.addEventListener('change', function () { load(false); });
    paramSel.addEventListener('change', function () { load(false); });
    load(false);
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


    /* ---------- Live parts of a page: blocks marked data-live are fetched again in the background and swapped in ---------- */
  // data-live-every says how often, in seconds. One fetch of the same page serves every block with the same interval.
  (function () {
    var groups = {};
    $all('[data-live]').forEach(function (b) { var s = +b.getAttribute('data-live-every') || 10; (groups[s] = groups[s] || []).push(b); });
    Object.keys(groups).forEach(function (s) {
      setInterval(function () {
        if (document.hidden || $('.modal.open')) return;   // not while the tab is hidden or a dialog is open
        fetch(location.href, { credentials: 'same-origin' })
          .then(function (r) { return r.ok && !r.redirected ? r.text() : null; })   // signed out: leave the page as it is
          .then(function (html) {
            if (!html) return;
            var doc = new DOMParser().parseFromString(html, 'text/html');
            groups[s].forEach(function (b) {
              var fresh = doc.querySelector('[data-live="' + b.getAttribute('data-live') + '"]');
              if (fresh) b.innerHTML = fresh.innerHTML;
            });
          })
          .catch(function () { });                          // offline for a moment: try again next time
      }, s * 1000);
    });
  })();
  // the bell's unread dot and list, every 15 seconds on every page
  if (bellBtn) setInterval(function () { if (!document.hidden) loadBell(); }, 15000);

  /* ---------- Password fields: the eye button shows or hides what was typed ---------- */
  $all('[data-show-password]').forEach(function (b) {
    b.addEventListener('click', function () {
      var input = b.parentNode.querySelector('input');
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      b.classList.toggle('on', show);
      b.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
      b.title = b.getAttribute('aria-label');
      input.focus();
    });
  });
  // hide every password again before a form is sent, so the browser never saves it as plain text
  $all('form').forEach(function (f) {
    f.addEventListener('submit', function () { $all('[data-show-password].on', f).forEach(function (b) { b.click(); }); });
  });

  window.SensorGrid = { initDashboard: initDashboard, initExportRange: initExportRange, initAlerts: initAlerts, initUsers: initUsers };
})();
