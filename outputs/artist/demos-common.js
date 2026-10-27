(function () {
  "use strict";
  const statuses = Object.freeze({
    Submitted: { label: "Submitted", className: "status-submitted", copy: "Your recording has been received and is awaiting review." },
    InReview: { label: "In review", className: "status-review", copy: "Your submission is currently being reviewed by the label." },
    Accepted: { label: "Accepted", className: "status-accepted", copy: "This submission has been accepted by the label." },
    Rejected: { label: "Rejected", className: "status-declined", copy: "The label has declined this submission." }
  });
  function status(value) {
    return statuses[value] || { label: "Status unavailable", className: "status-unknown", copy: "Refresh to check this submission’s current status." };
  }
  function date(value, withTime = false) {
    const instant = new Date(value);
    if (!value || !Number.isFinite(instant.getTime())) return "Date unavailable";
    return instant.toLocaleString(undefined, { dateStyle: "medium", ...(withTime ? { timeStyle: "short" } : {}) });
  }
  function size(value) {
    if (!Number.isFinite(value) || value < 0) return "Size unavailable";
    return value < 1024 * 1024 ? `${Math.max(1, Math.round(value / 1024))} KB` : `${(value / (1024 * 1024)).toFixed(1)} MB`;
  }
  function urls(id) {
    if (!Number.isSafeInteger(id) || id < 1) throw new Error("Invalid demo identifier.");
    return { detail: `/artist/demo/?id=${id}`, audio: `/api/demos/${id}/audio`, download: `/api/demos/${id}/audio?download=true` };
  }
  window.AfterformDemos = Object.freeze({ status, date, size, urls });
})();
