// EconAdvisor demo UI
// Local: python -m http.server 8000 from docs/
// API runs at http://localhost:5050

const API_BASE = 'http://localhost:5050';

const SERIES = [
  { key: 'policy_rate',        label: 'Policy Rate',        unit: '% (Riksbank reporänta)', source: 'Riksbank' },
  { key: 'cpif',               label: 'CPIF Inflation',     unit: '% YoY',                  source: 'SCB' },
  { key: 'unemployment',       label: 'Unemployment',       unit: '% (15–74 yrs)',           source: 'SCB' },
  { key: 'gdp_growth',         label: 'GDP Growth',         unit: '% annual',               source: 'World Bank' },
  { key: 'real_interest_rate', label: 'Real Interest Rate', unit: '% (rate − CPIF)',         source: 'Derived' },
  { key: 'yield_curve_slope',  label: 'Yield Curve Slope',  unit: '% (10y − 2y)',            source: 'Derived' },
];

const charts = {};

// ── Boot ────────────────────────────────────────────────────────────────────

async function init() {
  buildGrid();
  await loadAllIndicators();

  // Wire question buttons
  document.querySelectorAll('.q-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.getElementById('questionInput').value = btn.dataset.q;
      runAnalysis();
    });
  });

  // Enter key submits
  document.getElementById('questionInput').addEventListener('keydown', e => {
    if (e.key === 'Enter') runAnalysis();
  });
}

// ── Build grid cards ─────────────────────────────────────────────────────────

function buildGrid() {
  const grid = document.getElementById('indicatorsGrid');
  SERIES.forEach(s => {
    const card = document.createElement('div');
    card.className = 'indicator-card';
    card.id = `card-${s.key}`;
    card.innerHTML = `
      <div class="card-header">
        <span class="card-name">${s.label}</span>
        <span class="card-source">${s.source}</span>
      </div>
      <div class="card-value" id="val-${s.key}">—</div>
      <div class="card-unit">${s.unit}</div>
      <canvas class="card-chart" id="chart-${s.key}"></canvas>
    `;
    grid.appendChild(card);
  });
}

// ── Load indicators ──────────────────────────────────────────────────────────

async function loadAllIndicators() {
  setStatus('Loading…', false);
  const results = await Promise.allSettled(
    SERIES.map(s => fetchSeries(s.key))
  );

  let anyOk = false;
  results.forEach((r, i) => {
    if (r.status === 'fulfilled' && r.value) {
      renderCard(SERIES[i], r.value);
      anyOk = true;
    }
  });

  setStatus(anyOk ? 'Live data' : 'API offline', anyOk);
}

async function fetchSeries(key) {
  const res = await fetch(`${API_BASE}/api/indicators/SE/${key}`);
  if (!res.ok) return null;
  return res.json();
}

// ── Render a card + sparkline ────────────────────────────────────────────────

function renderCard(meta, data) {
  const obs = data.observations || [];
  if (!obs.length) return;

  const latest = obs[obs.length - 1].value;
  const valEl  = document.getElementById(`val-${meta.key}`);
  valEl.textContent = formatValue(latest, meta.key);
  valEl.className   = 'card-value' + valueClass(latest, meta.key);

  // Sparkline — last 24 observations
  const slice  = obs.slice(-24);
  const labels = slice.map(o => o.date?.substring(0, 7) ?? '');
  const values = slice.map(o => o.value);

  const ctx = document.getElementById(`chart-${meta.key}`).getContext('2d');
  if (charts[meta.key]) charts[meta.key].destroy();

  charts[meta.key] = new Chart(ctx, {
    type: 'line',
    data: {
      labels,
      datasets: [{
        data: values,
        borderColor: sparkColour(latest, meta.key),
        borderWidth: 1.5,
        pointRadius: 0,
        tension: 0.3,
        fill: true,
        backgroundColor: sparkFill(latest, meta.key),
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { enabled: false } },
      scales: { x: { display: false }, y: { display: false } },
      animation: false,
    }
  });
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatValue(v, key) {
  if (v == null) return '—';
  const abs = Math.abs(v);
  const sign = v < 0 ? '−' : '';
  if (key === 'gdp_growth' || key === 'real_interest_rate' || key === 'yield_curve_slope') {
    return sign + abs.toFixed(2) + '%';
  }
  return sign + abs.toFixed(2) + '%';
}

function valueClass(v, key) {
  if (v == null) return '';
  if (key === 'yield_curve_slope' && v < 0) return ' danger';
  if (key === 'real_interest_rate' && v < 0) return ' warning';
  if (key === 'gdp_growth' && v < 0) return ' negative';
  return '';
}

function sparkColour(v, key) {
  if (key === 'yield_curve_slope' && v < 0) return '#ff4d6d';
  if (key === 'gdp_growth' && v < 0)        return '#ff4d6d';
  return '#00d4ff';
}

function sparkFill(v, key) {
  if (key === 'yield_curve_slope' && v < 0) return 'rgba(255,77,109,0.07)';
  return 'rgba(0,212,255,0.06)';
}

function setStatus(text, ok) {
  const pill = document.getElementById('statusPill');
  const span = document.getElementById('statusText');
  span.textContent = text;
  pill.className = 'status-pill' + (ok ? ' ok' : ok === false ? ' err' : '');
}

// ── Analysis ─────────────────────────────────────────────────────────────────

async function runAnalysis() {
  const q = document.getElementById('questionInput').value.trim();
  if (!q) return;

  const askBtn      = document.getElementById('askBtn');
  const answerPanel = document.getElementById('answerPanel');
  const loadPanel   = document.getElementById('loadingPanel');
  const errorPanel  = document.getElementById('errorPanel');

  answerPanel.style.display = 'none';
  errorPanel.style.display  = 'none';
  loadPanel.style.display   = 'block';
  askBtn.disabled = true;

  try {
    const res = await fetch(`${API_BASE}/api/analyse`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question: q, country: 'SE' }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.detail || `HTTP ${res.status}`);
    }

    const data = await res.json();
    showAnswer(data);

  } catch (err) {
    document.getElementById('errorText').textContent =
      err.message.includes('fetch')
        ? 'Cannot reach the API. Make sure dotnet run is active on port 5050.'
        : err.message;
    errorPanel.style.display = 'block';
  } finally {
    loadPanel.style.display = 'none';
    askBtn.disabled = false;
  }
}

function showAnswer(data) {
  document.getElementById('answerQuestion').textContent = data.question;
  document.getElementById('answerText').textContent     = data.analysis;

  // Citations
  const citSec  = document.getElementById('citationsSection');
  const citList = document.getElementById('citationsList');
  citList.innerHTML = '';
  const cits = Array.isArray(data.citations) ? data.citations : [];
  if (cits.length) {
    cits.forEach(c => {
      const li = document.createElement('li');
      li.textContent = c;
      citList.appendChild(li);
    });
    citSec.style.display = 'block';
  } else {
    citSec.style.display = 'none';
  }

  // Meta
  const ts = new Date(data.generatedAt).toLocaleTimeString();
  const filled = Object.values(data.indicators || {}).filter(v => v != null).length;
  document.getElementById('answerMeta').textContent =
    `Generated ${ts} · ${filled}/6 indicators · country: ${data.country}`;

  document.getElementById('answerPanel').style.display = 'block';
}

// ── Start ────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', init);
