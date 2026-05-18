const authStorageKey = "fjob.admin.session";

let session = loadSession();

const loginPanel = document.getElementById("login-panel");
const dashboardShell = document.getElementById("dashboard-shell");
const loginForm = document.getElementById("login-form");
const loginStatus = document.getElementById("login-status");
const logoutButton = document.getElementById("logout-button");
const sessionUser = document.getElementById("session-user");
const sessionRole = document.getElementById("session-role");

const metricsGrid = document.getElementById("metrics-grid");
const sourcesList = document.getElementById("sources-list");
const runsTable = document.getElementById("runs-table");
const queueTable = document.getElementById("queue-table");
const failedSources = document.getElementById("failed-sources");
const auditTable = document.getElementById("audit-table");
const refreshButton = document.getElementById("refresh-button");
const manualTriggerForm = document.getElementById("manual-trigger-form");
const manualTriggerStatus = document.getElementById("manual-trigger-status");

async function boot() {
  bindEvents();

  if (!session?.accessToken) {
    renderLoggedOut();
    return;
  }

  const user = await fetchCurrentUser();
  if (!user) {
    clearSession();
    renderLoggedOut();
    return;
  }

  session.username = user.username;
  session.role = user.role;
  saveSession();
  renderLoggedIn();
  await refreshDashboard();
}

function bindEvents() {
  loginForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await login();
  });

  logoutButton.addEventListener("click", async () => {
    await logout();
  });

  refreshButton.addEventListener("click", refreshDashboard);
  manualTriggerForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await triggerManualCrawl();
  });
}

function renderLoggedOut() {
  loginPanel.classList.remove("hidden");
  dashboardShell.classList.add("hidden");
}

function renderLoggedIn() {
  loginPanel.classList.add("hidden");
  dashboardShell.classList.remove("hidden");
  sessionUser.textContent = session?.username ?? "Unknown";
  sessionRole.textContent = `Role: ${session?.role ?? "-"}`;
  document.getElementById("trigger-button").disabled = !isOperator();
}

async function login() {
  loginStatus.className = "status";
  loginStatus.textContent = "Dang xac thuc...";
  loginStatus.classList.remove("hidden");

  const payload = {
    username: document.getElementById("login-username").value.trim(),
    password: document.getElementById("login-password").value
  };

  const response = await fetch("/api/admin/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  const result = await response.json();
  if (!response.ok) {
    loginStatus.className = "status danger";
    loginStatus.textContent = result?.message || "Dang nhap that bai.";
    return;
  }

  session = result;
  saveSession();
  loginStatus.className = "status success";
  loginStatus.textContent = "Dang nhap thanh cong.";
  renderLoggedIn();
  await refreshDashboard();
}

async function logout() {
  if (session?.accessToken) {
    await authFetch("/api/admin/auth/logout", { method: "POST" });
  }

  clearSession();
  renderLoggedOut();
}

async function fetchCurrentUser() {
  const response = await authFetch("/api/admin/auth/me");
  if (response.status === 401) {
    return null;
  }

  return response.ok ? await response.json() : null;
}

async function refreshDashboard() {
  const response = await authFetch("/api/admin/dashboard");
  if (await handleUnauthorized(response)) {
    return;
  }

  const data = await response.json();

  renderMetrics(data.queueSummary, data.sources, data.recentCrawlRuns);
  renderSources(data.sources);
  renderRuns(data.recentCrawlRuns);
  renderQueue(data.queueItems);
  renderFailedSources(data.failedSources);
  renderAuditLogs(data.recentAuditLogs || []);
}

function renderMetrics(queueSummary, sources, runs) {
  const pausedCount = sources.filter((item) => item.isPaused).length;
  const failedRuns = runs.filter((item) => item.status === 4).length;

  metricsGrid.innerHTML = [
    metricCard("Pending", queueSummary.pendingCount),
    metricCard("Processing", queueSummary.processingCount),
    metricCard("Failed Queue", queueSummary.failedCount),
    metricCard("Paused Sources", pausedCount),
    metricCard("Failed Runs", failedRuns)
  ].join("");
}

function metricCard(label, value) {
  return `<article class="metric-card"><div class="metric-label">${label}</div><div class="metric-value">${value}</div></article>`;
}

function renderSources(items) {
  sourcesList.innerHTML = items.map((item) => `
    <article class="source-card">
      <div class="source-top">
        <div>
          <strong>${escapeHtml(item.source)}</strong>
          <div class="source-meta">Cap nhat ${formatDateTime(item.updatedAtUtc)}</div>
          <div class="source-meta">${item.reason ? escapeHtml(item.reason) : "Khong co ghi chu"}</div>
        </div>
        <span class="chip ${item.isPaused ? "danger" : "success"}">${item.isPaused ? "Paused" : "Running"}</span>
      </div>
      <div class="table-actions" style="margin-top:12px">
        ${isOperator()
          ? item.isPaused
            ? `<button data-action="resume" data-source="${escapeHtml(item.source)}">Resume</button>`
            : `<button data-action="pause" data-source="${escapeHtml(item.source)}">Pause</button>`
          : `<span class="table-muted">Readonly role</span>`}
      </div>
    </article>
  `).join("");

  sourcesList.querySelectorAll("button").forEach((button) => {
    button.addEventListener("click", async () => {
      const source = button.dataset.source;
      if (button.dataset.action === "pause") {
        if (!window.confirm(`Pause source ${source}?`)) {
          return;
        }

        const reason = window.prompt(`Ly do pause source ${source}:`, "Operational pause");
        await authFetch(`/api/admin/sources/${encodeURIComponent(source)}/pause`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ reason })
        });
      } else {
        if (!window.confirm(`Resume source ${source}?`)) {
          return;
        }

        await authFetch(`/api/admin/sources/${encodeURIComponent(source)}/resume`, { method: "POST" });
      }

      await refreshDashboard();
    });
  });
}

