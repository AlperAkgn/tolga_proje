(function () {
  "use strict";

  const page = document.body.dataset.authPage;
  const message = document.getElementById("authMessage");
  const retry = document.getElementById("authRetry");
  const content = document.querySelector("[data-auth-content]");
  const logoutButton = document.getElementById("artistLogout");
  const loginForm = document.getElementById("artistLoginForm");
  let sessionCheckInFlight = false;
  let lastSessionCheck = 0;
  let sessionChangeInFlight = false;

  function beginSessionCheck(background) {
    if (sessionCheckInFlight || sessionChangeInFlight) return false;
    if (background && (document.hidden || Date.now() - lastSessionCheck < 10000)) return false;
    sessionCheckInFlight = true;
    lastSessionCheck = Date.now();
    return true;
  }

  function onReturnToPage(check) {
    const recheck = () => {
      if (!document.hidden) check({ background: true });
    };
    document.addEventListener("visibilitychange", recheck);
    window.addEventListener("focus", recheck);
  }

  function showMessage(element, text, isError = false) {
    if (!element) return;
    element.textContent = text;
    element.classList.toggle("is-error", isError);
  }

  async function request(path, options = {}) {
    const { signal: callerSignal, ...requestOptions } = options;
    const controller = new AbortController();
    const cancel = () => controller.abort();
    if (callerSignal?.aborted) controller.abort();
    callerSignal?.addEventListener("abort", cancel, { once: true });
    const timeout = setTimeout(() => controller.abort(), 15000);
    try {
      const response = await fetch(path, {
        ...requestOptions,
        credentials: "same-origin",
        cache: "no-store",
        signal: controller.signal,
        headers: { Accept: "application/json", ...requestOptions.headers }
      });
      const data = response.status === 204 ? null : await response.json().catch(() => null);
      if (!response.ok) {
        const error = new Error("The request could not be completed.");
        error.status = response.status;
        error.details = data;
        throw error;
      }
      return data;
    } finally {
      clearTimeout(timeout);
      callerSignal?.removeEventListener("abort", cancel);
    }
  }

  // Request tokens stay only in this call. Session cookies are managed by the browser.
  async function changeSession(path, body) {
    const csrf = await request("/api/auth/csrf");
    if (!csrf || typeof csrf.requestToken !== "string" || !csrf.requestToken) {
      throw new Error("Session verification is unavailable.");
    }
    return request(path, {
      method: "POST",
      headers: {
        "X-AF-CSRF": csrf.requestToken,
        ...(body ? { "Content-Type": "application/json" } : {})
      },
      ...(body ? { body: JSON.stringify(body) } : {})
    });
  }

  window.AfterformArtist = Object.freeze({ request });

  function errorText(error, action) {
    if (action === "logout" && error.status === 429) return "Sign-out requests are temporarily limited. Please wait a little and try again.";
    if (action === "logout" && error.status === 400) return "We couldn’t verify the sign-out request. Please try signing out again.";
    if (error.status === 429) return "Too many sign-in attempts. Please wait a little before trying again.";
    if (error.status === 503) return "Artist services are temporarily unavailable. Please try again shortly.";
    if (error.status >= 500) return "Artist services encountered an error. Please try again shortly.";
    if (error.status === 400) return "Please check your details and try again. If this continues, refresh this page.";
    if (error.status === 401 && action === "login") return "Check your username and password. If you have tried several times, wait 15 minutes or contact the label.";
    return "We couldn’t connect to artist services. Please try again.";
  }

  function showArtist(artist) {
    if (!artist || typeof artist.userName !== "string") throw new Error("The artist response is incomplete.");
    const name = artist.displayName || artist.userName;
    const words = name.trim().split(/\s+/).filter(Boolean);
    const initials = words.slice(0, 2).map(word => Array.from(word)[0]).join("").toUpperCase();
    document.querySelectorAll("[data-artist-name]").forEach(element => { element.textContent = name; });
    document.querySelectorAll("[data-artist-username]").forEach(element => { element.textContent = `@${artist.userName}`; });
    document.querySelectorAll("[data-artist-initials]").forEach(element => { element.textContent = initials || "AF"; });
    document.querySelectorAll("[data-artist-card]").forEach(element => { element.setAttribute("aria-label", `Artist account: ${name}`); });
    document.dispatchEvent(new CustomEvent("afterform:artist-ready", { detail: artist }));
  }

  async function loadArtist({ background = false } = {}) {
    if (!beginSessionCheck(background)) return;
    if (!background) {
      if (content) content.hidden = true;
      if (retry) retry.hidden = true;
      if (logoutButton) logoutButton.disabled = true;
      showMessage(message, "Opening your artist space…");
    }
    try {
      const artist = await request("/api/auth/me");
      if (sessionChangeInFlight) return;
      showArtist(artist);
      if (content) content.hidden = false;
      if (logoutButton) logoutButton.disabled = false;
      if (retry) retry.hidden = true;
      showMessage(message, "");
    } catch (error) {
      if (sessionChangeInFlight) return;
      if (error.status === 401) {
        if (content) content.hidden = true;
        if (logoutButton) logoutButton.disabled = true;
        window.location.replace("/artist/login/");
        return;
      }
      showMessage(message, errorText(error, "session"), true);
      if (retry) retry.hidden = false;
    } finally {
      sessionCheckInFlight = false;
    }
  }

  if (page === "login" && loginForm) {
    const password = document.getElementById("password");
    const username = document.getElementById("username");
    const submit = loginForm.querySelector("[type=submit]");
    const submitLabel = submit.querySelector("[data-sign-in-label]");
    const loginMessage = document.getElementById("formMessage");
    let pending = true;

    function setPending(value, label = "Sign in") {
      pending = value;
      submit.disabled = value;
      username.readOnly = value;
      password.readOnly = value;
      loginForm.setAttribute("aria-busy", String(value));
      submitLabel.textContent = label;
    }

    async function checkExistingSession({ background = false } = {}) {
      if (background && pending) return;
      if (!beginSessionCheck(background)) return;
      if (!background) setPending(true, "Checking session…");
      try {
        await request("/api/auth/me");
        if (sessionChangeInFlight) return;
        window.location.replace("/artist/");
      } catch (error) {
        if (!background) {
          if (error.status !== 401) showMessage(loginMessage, errorText(error, "session"), true);
          setPending(false);
        }
      } finally {
        sessionCheckInFlight = false;
      }
    }

    loginForm.addEventListener("submit", async event => {
      event.preventDefault();
      if (pending || !loginForm.reportValidity()) return;
      sessionChangeInFlight = true;
      showMessage(loginMessage, "Signing you in…");
      username.removeAttribute("aria-invalid");
      password.removeAttribute("aria-invalid");
      setPending(true, "Signing in…");
      try {
        await changeSession("/api/auth/login", { userName: username.value.trim(), password: password.value });
        password.value = "";
        window.location.replace("/artist/");
      } catch (error) {
        showMessage(loginMessage, errorText(error, "login"), true);
        setPending(false);
        if (error.status === 401) {
          username.setAttribute("aria-invalid", "true");
          password.setAttribute("aria-invalid", "true");
          password.value = "";
          password.focus();
        } else {
          loginMessage.focus();
        }
      } finally {
        sessionChangeInFlight = false;
      }
    });

    checkExistingSession();
    onReturnToPage(checkExistingSession);
    window.addEventListener("pageshow", event => { if (event.persisted) checkExistingSession(); });
  } else if (page === "artist") {
    if (retry) retry.addEventListener("click", loadArtist);
    if (logoutButton) logoutButton.addEventListener("click", async () => {
      if (sessionChangeInFlight) return;
      sessionChangeInFlight = true;
      logoutButton.disabled = true;
      logoutButton.textContent = "Signing out…";
      showMessage(message, "Signing you out…");
      try {
        await changeSession("/api/auth/logout");
        window.location.replace("/artist/login/");
      } catch (error) {
        if (error.status === 401) {
          window.location.replace("/artist/login/");
          return;
        }
        showMessage(message, errorText(error, "logout"), true);
        logoutButton.disabled = false;
        logoutButton.textContent = "Sign out";
        message.focus();
      } finally {
        sessionChangeInFlight = false;
      }
    });
    loadArtist();
    onReturnToPage(loadArtist);
    window.addEventListener("pagehide", () => { if (content) content.hidden = true; });
    window.addEventListener("pageshow", event => { if (event.persisted) loadArtist(); });
  }
})();
