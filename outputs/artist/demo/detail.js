(function () {
  "use strict";
  const ui = window.AfterformDemos;
  const idText = new URLSearchParams(window.location.search).get("id") || "";
  const id = /^[1-9]\d*$/.test(idText) ? Number(idText) : NaN;
  const validId = Number.isSafeInteger(id) && id > 0;
  const detail = document.getElementById("demo-detail");
  const loading = document.getElementById("detail-loading");
  const errorBox = document.getElementById("detail-error");
  const retry = document.getElementById("detail-retry");
  const refresh = document.getElementById("detail-refresh");
  const audio = document.getElementById("demo-audio");
  let artistId = null;
  let controller = null;
  let sequence = 0;
  let pending = false;
  let lastSuccess = 0;

  function stopAudio() {
    audio.pause();
    audio.removeAttribute("src");
    audio.load();
  }

  function clearPrivateData() {
    controller?.abort();
    sequence += 1;
    pending = false;
    lastSuccess = 0;
    detail.hidden = true;
    stopAudio();
    ["demo-title", "demo-subtitle", "original-file", "demo-note", "demo-number", "demo-genre",
      "demo-submitted", "demo-updated", "demo-size", "status-label", "status-copy", "record-number"].forEach(key => {
      document.getElementById(key).textContent = "";
    });
    document.getElementById("demo-download").removeAttribute("href");
    document.getElementById("detail-refreshed").textContent = "Times shown in your local time";
    document.title = "Demo Details — AFTER/FORM Artist Portal";
  }

  function showError(text, canRetry) {
    document.getElementById("detail-error-text").textContent = text;
    errorBox.hidden = false;
    retry.hidden = !canRetry;
  }

  function render(demo) {
    if (!demo || demo.id !== id || typeof demo.title !== "string") throw new Error("The demo response is incomplete.");
    const urls = ui.urls(id);
    const status = ui.status(demo.status);
    document.getElementById("demo-title").textContent = demo.title;
    document.getElementById("demo-subtitle").textContent = `${demo.genre} · Submission #${demo.id}`;
    document.getElementById("record-number").textContent = `AF—${demo.id}`;
    document.getElementById("original-file").textContent = demo.originalFileName;
    document.getElementById("demo-status").className = `status-line ${status.className}`;
    document.getElementById("status-label").textContent = status.label;
    document.getElementById("status-copy").textContent = status.copy;
    document.getElementById("demo-number").textContent = `#${demo.id}`;
    document.getElementById("demo-genre").textContent = demo.genre;
    document.getElementById("demo-submitted").textContent = ui.date(demo.submittedAtUtc, true);
    document.getElementById("demo-updated").textContent = demo.updatedAtUtc ? ui.date(demo.updatedAtUtc, true) : "No review update yet";
    document.getElementById("demo-size").textContent = ui.size(demo.fileSizeBytes);
    document.getElementById("demo-note").textContent = demo.description || "You didn’t add a note to this submission.";
    // A status refresh must not interrupt a recording that is playing.
    if (audio.getAttribute("src") !== urls.audio) {
      audio.src = urls.audio;
      document.getElementById("playback-note").hidden = true;
    }
    document.getElementById("demo-download").href = urls.download;
    document.title = `${demo.title} — AFTER/FORM Artist Portal`;
    detail.hidden = false;
    lastSuccess = Date.now();
    document.getElementById("detail-refreshed").textContent = `Updated ${new Date(lastSuccess).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })} · Times shown in your local time`;
  }

  async function load() {
    if (!artistId || pending) return;
    if (!validId) {
      loading.hidden = true;
      showError("This demo link is invalid. Open a recording from your demo history.", false);
      return;
    }
    controller = new AbortController();
    const signal = controller.signal;
    const currentSequence = ++sequence;
    const account = artistId;
    pending = true;
    refresh.disabled = retry.disabled = true;
    loading.textContent = lastSuccess ? "Refreshing the submission status…" : "Loading your recording…";
    loading.hidden = false;
    errorBox.hidden = true;
    detail.setAttribute("aria-busy", "true");
    try {
      const demo = await window.AfterformArtist.request(`/api/demos/${id}`, { signal });
      if (signal.aborted || currentSequence !== sequence || account !== artistId) return;
      if (typeof demo?.artistId !== "string") throw new Error("The demo response is incomplete.");
      if (demo.artistId !== account) {
        clearPrivateData();
        document.querySelector("[data-auth-content]").hidden = true;
        window.location.reload();
        return;
      }
      render(demo);
    } catch (error) {
      if (signal.aborted || currentSequence !== sequence) return;
      if (error.status === 401) {
        clearPrivateData();
        document.querySelector("[data-auth-content]").hidden = true;
        window.location.replace("/artist/login/");
        return;
      }
      if (error.status === 404) {
        clearPrivateData();
        showError("This submission is unavailable for your account. Return to your demo history to choose a recording.", true);
        loading.hidden = true;
        refresh.disabled = retry.disabled = false;
        detail.setAttribute("aria-busy", "false");
        return;
      }
      showError(`We couldn’t load this submission. ${lastSuccess ? "The details shown are from the last successful refresh. " : ""}Please try again.`, true);
    } finally {
      if (currentSequence === sequence) {
        pending = false;
        loading.hidden = true;
        refresh.disabled = retry.disabled = false;
        detail.setAttribute("aria-busy", "false");
      }
    }
  }

  refresh.addEventListener("click", load);
  retry.addEventListener("click", load);
  audio.addEventListener("error", () => {
    if (audio.hasAttribute("src")) document.getElementById("playback-note").hidden = false;
  });
  document.addEventListener("afterform:artist-ready", event => {
    const account = event.detail.id;
    if (typeof account !== "string" || !account) return;
    if (artistId !== null && artistId !== account) clearPrivateData();
    artistId = account;
    if (!pending && Date.now() - lastSuccess >= 10000) load();
  });
  setInterval(() => {
    if (!document.hidden && !document.querySelector("[data-auth-content]").hidden
      && !pending && Date.now() - lastSuccess >= 60000) load();
  }, 60000);
  window.addEventListener("pagehide", () => { clearPrivateData(); artistId = null; });
})();
