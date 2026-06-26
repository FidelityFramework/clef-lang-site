/**
 * Clef Smart Search — hybrid BM25 + vector search client
 * Replaces Hextra's FlexSearch with server-side search via Cloudflare Workers
 */
(function () {
  "use strict";

  const SEARCH_URL = window.CLEF_SEARCH_URL || "https://clef-search.engineering-0c5.workers.dev";
  const DEBOUNCE_MS = 300;
  const MAX_RESULTS = 20;

  // DOM refs (resolved lazily)
  let modal, input, results, stats, synthesizeBtn, synthesisArea, synthesisContent, shareBtn, clearBtn;

  function $(id) { return document.getElementById(id); }

  function ensureRefs() {
    if (modal) return;
    modal = $("clef-search-modal");
    input = $("clef-search-input");
    results = $("clef-search-results");
    stats = $("clef-search-stats");
    synthesizeBtn = $("clef-search-synthesize-btn");
    synthesisArea = $("clef-search-synthesis");
    synthesisContent = $("clef-synthesis-content");
    shareBtn = $("clef-search-share-btn");
    clearBtn = $("clef-search-clear-btn");
  }

  /** Keep the stateful action buttons in sync with the modal's state:
   *  - Share / Clear: active only when the search box has text.
   *  - Summarize with AI: active only when there are search results to summarize.
   *  Exit is always active. */
  function updateActionButtons() {
    const hasText = !!(input && input.value.trim());
    const hasResults = currentResults.length > 0;
    if (shareBtn) shareBtn.disabled = !hasText;
    if (clearBtn) clearBtn.disabled = !hasText;
    if (synthesizeBtn) synthesizeBtn.disabled = !hasResults;
  }

  // ── State ──────────────────────────────────────────────────

  let debounceTimer = null;
  let selectedIndex = -1;
  let currentResults = [];
  let abortController = null;
  let lastQuery = "";
  // Preserved across close/reopen so the modal rehydrates instead of starting
  // blank. Cleared only by the explicit Clear button.
  let lastResultsHtml = "";
  let lastStatsText = "";
  let lastSynthesisHtml = "";
  // True only when the panel holds a genuine, successful summary (not an error or
  // timeout message). Gates whether a shared link carries summarize=yes.
  let lastSynthesisOk = false;

  // ── Helpers ────────────────────────────────────────────────

  function escapeHtml(s) {
    const el = document.createElement("span");
    el.textContent = s;
    return el.innerHTML;
  }

  /** Lightweight markdown-to-HTML for synthesis output */
  function renderMarkdown(text) {
    let html = escapeHtml(text);
    // Bold: **text**
    html = html.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
    // Italic: *text* (but not inside already-processed bold)
    html = html.replace(/(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)/g, "<em>$1</em>");
    // Inline code: `text`
    html = html.replace(/`([^`]+)`/g, "<code>$1</code>");
    // Paragraphs: double newline
    html = html.replace(/\n\n+/g, "</p><p>");
    // Single newline -> <br>
    html = html.replace(/\n/g, "<br>");
    return "<p>" + html + "</p>";
  }

  const TYPE_LABELS = {
    blog: "Blog",
    design: "Design",
    internals: "Internals",
    reference: "Reference",
    guides: "Guides",
    spec: "Spec",
  };

  const TYPE_COLORS = {
    blog: "#2563eb",
    design: "#7c3aed",
    internals: "#059669",
    reference: "#d97706",
    guides: "#0891b2",
    spec: "#dc2626",
  };

  function typeBadge(type) {
    const label = TYPE_LABELS[type] || type;
    const color = TYPE_COLORS[type] || "#6b7280";
    return `<span class="clef-result-type" style="--badge-color: ${color}">${escapeHtml(label)}</span>`;
  }

  function truncate(s, max) {
    if (!s || s.length <= max) return s || "";
    return s.substring(0, max) + "\u2026";
  }

  /** Generate a Hugo/Goldmark-compatible heading anchor from a section title */
  function headingAnchor(title) {
    if (!title || title === "Introduction") return "";
    return "#" + title
      .toLowerCase()
      .replace(/[^\w\s-]/g, "")
      .replace(/\s+/g, "-")
      .replace(/-+/g, "-")
      .replace(/^-|-$/g, "");
  }

  // ── Search API ─────────────────────────────────────────────

  async function fetchSearch(query) {
    if (abortController) abortController.abort();
    abortController = new AbortController();

    const resp = await fetch(`${SEARCH_URL}/search/hybrid`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ query, limit: MAX_RESULTS }),
      signal: abortController.signal,
    });
    if (!resp.ok) throw new Error(`Search failed: ${resp.status}`);
    return resp.json();
  }

  // ── Render ─────────────────────────────────────────────────

  function renderResults(data) {
    currentResults = data.results || [];
    selectedIndex = -1;

    if (currentResults.length === 0) {
      results.innerHTML = `<div class="clef-search-empty">No results found</div>`;
      stats.textContent = "";
      updateActionButtons();
      return;
    }

    const items = currentResults.map((r, i) =>
      `<a href="${escapeHtml(r.pageUrl + headingAnchor(r.sectionTitle))}" class="clef-search-result" data-index="${i}">
        <div class="clef-result-header">
          ${typeBadge(r.contentType)}
          <span class="clef-result-title">${escapeHtml(r.pageTitle)}</span>
        </div>
        ${r.sectionTitle ? `<span class="clef-result-section">${escapeHtml(r.sectionTitle)}</span>` : ""}
        <p class="clef-result-snippet">${escapeHtml(truncate(r.snippet, 200))}</p>
      </a>`
    ).join("");

    results.innerHTML = items;
    const ms = data.searchTimeMs != null ? ` in ${data.searchTimeMs}ms` : "";
    stats.textContent = `${currentResults.length} result${currentResults.length !== 1 ? "s" : ""}${ms}`;
    updateActionButtons();

    // Snapshot for rehydration on reopen.
    lastResultsHtml = results.innerHTML;
    lastStatsText = stats.textContent;
  }

  function updateSelection() {
    const items = results.querySelectorAll(".clef-search-result");
    items.forEach((el, i) => {
      el.classList.toggle("clef-result-selected", i === selectedIndex);
    });
    if (selectedIndex >= 0 && items[selectedIndex]) {
      items[selectedIndex].scrollIntoView({ block: "nearest" });
    }
  }

  // ── Synthesis (SSE streaming) ──────────────────────────────

  async function startSynthesis() {
    const query = input.value.trim();
    // No-op without a query or results — the button is disabled in this state,
    // but the summarize=yes link path calls this directly, so guard here too.
    if (!query || currentResults.length === 0) return;

    synthesisArea.style.display = "";
    synthesisContent.innerHTML = '<span class="clef-synthesis-spinner"></span> Generating summary...';
    // Any prior success is invalidated until this attempt resolves successfully.
    lastSynthesisOk = false;
    lastSynthesisHtml = "";

    // Outer cap so the spinner can't spin forever. Set above the worker's own
    // ~28s AI-timeout so the worker's clean "unavailable" error wins the race and
    // the user sees a real message rather than a raw fetch abort.
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), 35000);

    try {
      const resp = await fetch(`${SEARCH_URL}/synthesize-stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ query, limit: 5 }),
        signal: controller.signal,
      });

      if (!resp.ok) {
        synthesisContent.textContent = "Synthesis unavailable.";
        return;
      }

      const data = await resp.json();
      if (data.synthesis) {
        synthesisContent.innerHTML = renderMarkdown(data.synthesis);
        // Only a real summary is snapshotted and marked shareable.
        lastSynthesisHtml = synthesisContent.innerHTML;
        lastSynthesisOk = true;
      } else if (data.error) {
        synthesisContent.textContent = data.error;
      } else {
        synthesisContent.textContent = "No synthesis available for this query.";
      }
    } catch (err) {
      if (err.name === "AbortError") {
        synthesisContent.textContent = "Summary timed out. Try again.";
      } else {
        synthesisContent.textContent = "Error generating summary.";
      }
    } finally {
      window.clearTimeout(timeoutId);
    }
  }

  // ── Event handlers ─────────────────────────────────────────

  /** Hide and forget the AI summary — it belongs to the previous query. */
  function dropSynthesis() {
    synthesisArea.style.display = "none";
    synthesisContent.textContent = "";
    lastSynthesisHtml = "";
    lastSynthesisOk = false;
  }

  /** Run a search immediately (no debounce) and render. Shared by the input
   *  handler's debounced path and the shared-link hydration path. */
  async function runSearch(query) {
    try {
      results.innerHTML = `<div class="clef-search-loading">Searching…</div>`;
      const data = await fetchSearch(query);
      // Only render if the box still holds this query (guards against races).
      if (input.value.trim() === query) {
        renderResults(data);
      }
    } catch (err) {
      if (err.name !== "AbortError") {
        results.innerHTML = `<div class="clef-search-empty">Search error - try again</div>`;
      }
    }
  }

  function onInput() {
    const query = input.value.trim();

    // A new search phrase invalidates any summary from the prior query.
    if (query !== lastQuery && lastSynthesisHtml) {
      dropSynthesis();
    }
    lastQuery = query;

    if (debounceTimer) clearTimeout(debounceTimer);

    if (!query) {
      results.innerHTML = `<div class="clef-search-empty">Type to search across documentation, design docs, and blog posts</div>`;
      stats.textContent = "";
      currentResults = [];
      selectedIndex = -1;
      updateActionButtons();
      return;
    }

    // Share/Clear become active as soon as there's text; Summarize waits for results.
    updateActionButtons();
    debounceTimer = setTimeout(() => runSearch(query), DEBOUNCE_MS);
  }

  function onKeyDown(e) {
    const items = results.querySelectorAll(".clef-search-result");
    const count = items.length;

    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        selectedIndex = count > 0 ? (selectedIndex + 1) % count : -1;
        updateSelection();
        break;
      case "ArrowUp":
        e.preventDefault();
        selectedIndex = count > 0 ? (selectedIndex - 1 + count) % count : -1;
        updateSelection();
        break;
      case "Enter":
        e.preventDefault();
        if (selectedIndex >= 0 && items[selectedIndex]) {
          items[selectedIndex].click();
        }
        break;
      case "Escape":
        e.preventDefault();
        close();
        break;
    }
  }

  // ── Open / Close ───────────────────────────────────────────

  function isOpen() {
    return modal && modal.style.display !== "none";
  }

  function open() {
    ensureRefs();
    modal.style.display = "";

    // Rehydrate the last session so reopening doesn't feel like starting over:
    // restore the query, the results list, the stats, and the AI summary if any.
    if (lastQuery) {
      input.value = lastQuery;
      if (lastResultsHtml) {
        results.innerHTML = lastResultsHtml;
        stats.textContent = lastStatsText;
      }
      if (lastSynthesisHtml) {
        synthesisArea.style.display = "";
        synthesisContent.innerHTML = lastSynthesisHtml;
      }
    } else {
      results.innerHTML = `<div class="clef-search-empty">Type to search across documentation, design docs, and blog posts</div>`;
      stats.textContent = "";
      synthesisArea.style.display = "none";
    }
    updateActionButtons();
    selectedIndex = -1;
    // Focus after paint so transition works; place caret at end of restored text.
    requestAnimationFrame(() => {
      input.focus();
      const len = input.value.length;
      input.setSelectionRange(len, len);
    });
  }

  function close() {
    ensureRefs();
    modal.style.display = "none";
    if (abortController) abortController.abort();
    if (debounceTimer) clearTimeout(debounceTimer);
    // Intentionally preserve query/results/synthesis state for the next open().
  }

  /** Deliberate reset of the input and all results — the Clear button. */
  function clearSearch() {
    ensureRefs();
    if (abortController) abortController.abort();
    if (debounceTimer) clearTimeout(debounceTimer);
    input.value = "";
    lastQuery = "";
    currentResults = [];
    selectedIndex = -1;
    lastResultsHtml = "";
    lastStatsText = "";
    results.innerHTML = `<div class="clef-search-empty">Type to search across documentation, design docs, and blog posts</div>`;
    stats.textContent = "";
    dropSynthesis();
    updateActionButtons();
    input.focus();
  }

  // User dismissed the summary with the × — drop it so it doesn't return on reopen.
  function closeSynthesis() {
    ensureRefs();
    dropSynthesis();
  }

  // ── Share ──────────────────────────────────────────────────

  const SHARE_PARAM = "q";
  const SUMMARIZE_PARAM = "summarize";

  /** Build a shareable absolute URL carrying the query as ?q=, and ?summarize=yes
   *  when the recipient should also get the AI summary on arrival. */
  function buildShareUrl(query, withSummary) {
    const url = new URL(window.location.href);
    url.search = "";
    url.hash = "";
    url.searchParams.set(SHARE_PARAM, query);
    if (withSummary) url.searchParams.set(SUMMARIZE_PARAM, "yes");
    return url.toString();
  }

  /** Is a SUCCESSFUL AI summary currently displayed? Drives whether a shared link
   *  carries summarize=yes. Only a genuine summary that is still on screen qualifies;
   *  an error/timeout message or a dismissed panel does not, so a shared link never
   *  propagates a failed-summary intent. */
  function isSynthesisShown() {
    return lastSynthesisOk
      && synthesisArea && synthesisArea.style.display !== "none"
      && !!lastSynthesisHtml;
  }

  /** Copy a shareable link to the current query, with brief button feedback. */
  async function shareSearch() {
    ensureRefs();
    const query = input.value.trim();
    if (!query) return;

    const withSummary = isSynthesisShown();

    const btn = $("clef-search-share-btn");
    const shareUrl = buildShareUrl(query, withSummary);

    const confirm = (label) => {
      if (!btn) return;
      const original = btn.dataset.label || btn.textContent;
      btn.dataset.label = original;
      btn.textContent = label;
      btn.classList.add("clef-share-copied");
      window.setTimeout(() => {
        btn.textContent = btn.dataset.label;
        btn.classList.remove("clef-share-copied");
      }, 1500);
    };

    try {
      await navigator.clipboard.writeText(shareUrl);
      confirm("Copied!");
    } catch (err) {
      // Clipboard API unavailable (insecure context / older browser): fall back
      // to a hidden textarea + execCommand so the feature still works.
      try {
        const ta = document.createElement("textarea");
        ta.value = shareUrl;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        document.execCommand("copy");
        document.body.removeChild(ta);
        confirm("Copied!");
      } catch (_) {
        confirm("Copy failed");
      }
    }
  }

  /** On load, if the URL carries ?q=, open the modal, run that search, then strip
   *  the param so the address bar stays clean (the link itself still works). */
  async function hydrateFromUrl() {
    let query = null;
    let wantSummary = false;
    try {
      const params = new URL(window.location.href).searchParams;
      query = params.get(SHARE_PARAM);
      wantSummary = params.get(SUMMARIZE_PARAM) === "yes";
    } catch (_) {
      return;
    }
    if (!query) return;
    query = query.trim();
    if (!query) return;

    // Remove ?q= (and ?summarize=) from the address bar without a navigation or
    // history entry; the link itself still works.
    try {
      const cleaned = new URL(window.location.href);
      cleaned.searchParams.delete(SHARE_PARAM);
      cleaned.searchParams.delete(SUMMARIZE_PARAM);
      window.history.replaceState({}, "", cleaned.pathname + cleaned.hash);
    } catch (_) { /* non-fatal: leave the param if replaceState is unavailable */ }

    open();
    input.value = query;
    lastQuery = query;
    updateActionButtons();
    await runSearch(query);
    // A summarize=yes link runs the AI summary once results are in.
    if (wantSummary) startSynthesis();
  }

  // ── Init ───────────────────────────────────────────────────

  function init() {
    ensureRefs();
    if (!modal) return;

    input.addEventListener("input", onInput);
    input.addEventListener("keydown", onKeyDown);

    // Global Ctrl+K / Cmd+K toggle, and Escape to close from anywhere in the modal.
    document.addEventListener("keydown", (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        if (isOpen()) {
          close();
        } else {
          open();
        }
      } else if (e.key === "Escape" && isOpen()) {
        e.preventDefault();
        close();
      }
    });

    // Click on result navigates
    results.addEventListener("click", (e) => {
      const link = e.target.closest(".clef-search-result");
      if (link) {
        close();
        // Default anchor navigation will handle it
      }
    });

    // A shared ?q= link opens and runs the search on arrival.
    hydrateFromUrl();
  }

  // ── Public API ─────────────────────────────────────────────

  window.clefSearch = {
    open,
    close,
    clear: clearSearch,
    share: shareSearch,
    closeSynthesis,
    synthesize: startSynthesis,
  };

  // Init on DOM ready
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
