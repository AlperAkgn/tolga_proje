(function () {
  "use strict";

  const api = window.AfterformArtist;
  const ui = window.AfterformDemos;
  const list = document.getElementById("history-list");
  const loading = document.getElementById("history-loading");
  const errorBox = document.getElementById("history-error");
  const empty = document.getElementById("history-empty");
  const pager = document.getElementById("history-pages");
  const previous = document.getElementById("previous-page");
  const next = document.getElementById("next-page");
  const refresh = document.getElementById("history-refresh");
  const search = document.getElementById("demo-search");
  const tabs = Array.from(document.querySelectorAll("[data-status]"));
  const allowed = ["", "Submitted", "InReview", "Accepted", "Rejected"];
  const params = new URLSearchParams(window.location.search);
  let selected = allowed.includes(params.get("status")) ? params.get("status") : "";
  let term = (params.get("search") || "").trim().slice(0, 100);
  let page = Math.min(1000000, Math.max(1, Number.parseInt(params.get("page"), 10) || 1));
  let artistId = null;
  let controller = null;
  let sequence = 0;
  let pending = false;
  let lastSuccess = 0;
  let totalPages = 0;
  let hasSnapshot = false;
  let debounce;
  search.value = term;

  function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function clearPrivateData() {
    controller?.abort();
    sequence += 1;
    pending = false;
    hasSnapshot = false;
    lastSuccess = 0;
    totalPages = 0;
    list.replaceChildren();
    pager.hidden = empty.hidden = errorBox.hidden = true;
    loading.hidden = false;
    document.getElementById("history-total").textContent = "—";
    document.querySelectorAll("[data-count]").forEach(node => { node.textContent = "—"; });
    document.getElementById("refresh-time").textContent = "Private artist portal / Demo history";
  }

  function setPending(value, background) {
    pending = value;
    list.setAttribute("aria-busy", String(value));
    loading.hidden = !value;
    loading.textContent = background ? "Refreshing your demo history…" : "Loading your demos…";
    refresh.disabled = value || !artistId;
    previous.disabled = value || page <= 1;
    next.disabled = value || page >= totalPages;
  }

  function syncTabs() {
    tabs.forEach(tab => { tab.setAttribute("aria-pressed", String(tab.dataset.status === selected)); });
  }

  function renderRow(demo) {
    const urls = ui.urls(demo.id);
    const state = ui.status(demo.status);
    const row = element("article", "demo-row");
    const track = element("div", "demo-track");
    const sleeve = element("div", "sleeve");
    sleeve.setAttribute("aria-hidden", "true");
    sleeve.append(element("b", "", `AF—${demo.id}`));
    const copy = element("div", "track-copy");
    const heading = element("h3", "track-title");
    const title = element("a", "", demo.title);
    title.href = urls.detail;
    title.title = demo.title;
    heading.append(title);
    copy.append(heading, element("p", "track-meta", `${demo.genre} · ${ui.size(demo.fileSizeBytes)} · #${demo.id}`));
    const links = element("div", "row-links");
    const view = element("a", "", "View demo ↗");
    view.href = urls.detail;
    const download = element("a", "", "Download original ↓");
    download.href = urls.download;
    links.append(view, download);
    copy.append(links);
    track.append(sleeve, copy);
    const date = element("div", "date-block");
    date.append(element("small", "", "Submitted"), element("span", "", ui.date(demo.submittedAtUtc)));
    const status = element("div", `status-block ${state.className}`);
    const line = element("span", "status-line");
    const dot = element("i", "status-dot");
    dot.setAttribute("aria-hidden", "true");
    line.append(dot, element("span", "", state.label));
    status.append(element("small", "", "Current status"), line,
      element("span", "status-date", demo.updatedAtUtc ? `Updated ${ui.date(demo.updatedAtUtc)}` : "No review update yet"));
    row.append(track, date, status);
    return row;
  }

  function render(data) {
    if (!data || !Array.isArray(data.items) || !data.summary || !Number.isSafeInteger(data.totalCount)
      || !Number.isSafeInteger(data.page) || !Number.isSafeInteger(data.totalPages)) {
      throw new Error("Demo history is incomplete.");
    }
    const rows = data.items.map(renderRow);
    list.replaceChildren(...rows);
    page = data.page;
    totalPages = data.totalPages;
    document.getElementById("history-total").textContent = data.totalCount;
    document.querySelectorAll("[data-count]").forEach(node => {
      const count = data.summary[node.dataset.count];
      node.textContent = Number.isSafeInteger(count) && count >= 0 ? count : "—";
    });
    empty.hidden = rows.length !== 0;
    const first = data.summary.total === 0;
    document.getElementById("empty-title").textContent = first ? "Your first demo starts here." : "No matching demos.";
    document.getElementById("empty-copy").textContent = first
      ? "Send your first recording to start your private demo archive."
      : "Try another status or search term to find a recording in your archive.";
    document.getElementById("empty-send").hidden = !first;
    document.getElementById("clear-filters").hidden = first;
    pager.hidden = totalPages <= 1;
    document.getElementById("page-label").textContent = `Page ${page} of ${totalPages} · ${data.totalCount} demos`;
    const query = new URLSearchParams();
    if (selected) query.set("status", selected);
    if (term) query.set("search", term);
    if (page > 1) query.set("page", page);
    window.history.replaceState(null, "", `/artist/${query.size ? `?${query}` : ""}`);
    lastSuccess = Date.now();
    hasSnapshot = true;
    document.getElementById("refresh-time").textContent = `Updated ${new Date(lastSuccess).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })} · Times shown in your local time`;
  }

  async function load({ background = false } = {}) {
    if (!artistId) return;
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    const currentSequence = ++sequence;
    const account = artistId;
    if (!background) {
      list.replaceChildren();
      empty.hidden = pager.hidden = true;
      hasSnapshot = false;
      document.getElementById("history-total").textContent = "—";
    }
    errorBox.hidden = true;
    setPending(true, background);
    syncTabs();
    const query = new URLSearchParams({ page: String(page), pageSize: "10" });
    if (selected) query.set("status", selected);
    if (term) query.set("search", term);
    try {
      const data = await api.request(`/api/demos?${query}`, { signal });
      if (signal.aborted || currentSequence !== sequence || account !== artistId) return;
      if (typeof data?.artistId !== "string") throw new Error("The demo history response is incomplete.");
      if (data.artistId !== account) {
        clearPrivateData();
        document.querySelector("[data-auth-content]").hidden = true;
        window.location.reload();
        return;
      }
      render(data);
    } catch (error) {
      if (signal.aborted || currentSequence !== sequence) return;
      if (error.status === 401) {
        clearPrivateData();
        document.querySelector("[data-auth-content]").hidden = true;
        window.location.replace("/artist/login/");
        return;
      }
      empty.hidden = true;
      const text = error.status >= 500 ? "Demo services are temporarily unavailable." : "We couldn’t load your demo history.";
      document.getElementById("history-error-text").textContent = `${text} ${hasSnapshot ? "The records below are from the last successful refresh. " : ""}Please try again.`;
      errorBox.hidden = false;
    } finally {
      if (currentSequence === sequence) setPending(false, background);
    }
  }

  function applySearch() {
    clearTimeout(debounce);
    const value = search.value.trim().slice(0, 100);
    if (value === term) return;
    term = value;
    page = 1;
    load();
  }

  tabs.forEach(tab => tab.addEventListener("click", () => {
    if (selected === tab.dataset.status) return;
    selected = tab.dataset.status;
    page = 1;
    load();
  }));
  search.addEventListener("input", () => { clearTimeout(debounce); debounce = setTimeout(applySearch, 350); });
  document.getElementById("history-search").addEventListener("submit", event => { event.preventDefault(); applySearch(); });
  document.getElementById("clear-filters").addEventListener("click", () => {
    clearTimeout(debounce);
    selected = term = "";
    search.value = "";
    page = 1;
    load();
  });
  refresh.addEventListener("click", () => load({ background: true }));
  document.getElementById("history-retry").addEventListener("click", () => load({ background: hasSnapshot }));
  previous.addEventListener("click", () => { if (!pending && page > 1) { page -= 1; load(); } });
  next.addEventListener("click", () => { if (!pending && page < totalPages) { page += 1; load(); } });
  document.addEventListener("afterform:artist-ready", event => {
    const id = event.detail.id;
    if (typeof id !== "string" || !id) return;
    if (artistId !== null && artistId !== id) {
      clearPrivateData();
      selected = term = "";
      search.value = "";
      page = 1;
    }
    artistId = id;
    if (!pending && Date.now() - lastSuccess >= 10000) load({ background: hasSnapshot });
  });
  setInterval(() => {
    if (!document.hidden && !document.querySelector("[data-auth-content]").hidden && !pending
      && Date.now() - lastSuccess >= 60000) load({ background: hasSnapshot });
  }, 60000);
  window.addEventListener("pagehide", () => { clearTimeout(debounce); clearPrivateData(); artistId = null; });
  syncTabs();
})();
