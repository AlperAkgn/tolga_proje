(function () {
  "use strict";

  const form = document.getElementById("demo-form");
  const fileInput = document.getElementById("audio-file");
  const dropzone = document.getElementById("dropzone");
  const fileName = document.getElementById("file-name");
  const dropTitle = document.getElementById("drop-title");
  const removeFile = document.getElementById("remove-file");
  const note = document.getElementById("artist-note");
  const noteCount = document.getElementById("note-count");
  const message = document.getElementById("form-message");
  const send = document.getElementById("send-demo");
  const sendLabel = document.getElementById("send-label");
  const progress = document.getElementById("upload-progress");
  const bar = document.getElementById("upload-bar");
  const state = document.getElementById("upload-state");
  const receipt = document.getElementById("saved-demo");
  const audio = document.getElementById("saved-audio");
  let limit = 100 * 1024 * 1024;
  let extensions = [".mp3", ".wav", ".flac"];
  let ready = false;
  let preparing = false;
  let pending = false;
  let artistId = null;
  let receiptLoaded = false;

  function showMessage(title, text, isError = false) {
    const heading = document.createElement("strong");
    heading.textContent = title;
    const copy = document.createElement("span");
    copy.textContent = text;
    message.replaceChildren(heading, copy);
    message.classList.toggle("is-error", isError);
    message.classList.add("is-visible");
  }

  function sizeText(bytes) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  function fileError(file) {
    if (!file) return "Choose one audio file to continue.";
    const extension = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!extensions.includes(extension)) return "Choose an MP3, WAV or FLAC file.";
    if (file.size === 0) return "This file is empty. Choose your original recording.";
    if (file.size > limit) return `Choose an audio file no larger than ${Math.round(limit / (1024 * 1024))} MB.`;
    return "";
  }

  function clearFile() {
    fileInput.value = "";
    fileName.textContent = "";
    fileName.style.color = "";
    dropTitle.textContent = "Drop your audio here or browse";
    dropzone.classList.remove("has-file");
    dropzone.removeAttribute("aria-invalid");
    removeFile.hidden = true;
  }

  function setFile(file) {
    if (!file) return;
    const error = fileError(file);
    clearFileDisplay();
    if (error) {
      fileInput.value = "";
      fileName.textContent = error;
      fileName.style.color = "#d9a49b";
      dropzone.setAttribute("aria-invalid", "true");
      return;
    }
    fileName.textContent = `${file.name} · ${sizeText(file.size)}`;
    dropTitle.textContent = "Audio file selected";
    dropzone.classList.add("has-file");
    removeFile.hidden = false;
  }

  function clearFileDisplay() {
    fileName.textContent = "";
    fileName.style.color = "";
    dropTitle.textContent = "Drop your audio here or browse";
    dropzone.classList.remove("has-file");
    dropzone.removeAttribute("aria-invalid");
    removeFile.hidden = true;
  }

  function setPending(value) {
    pending = value;
    form.setAttribute("aria-busy", String(value));
    Array.from(form.elements).forEach(element => { element.disabled = value; });
    send.disabled = value || !ready;
    sendLabel.textContent = value ? "Sending your demo…" : ready ? "Send demo" : "Preparing uploads…";
    dropzone.setAttribute("aria-disabled", String(value));
    dropzone.tabIndex = value ? -1 : 0;
    progress.hidden = !value;
  }

  function showReceipt(demo, announce) {
    if (!demo || !Number.isSafeInteger(demo.id) || demo.id < 1 || typeof demo.title !== "string" || demo.artistId !== artistId)
      throw new Error("The submission receipt is incomplete.");
    const urls = window.AfterformDemos.urls(demo.id);
    const receiptPath = `/artist/submit/?demo=${demo.id}`;
    document.getElementById("saved-status").textContent = window.AfterformDemos.status(demo.status).label.toUpperCase();
    document.getElementById("saved-title").textContent = demo.title;
    const date = window.AfterformDemos.date(demo.submittedAtUtc, true);
    document.getElementById("saved-detail").textContent =
      `Submission #${demo.id} · ${demo.genre} · ${date}\n${demo.originalFileName} · ${sizeText(demo.fileSizeBytes)}`;
    if (audio.getAttribute("src") !== urls.audio) {
      audio.pause();
      audio.src = urls.audio;
      document.getElementById("playback-note").hidden = true;
    }
    document.getElementById("saved-download").href = urls.download;
    document.getElementById("saved-receipt").href = urls.detail;
    receipt.hidden = false;
    receiptLoaded = true;
    if (announce) {
      window.history.replaceState(null, "", receiptPath);
      showMessage("Demo submitted", "Your recording and track information have been saved. You can follow its status in your demo history.");
      message.focus({ preventScroll: true });
      receipt.scrollIntoView({ behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth", block: "nearest" });
    }
  }

  async function prepareUploads(artist) {
    if (artistId !== null && artistId !== artist.id) {
      clearFile();
      receipt.hidden = true;
      audio.pause();
      audio.removeAttribute("src");
      audio.load();
      receiptLoaded = false;
    }
    artistId = artist.id;
    if (preparing || pending) return;
    preparing = true;
    try {
      if (!ready) {
        const options = await window.AfterformArtist.request("/api/demos/options");
        if (!options || !Number.isSafeInteger(options.maxFileSizeBytes) || options.maxFileSizeBytes <= 0
          || !Array.isArray(options.acceptedExtensions)) throw new Error("Upload options are unavailable.");
        limit = options.maxFileSizeBytes;
        extensions = options.acceptedExtensions;
        ready = true;
        send.disabled = false;
        sendLabel.textContent = "Send demo";
        message.classList.remove("is-visible");
      }
      const id = new URLSearchParams(window.location.search).get("demo");
      if (!receiptLoaded && id && /^[1-9]\d{0,17}$/.test(id)) {
        const demo = await window.AfterformArtist.request(`/api/demos/${id}`);
        showReceipt(demo, false);
      }
    } catch (error) {
      if (error.status === 401) { window.location.replace("/artist/login/"); return; }
      if (ready && error.status === 404) {
        receiptLoaded = true;
        showMessage("Receipt unavailable", "This submission is unavailable for your account.", true);
      } else {
        showMessage("Uploads unavailable", "We couldn’t prepare your upload or receipt. Please refresh the page to try again.", true);
      }
    } finally { preparing = false; }
  }

  function upload(body, csrf) {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open("POST", "/api/demos");
      xhr.timeout = 180000;
      xhr.withCredentials = true;
      xhr.setRequestHeader("Accept", "application/json");
      xhr.setRequestHeader("X-AF-CSRF", csrf);
      xhr.upload.onprogress = event => {
        if (event.lengthComputable) {
          const percent = Math.min(100, Math.round(event.loaded / event.total * 100));
          bar.value = percent;
          state.textContent = percent === 100 ? "Upload sent. Checking and saving your demo…" : `Uploading your recording · ${percent}%`;
        }
      };
      xhr.onload = () => {
        let data = null;
        try { data = JSON.parse(xhr.responseText); } catch { }
        if (xhr.status === 201 && data) resolve(data);
        else {
          const error = new Error("The upload could not be completed.");
          error.status = xhr.status;
          error.details = data;
          reject(error);
        }
      };
      xhr.onerror = xhr.ontimeout = xhr.onabort = () => reject(new Error("The upload response was interrupted."));
      // Let the browser set the multipart boundary and Content-Type.
      xhr.send(body);
    });
  }

  function errorText(error) {
    if (error.status === 400 && error.details && error.details.errors) {
      const entries = Object.values(error.details.errors).flat();
      if (typeof entries[0] === "string") return entries[0];
    }
    if (error.status === 400) return "We couldn’t verify this submission. Refresh the page and try again.";
    if (error.status === 413) return "The upload is too large. Choose a file no larger than 100 MB.";
    if (error.status === 429) return "You’ve reached the upload limit. Wait up to 10 minutes before sending another demo.";
    if (error.status >= 500) return "Demo services are temporarily unavailable. Keep your original file and try again shortly.";
    return "We couldn’t confirm the upload. It may have been saved; check your records before sending the same recording again.";
  }

  document.addEventListener("afterform:artist-ready", event => { prepareUploads(event.detail); });
  dropzone.addEventListener("click", event => {
    if (pending || event.target === fileInput || event.target.closest("#remove-file")) return;
    fileInput.click();
  });
  fileInput.addEventListener("change", () => setFile(fileInput.files[0]));
  removeFile.addEventListener("click", event => { event.preventDefault(); event.stopPropagation(); if (!pending) clearFile(); });
  ["dragenter", "dragover"].forEach(name => dropzone.addEventListener(name, event => {
    event.preventDefault();
    if (!pending) dropzone.classList.add("is-over");
  }));
  ["dragleave", "drop"].forEach(name => dropzone.addEventListener(name, event => {
    event.preventDefault();
    dropzone.classList.remove("is-over");
  }));
  dropzone.addEventListener("drop", event => {
    if (pending) return;
    if (event.dataTransfer.files.length !== 1) {
      clearFile();
      fileName.textContent = "Choose exactly one audio file.";
      fileName.style.color = "#d9a49b";
      dropzone.setAttribute("aria-invalid", "true");
      return;
    }
    const transfer = new DataTransfer();
    transfer.items.add(event.dataTransfer.files[0]);
    fileInput.files = transfer.files;
    setFile(fileInput.files[0]);
  });
  dropzone.addEventListener("keydown", event => {
    if (event.target !== dropzone || pending) return;
    if (event.key === "Enter" || event.key === " ") { event.preventDefault(); fileInput.click(); }
  });
  note.addEventListener("input", () => { noteCount.textContent = `${note.value.length} / 500`; });
  audio.addEventListener("error", () => { document.getElementById("playback-note").hidden = false; });
  window.addEventListener("beforeunload", event => {
    if (pending) { event.preventDefault(); event.returnValue = ""; }
  });
  window.addEventListener("pagehide", () => { audio.pause(); });

  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (pending || !ready) return;
    const title = document.getElementById("track-title");
    title.value = title.value.trim();
    if (!form.reportValidity()) return;
    const file = fileInput.files[0];
    const invalid = fileError(file);
    if (invalid) {
      fileName.textContent = invalid;
      fileName.style.color = "#d9a49b";
      dropzone.setAttribute("aria-invalid", "true");
      dropzone.focus();
      return;
    }
    const accountAtStart = artistId;
    const body = new FormData();
    body.append("trackTitle", title.value);
    body.append("genre", document.getElementById("genre").value);
    body.append("artistNote", note.value);
    body.append("rightsConfirmation", String(document.getElementById("rights-confirmation").checked));
    body.append("audioFile", file, file.name);
    message.classList.remove("is-visible");
    bar.value = 0;
    state.textContent = "Preparing your demo…";
    setPending(true);
    try {
      const current = await window.AfterformArtist.request("/api/auth/me");
      if (current.id !== accountAtStart) {
        showMessage("Account changed", "Refresh the page before submitting from your current artist account.", true);
        ready = false;
        return;
      }
      const csrf = await window.AfterformArtist.request("/api/auth/csrf");
      if (!csrf || typeof csrf.requestToken !== "string" || !csrf.requestToken) throw new Error("Session verification failed.");
      state.textContent = "Uploading your recording…";
      const demo = await upload(body, csrf.requestToken);
      if (artistId !== accountAtStart) {
        showMessage("Account changed", "The upload finished for the account that sent it. Refresh the page before continuing.", true);
        ready = false;
        return;
      }
      showReceipt(demo, true);
      form.reset();
      clearFile();
      noteCount.textContent = "0 / 500";
    } catch (error) {
      if (error.status === 401) { window.location.replace("/artist/login/"); return; }
      showMessage("Submission not confirmed", errorText(error), true);
      message.focus({ preventScroll: true });
    } finally { setPending(false); }
  });
})();