function renderRuns(items) {
  runsTable.innerHTML = buildTable(
    ["Source", "Keyword", "Status", "Imported", "Requested", "Trace"],
    items.map((item) => [
      escapeHtml(item.source),
      escapeHtml(item.keyword),
      statusText(item.status),
      item.importedJobs,
      formatDateTime(item.requestedAtUtc),
      `<span class="table-muted">${escapeHtml(item.traceId)}</span>`
    ])
  );
}

function renderQueue(items) {
  queueTable.innerHTML = buildTable(
    ["Source", "Keyword", "Status", "Attempts", "Next Attempt", "Error", "Action"],
    items.map((item) => [
      escapeHtml(item.source),
      escapeHtml(item.keyword),
      statusText(item.status),
      item.attemptCount,
      formatDateTime(item.nextAttemptAtUtc),
      `<span class="table-muted">${escapeHtml(item.errorMessage || "")}</span>`,
      isOperator() && item.status === 4
        ? `<button data-retry="${item.requestId}">Retry</button>`
        : ""
    ])
  );

  queueTable.querySelectorAll("button[data-retry]").forEach((button) => {
    button.addEventListener("click", async () => {
      if (!window.confirm("Retry crawl request nay?")) {
        return;
      }

      await authFetch(`/api/admin/crawl-requests/${button.dataset.retry}/retry`, { method: "POST" });
      await refreshDashboard();
    });
  });
}

function renderFailedSources(items) {
  failedSources.innerHTML = items.length
    ? items.map((item) => `<span class="chip danger">${escapeHtml(item.source)}: ${item.failureCount}</span>`).join("")
    : `<span class="chip success">Khong co source loi gan day</span>`;
}

function renderAuditLogs(items) {
  auditTable.innerHTML = buildTable(
    ["Time", "Action", "Target", "Actor", "Result", "Details"],
    items.map((item) => [
      formatDateTime(item.occurredAtUtc),
      escapeHtml(item.action),
      escapeHtml(item.target),
      escapeHtml(item.actor),
      item.success ? `<span class="chip success">Success</span>` : `<span class="chip danger">Failed</span>`,
      escapeHtml(item.details)
    ])
  );
}

function buildTable(headers, rows) {
  if (!rows.length) {
    return `<p class="table-muted">Chua co du lieu.</p>`;
  }

  return `
    <table>
      <thead>
        <tr>${headers.map((header) => `<th>${header}</th>`).join("")}</tr>
      </thead>
      <tbody>
        ${rows.map((row) => `<tr>${row.map((cell) => `<td>${cell}</td>`).join("")}</tr>`).join("")}
      </tbody>
    </table>
  `;
}

async function triggerManualCrawl() {
  if (!isOperator()) {
    manualTriggerStatus.className = "status danger";
    manualTriggerStatus.textContent = "Role readonly khong duoc trigger crawl.";
    return;
  }

  manualTriggerStatus.className = "status";
  manualTriggerStatus.textContent = "Dang trigger crawl...";

  const payload = {
    source: document.getElementById("source").value.trim(),
    keyword: document.getElementById("keyword").value.trim(),
    triggeredBy: session?.username || "admin-dashboard"
  };

  const response = await authFetch("/api/admin/crawl-requests", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (await handleUnauthorized(response)) {
    return;
  }

  const result = await response.json();

  if (!response.ok) {
    manualTriggerStatus.className = "status danger";
    manualTriggerStatus.textContent = buildFailureMessage(response.status, result);
    return;
  }

  manualTriggerStatus.className = "status success";
  manualTriggerStatus.textContent = `Da tao crawl request thanh cong cho source ${payload.source}.`;
  await refreshDashboard();
}

function buildFailureMessage(statusCode, result) {
  const message = result?.message || `HTTP ${statusCode}`;
  try {
    const nested = JSON.parse(message);
    return nested.message ? `Trigger that bai: ${nested.message}` : `Trigger that bai: ${message}`;
  } catch {
    return `Trigger that bai: ${message}`;
  }
}

async function authFetch(url, options = {}) {
  const headers = new Headers(options.headers || {});
  if (session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  return fetch(url, { ...options, headers });
}

async function handleUnauthorized(response) {
  if (response.status !== 401 && response.status !== 403) {
    return false;
  }

  clearSession();
  renderLoggedOut();
  loginStatus.className = "status danger";
  loginStatus.textContent = response.status === 401
    ? "Phien dang nhap het han hoac khong hop le."
    : "Tai khoan hien tai khong du quyen thuc hien thao tac nay.";
  loginStatus.classList.remove("hidden");
  return true;
}

function isOperator() {
  return session?.role === "operator";
}

function loadSession() {
  const raw = window.localStorage.getItem(authStorageKey);
  return raw ? JSON.parse(raw) : null;
}

function saveSession() {
  window.localStorage.setItem(authStorageKey, JSON.stringify(session));
}

function clearSession() {
  session = null;
  window.localStorage.removeItem(authStorageKey);
}

function statusText(status) {
  return {
    1: "Pending/Requested",
    2: "Processing/Running",
    3: "Completed",
    4: "Failed"
  }[status] ?? `Unknown (${status})`;
}

function formatDateTime(value) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "short",
    timeStyle: "short"
  }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

boot();
