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
  let modal, input, results, stats, synthesizeBtn, synthesisArea, synthesisContent;

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
  }

  // ── State ──────────────────────────────────────────────────

  let debounceTimer = null;
  let selectedIndex = -1;
  let currentResults = [];
  let abortController = null;
  let lastQuery = "";

  // ── Helpers ────────────────────────────────────────────────

  function escapeHtml(s) {
    const el = document.createElement("span");
    el.textContent = s;
    return el.innerHTML;
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
      synthesizeBtn.style.display = "none";
      return;
    }

    const items = currentResults.map((r, i) =>
      `<a href="${escapeHtml(r.pageUrl)}" class="clef-search-result" data-index="${i}">
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
    synthesizeBtn.style.display = "";
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
    if (!query) return;

    synthesisArea.style.display = "";
    synthesisContent.textContent = "";

    try {
      const resp = await fetch(`${SEARCH_URL}/synthesize-stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ query, limit: 10 }),
      });

      if (!resp.ok) {
        synthesisContent.textContent = "Synthesis unavailable.";
        return;
      }

      const reader = resp.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop(); // keep incomplete line

        for (const line of lines) {
          if (line.startsWith("data: ")) {
            const payload = line.slice(6);
            if (payload === "[DONE]") break;
            try {
              const obj = JSON.parse(payload);
              if (obj.text) {
                synthesisContent.textContent += obj.text;
              } else if (obj.response) {
                synthesisContent.textContent += obj.response;
              }
            } catch {
              // Non-JSON data line — append as text
              if (payload.trim()) {
                synthesisContent.textContent += payload;
              }
            }
          }
        }
      }

      if (!synthesisContent.textContent.trim()) {
        synthesisContent.textContent = "No synthesis available for this query.";
      }
    } catch (err) {
      if (err.name !== "AbortError") {
        synthesisContent.textContent = "Error generating summary.";
      }
    }
  }

  // ── Event handlers ─────────────────────────────────────────

  function onInput() {
    const query = input.value.trim();
    lastQuery = query;

    if (debounceTimer) clearTimeout(debounceTimer);

    if (!query) {
      results.innerHTML = `<div class="clef-search-empty">Type to search across documentation, design docs, and blog posts</div>`;
      stats.textContent = "";
      synthesizeBtn.style.display = "none";
      currentResults = [];
      selectedIndex = -1;
      return;
    }

    debounceTimer = setTimeout(async () => {
      try {
        results.innerHTML = `<div class="clef-search-loading">Searching\u2026</div>`;
        const data = await fetchSearch(query);
        // Only render if query hasn't changed during fetch
        if (input.value.trim() === query) {
          renderResults(data);
        }
      } catch (err) {
        if (err.name !== "AbortError") {
          results.innerHTML = `<div class="clef-search-empty">Search error — try again</div>`;
        }
      }
    }, DEBOUNCE_MS);
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

  function open() {
    ensureRefs();
    modal.style.display = "";
    input.value = "";
    results.innerHTML = `<div class="clef-search-empty">Type to search across documentation, design docs, and blog posts</div>`;
    stats.textContent = "";
    synthesizeBtn.style.display = "none";
    synthesisArea.style.display = "none";
    currentResults = [];
    selectedIndex = -1;
    // Focus after paint so transition works
    requestAnimationFrame(() => input.focus());
  }

  function close() {
    ensureRefs();
    modal.style.display = "none";
    if (abortController) abortController.abort();
    if (debounceTimer) clearTimeout(debounceTimer);
  }

  function closeSynthesis() {
    ensureRefs();
    synthesisArea.style.display = "none";
    synthesisContent.textContent = "";
  }

  // ── Init ───────────────────────────────────────────────────

  function init() {
    ensureRefs();
    if (!modal) return;

    input.addEventListener("input", onInput);
    input.addEventListener("keydown", onKeyDown);

    // Global Ctrl+K / Cmd+K
    document.addEventListener("keydown", (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        if (modal.style.display === "none") {
          open();
        } else {
          close();
        }
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
  }

  // ── Public API ─────────────────────────────────────────────

  window.clefSearch = {
    open,
    close,
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
