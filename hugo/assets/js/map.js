/* Clef Corpus Map — a navigable graph of the body of work, peer to Smart Search.
   Mirrors the clefSearch modal lifecycle (open/close/Esc). Cytoscape loads lazily on first open. */
(function () {
  "use strict";

  var CY_SRC = "https://cdnjs.cloudflare.com/ajax/libs/cytoscape/3.30.2/cytoscape.min.js";
  var GRAPH_URL = window.CLEF_GRAPH_URL || "/graph.json";

  // Two layers are chunky enough to sub-band into concentric tiers:
  //  - SPEC splits into 3 tiers by its taxonomy category (collapsed 6 categories → 3):
  //      foundations (Process+Language) → meaning (Semantics+Representation) → machinery
  //      (Compiler+Platform). Inside-out: what the language IS → what it MEANS → how it COMPILES.
  //  - DOCS splits into 3 by top-level group: design → internals → tooling/guides.
  // bandOf() derives the band; color + radial level key off it, not the bare layer.
  var SPEC_TIER = {
    Process: "spec-foundations", Language: "spec-foundations",
    Semantics: "spec-meaning", Representation: "spec-meaning",
    Compiler: "spec-machinery", Platform: "spec-machinery",
  };
  function bandOf(data) {
    if (data.layer === "spec") {
      return SPEC_TIER[data.category] || "spec-meaning"; // uncategorized → middle tier
    }
    if (data.layer !== "docs") return data.layer;
    // top-group from contentType (live worker) or the id path (static preview)
    var g = data.contentType;
    if (!g || g === "docs") {
      var parts = (data.id || "").split("/").filter(Boolean); // ["docs","design",...]
      g = parts[1] || "";
    }
    if (g === "design") return "docs-design";
    if (g === "internals") return "docs-internals";
    return "docs-tooling"; // tooling, guides, reference
  }
  // band -> color. Spec sub-bands are three shades of the orange family; docs three of blue.
  var COLOR = {
    preprint: "#e3b341", external: "#8b949e",
    "spec-foundations": "#f78166", "spec-meaning": "#e8643f", "spec-machinery": "#c4451f",
    "docs-design": "#58a6ff", "docs-internals": "#388bfd", "docs-tooling": "#1f6feb",
    blog: "#3fb950",
  };
  // radial rings, center → out. Docs tiers are ordered SMALLEST-count inner → LARGEST outer
  // (tooling ~8 inner, internals ~35, design ~66 outer) so each ring's node count matches its
  // circumference — the balanced distribution that makes the spec tiers read cleanly. The
  // conceptual inside-out story for docs is carried by color, not radius.
  var LEVEL = {
    preprint: 1, external: 2,
    "spec-foundations": 3, "spec-meaning": 4, "spec-machinery": 5,
    "docs-tooling": 6, "docs-internals": 7, "docs-design": 8,
    blog: 9,
  };

  var cy = null, loadingCy = null;

  // The frozen sub-graph the user has built (set of node IDs), persisted across navigation
  // so resuming the Atlas restores the custom graph they left. Lives at module scope because
  // the modal re-renders a fresh cy instance on every open.
  var FROZEN_KEY = "clefAtlasFrozen";
  var frozen = loadState();

  // The last node the user clicked through to (the "you were here" anchor). Persisted separately
  // from the frozen set — a single-node breadcrumb, independent of the custom frozen sub-graph.
  var ANCHOR_KEY = "clefAtlasAnchor";
  var anchorId = loadAnchor();

  // Whether the Atlas has been revealed once this session. The fade-up reveal plays only on the
  // FIRST open per session; after that the graph is simply present on open (no reveal animation),
  // so a returning user does not re-orient to the same node positions every time.
  var SEEN_KEY = "clefAtlasSeen";
  function hasBeenSeen() {
    try { return sessionStorage.getItem(SEEN_KEY) === "1"; } catch (e) { return false; }
  }
  function markSeen() {
    try { sessionStorage.setItem(SEEN_KEY, "1"); } catch (e) {}
  }

  function loadState() {
    try {
      var raw = sessionStorage.getItem(FROZEN_KEY);
      return new Set(raw ? JSON.parse(raw) : []);
    } catch (e) { return new Set(); }
  }
  function persistState() {
    try { sessionStorage.setItem(FROZEN_KEY, JSON.stringify(Array.from(frozen))); } catch (e) {}
  }
  function loadAnchor() {
    try { return sessionStorage.getItem(ANCHOR_KEY) || null; } catch (e) { return null; }
  }
  function setAnchor(id) {
    anchorId = id;
    try { if (id) sessionStorage.setItem(ANCHOR_KEY, id); else sessionStorage.removeItem(ANCHOR_KEY); } catch (e) {}
  }
  function updateFreezeUI() {
    // reflect the frozen count on the Clear button + show/hide it
    var btn = document.querySelector("#clef-map-modal [data-map-clear]");
    if (btn) {
      btn.textContent = frozen.size ? ("Clear (" + frozen.size + ")") : "Clear";
      btn.style.opacity = frozen.size ? "1" : "0.55";
    }
  }

  // Cue on re-hydrate with an active selection but no frozen set (a restored anchor the user could
  // reset): show "Clear (1)" and pulse it twice to draw the eye, so a returning user knows Clear
  // will reset the highlighted link. The COUNT STAYS — it is a persistent affordance until the user
  // actually clicks Clear (which resets everything). Only the pulse animation is transient. A
  // frozen set is NOT cued here — it already carries its own persistent count.
  var cueTimer = null;
  function cueClear() {
    var btn = document.querySelector("#clef-map-modal [data-map-clear]");
    if (!btn || frozen.size) return;
    if (cueTimer) { clearTimeout(cueTimer); cueTimer = null; }
    btn.textContent = "Clear (1)";
    btn.style.opacity = "1";
    btn.classList.add("clef-map-btn-cue");
    // Strip only the pulse class once the animation is done; leave "Clear (1)" in place.
    cueTimer = setTimeout(function () {
      btn.classList.remove("clef-map-btn-cue");
      cueTimer = null;
    }, 1300);
  }

  function modal() { return document.getElementById("clef-map-modal"); }
  function isOpen() { var m = modal(); return m && m.style.display !== "none"; }

  function loadCytoscape() {
    if (window.cytoscape) return Promise.resolve();
    if (loadingCy) return loadingCy;
    loadingCy = new Promise(function (res, rej) {
      var s = document.createElement("script");
      s.src = CY_SRC; s.onload = res; s.onerror = rej;
      document.head.appendChild(s);
    });
    return loadingCy;
  }

  function tip(html, x, y) {
    var t = document.getElementById("clef-map-tip");
    if (!html) { t.style.display = "none"; return; }
    t.innerHTML = html; t.style.display = "block";
    t.style.left = (x + 16) + "px"; t.style.top = (y + 16) + "px";
  }

  // animate is opt-in per call: the INITIAL render places nodes instantly (no corner-to-ring
  // flight — the reveal is a CSS fade-up on the container instead), but a user-triggered layout
  // TOGGLE (Concentric↔Grouped) animates so the rearrange reads as a deliberate change.
  function buildLayout(name, indeg, animate) {
    if (name === "grouped") {
      var ord = {
        preprint: 900, external: 800,
        "spec-foundations": 700, "spec-meaning": 600, "spec-machinery": 500,
        "docs-tooling": 400, "docs-internals": 300, "docs-design": 200, blog: 100,
      };
      return { name: "concentric", concentric: function (n) { return ord[bandOf(n.data())] + (10 - Math.min(9, indeg[n.data("id")] || 0)); },
               levelWidth: function () { return 3; }, minNodeSpacing: 9, spacingFactor: 1.0, animate: !!animate, animationDuration: 500 };
    }
    // 9 bands, preprint(1)…blog(9). Spread the concentric values widely (×10) and force a
    // small levelWidth so each band gets its OWN ring — at gap-of-1 Cytoscape was bucketing
    // adjacent crowded docs tiers (design 66 + internals 35) into one ring, collapsing the
    // three docs tiers into two. Higher value = innermost, so invert.
    return { name: "concentric", concentric: function (n) { return (10 - LEVEL[bandOf(n.data())]) * 10; },
             levelWidth: function () { return 5; }, minNodeSpacing: 10, spacingFactor: 0.9, animate: !!animate, animationDuration: 500 };
  }

  function render(g) {
    var indeg = {};
    g.edges.forEach(function (e) { indeg[e.data.target] = (indeg[e.data.target] || 0) + 1; });
    var alldeg = {};
    g.edges.forEach(function (e) { alldeg[e.data.target] = (alldeg[e.data.target] || 0) + 1; alldeg[e.data.source] = (alldeg[e.data.source] || 0) + 1; });
    g.nodes.forEach(function (n) { n.data.iso = (alldeg[n.data.id] || 0) === 0; });

    var dark = document.documentElement.classList.contains("dark");
    cy = window.cytoscape({
      container: document.getElementById("clef-map-cy"),
      elements: g,
      style: [
        { selector: "node", style: {
          "background-color": function (e) { return COLOR[bandOf(e.data())] || "#888"; },
          "width": function (e) { var d = indeg[e.data("id")] || 0; var b = e.data("layer") === "preprint" ? 16 : 8; return b + Math.min(30, d * 2.2); },
          "height": function (e) { var d = indeg[e.data("id")] || 0; var b = e.data("layer") === "preprint" ? 16 : 8; return b + Math.min(30, d * 2.2); },
          "border-width": 0.5,
          "border-color": dark ? "#0d1117" : "#fff" } },
        { selector: "node[?iso]", style: { "opacity": 0.25 } },
        { selector: 'edge[type="href"]', style: { "width": 0.4, "line-color": dark ? "#484f58" : "#d0d7de", "curve-style": "haystack", "opacity": 0.45 } },
        { selector: 'edge[type="cites"]', style: { "width": 0.8, "line-color": "#e3b341", "curve-style": "haystack", "opacity": 0.55 } },
        // Theme edges (shared specific tag). Hidden by default — the "Themes" toggle reveals them,
        // at an opacity readable enough to show clustering rather than the near-invisible 0.2 wash.
        { selector: 'edge[type="tag"]', style: { "width": 0.5, "line-color": "#bc8cff", "line-style": "dashed", "curve-style": "haystack", "opacity": 0.35, "display": "none" } },
        { selector: "node:selected", style: { "border-width": 2.5, "border-color": dark ? "#e6edf3" : "#1f2328" } },
        // Cascade (later selectors win): faded states first, then the lit frozen set, then the
        // brightest live-hover on top.
        // .restdim = the PERSISTENT fade of everything outside the frozen sub-graph (resting
        // state once a set is frozen). .dim = the transient fade during a live hover. Same look.
        { selector: ".restdim", style: { "opacity": 0.05 } },
        { selector: ".dim", style: { "opacity": 0.05 } },
        // Frozen sub-graph = the active working set: lit (full color), nodes gold-ringed, edges
        // emphasized. Overrides the fades so it stays bright while the rest is dimmed.
        { selector: "node.frznode", style: { "opacity": 1, "border-width": 2, "border-color": "#e3b341" } },
        { selector: "edge.frz", style: { "opacity": 0.9, "width": 1.2, "line-color": "#e3b341", "line-style": "solid" } },
        // Live hover: brightest, layered on top of whatever resting state exists.
        { selector: ".hi", style: { "opacity": 0.95, "width": 1.4, "line-color": dark ? "#e6edf3" : "#1f2328" } }
      ],
      layout: buildLayout("concentric", indeg, false)
    });

    // ── Freeze: the frozen set becomes the working sub-graph ──────────────────────────────
    // When a frozen set exists, it IS the resting state of the graph: frozen nodes/edges are
    // lit (full color) and EVERYTHING ELSE IS PERSISTENTLY DIMMED — the user is hand-building a
    // reviewable sub-graph. The set grows only when the user hovers/taps an active (frozen)
    // node, lighting its links so they can freeze the next hop. Persists across navigation
    // (sessionStorage) so resuming the Atlas restores the sub-graph exactly as left.
    //
    // Class cascade (later wins in cy.style order): .restdim (persistent fade of non-frozen)
    // < .frznode/.frz (lit frozen set) < .hi (live hover, brightest, layered on top).
    function applyFrozen() {
      cy.batch(function () {
        cy.elements().removeClass("frz frznode restdim");
        if (!frozen.size) return;
        // 1) fade everything by default (the persistent resting dim)
        cy.elements().addClass("restdim");
        // 2) lift the frozen set back to lit
        frozen.forEach(function (id) {
          var n = cy.getElementById(id);
          if (!n || n.empty()) return;
          n.addClass("frznode").removeClass("restdim");
          n.connectedEdges().forEach(function (ed) {
            var s = ed.data("source"), t = ed.data("target");
            if (frozen.has(s) && frozen.has(t)) ed.addClass("frz").removeClass("restdim");
          });
        });
      });
      updateFreezeUI();
    }
    function freezeNeighborhood(node) {
      // 1 hop: the node, its direct neighbors, and the connecting edges.
      frozen.add(node.id());
      node.neighborhood("node").forEach(function (n) { frozen.add(n.id()); });
      persistState();
      applyFrozen();
    }

    // Touch devices have no hover, and a navigating tap is the wrong default (it yanks the
    // user off the page mid-exploration). So the input model differs by device:
    //   desktop: hover = highlight, left-click = navigate, right-click = freeze
    //   touch:   tap = highlight + pinned callout (with a "Go →" link), long-press = freeze,
    //            navigation only via the callout link (a deliberate second action)
    var TOUCH = false;
    try { TOUCH = window.matchMedia("(hover: none), (pointer: coarse)").matches; } catch (e) {}

    function navigate(d) {
      var u = d.url || d.id; if (!u) return;
      // Remember this node as the "you were here" anchor before leaving, so re-opening the Atlas
      // restores its selected-border + link highlight as a visual breadcrumb.
      setAnchor(d.id);
      if (/^https?:\/\//.test(u)) window.open(u, "_blank", "noopener,noreferrer");
      else window.location.href = u;
    }
    function highlight(node) {
      // transient live highlight: dim everything, then light this node's neighborhood on top.
      // Layers over the frozen resting state (.restdim/.frz stay underneath; .hi is brightest).
      cy.elements().addClass("dim");
      node.removeClass("dim"); node.neighborhood().removeClass("dim");
      node.connectedEdges().addClass("hi").removeClass("dim");
    }
    function clearHighlight() {
      cy.elements().removeClass("dim hi");
      // If a sub-graph is frozen, the resting state is NOT all-bright — restore the frozen
      // baseline (frozen lit, the rest persistently dimmed) instead of clearing to full.
      if (frozen.size) applyFrozen();
    }
    function tipFor(d) {
      return '<div class="tt">' + (d.title || d.id) + '</div><div class="tu">' + (d.url || d.id) + '</div>'
        + '<div class="tm">' + d.layer + " · " + (indeg[d.id] || 0) + " inbound" + (d.iso ? " · isolated" : "")
        + (frozen.has(d.id) ? " · frozen" : "") + "</div>";
    }

    if (!TOUCH) {
      // ── Desktop ──
      cy.on("mouseover", "node", function (e) {
        var d = e.target.data(), p = e.renderedPosition;
        tip(tipFor(d), p.x, p.y);
        highlight(e.target);
      });
      cy.on("mouseout", "node", function () { tip(null); clearHighlight(); });
      cy.on("tap", "node", function (e) { navigate(e.target.data()); });
      cy.on("cxttap", "node", function (e) { freezeNeighborhood(e.target); });
    } else {
      // ── Touch: tap highlights + pins a callout; navigation is via the callout's link ──
      cy.on("tap", "node", function (e) {
        var d = e.target.data(), p = e.renderedPosition;
        clearHighlight(); highlight(e.target);
        var t = document.getElementById("clef-map-tip");
        var go = '<a class="clef-map-go" href="' + (d.url || d.id) + '"'
          + (/^https?:\/\//.test(d.url || d.id) ? ' target="_blank" rel="noopener noreferrer"' : '')
          + '>Go →</a>';
        tip(tipFor(d) + '<div class="clef-map-tipactions">' + go
          + '<button type="button" class="clef-map-freezebtn">Freeze</button></div>', p.x, p.y);
        // Wire the in-callout actions. stopPropagation on the buttons so the tap does NOT bubble to
        // the empty-space dismiss handler (which would close the callout before the action fires).
        var fb = t && t.querySelector(".clef-map-freezebtn");
        if (fb) fb.addEventListener("click", function (ev) {
          ev.stopPropagation(); ev.preventDefault(); freezeNeighborhood(e.target);
        });
        // Set the "you were here" anchor before the link navigates, mirroring desktop's
        // navigate(). The link's own href does the navigation; we only record the breadcrumb
        // so re-opening the Atlas restores this node's selected-border + link highlight.
        var gl = t && t.querySelector(".clef-map-go");
        if (gl) gl.addEventListener("click", function (ev) { ev.stopPropagation(); setAnchor(d.id); });
      });
      // long-press = freeze (the right-click equivalent)
      cy.on("taphold", "node", function (e) { freezeNeighborhood(e.target); });
      // tapping empty space dismisses the callout + transient highlight
      cy.on("tap", function (e) { if (e.target === cy) { tip(null); clearHighlight(); } });
    }
    var cyContainer = document.getElementById("clef-map-cy");
    if (cyContainer) cyContainer.addEventListener("contextmenu", function (ev) { ev.preventDefault(); });

    // layout + edge toggles
    document.querySelectorAll("#clef-map-modal [data-map-layout]").forEach(function (b) {
      b.onclick = function () {
        document.querySelectorAll("#clef-map-modal [data-map-layout]").forEach(function (x) { x.classList.remove("active"); });
        b.classList.add("active");
        cy.layout(buildLayout(b.getAttribute("data-map-layout"), indeg, true)).run();
        // re-apply frozen styling after the layout settles (layout clears classes)
        applyFrozen();
      };
    });
    // per-type edge toggles: each button independently shows/hides its own edge type
    document.querySelectorAll("#clef-map-modal [data-map-edge]").forEach(function (b) {
      b.onclick = function () {
        var type = b.getAttribute("data-map-edge");
        var on = b.classList.toggle("active");
        cy.edges('[type="' + type + '"]').style("display", on ? "element" : "none");
      };
    });
    // Clear button: reset to the full graph. Wipes the frozen sub-graph AND the current
    // selection/anchor + any transient highlight, so it does something useful even when no
    // sub-graph is frozen (drop the "you were here" breadcrumb and show everything lit).
    document.querySelectorAll("#clef-map-modal [data-map-clear]").forEach(function (b) {
      b.onclick = function () {
        if (cueTimer) { clearTimeout(cueTimer); cueTimer = null; }
        b.classList.remove("clef-map-btn-cue");
        frozen.clear(); persistState();
        setAnchor(null);
        cy.batch(function () {
          cy.elements().removeClass("dim hi restdim frz frznode");
          cy.elements().unselect();
        });
        updateFreezeUI();
        tip(null);
      };
    });

    // ── Help popover (the ? button) ──────────────────────────────────────────────────────
    // One source of content, wording adapts to the input model (touch vs mouse). Auto-shows
    // once on touch first-run (no way to discover long-press otherwise), then remembers it.
    var help = document.getElementById("clef-map-help");
    var helpList = document.getElementById("clef-map-help-list");
    if (helpList) {
      var rows = TOUCH ? [
        ["Tap a node", "highlight it and its links; opens a card with “Go →”"],
        ["Long-press a node", "freeze its sub-graph (or use Freeze on the card)"],
        ["Tap “Go →”", "open that spec / doc / blog entry"],
        ["Clear", "reset the frozen sub-graph"]
      ] : [
        ["Hover over a node", "highlight it and its links"],
        ["Click a node", "open that spec / doc / blog entry"],
        ["Right-click a node", "freeze its sub-graph (build a custom graph hop by hop)"],
        ["Clear", "reset the frozen sub-graph"]
      ];
      helpList.innerHTML = rows.map(function (r) {
        return '<li><b>' + r[0] + '</b><span>' + r[1] + '</span></li>';
      }).join("");
    }
    function showHelp() { if (help) help.hidden = false; }
    function hideHelp() { if (help) help.hidden = true; }
    function toggleHelp() { if (help) (help.hidden ? showHelp() : hideHelp()); }
    document.querySelectorAll("#clef-map-modal [data-map-help]").forEach(function (b) { b.onclick = toggleHelp; });
    document.querySelectorAll("#clef-map-modal [data-map-help-close]").forEach(function (b) { b.onclick = hideHelp; });
    // dismiss on tap/click outside the popover (but not when the ? button itself is hit)
    if (help) help.addEventListener("click", function (e) { if (e.target === help) hideHelp(); });
    // first-run auto-show on touch only
    if (TOUCH) {
      var seen = false;
      try { seen = localStorage.getItem("clefAtlasHelpSeen") === "1"; } catch (e) {}
      if (!seen) {
        showHelp();
        try { localStorage.setItem("clefAtlasHelpSeen", "1"); } catch (e) {}
      }
    }

    // Restore any frozen sub-graph the user left behind, then paint it.
    applyFrozen();

    // Restore the "you were here" anchor: the last node the user clicked through to gets its
    // selected-border (white) back, and its links re-highlight — a visual breadcrumb layered on
    // top of the frozen resting state. If the anchor sits at the boundary of a frozen sub-graph,
    // the user sees both their custom graph AND exactly where they stepped out of it.
    if (anchorId) {
      var an = cy.getElementById(anchorId);
      if (an && !an.empty()) {
        // select() gives the white anchor border; the neighborhood highlight layers on top of
        // whatever resting state applyFrozen() already painted (transient .dim/.hi over .restdim).
        an.select();
        cy.elements().addClass("dim");
        an.removeClass("dim"); an.neighborhood().removeClass("dim");
        an.connectedEdges().addClass("hi").removeClass("dim");
        // re-lift the frozen set so it stays visibly lit under the anchor's transient dim
        if (frozen.size) frozen.forEach(function (id) {
          var fn = cy.getElementById(id);
          if (fn && !fn.empty()) fn.removeClass("dim");
        });
        // Cue the Clear button so the returning user sees it can reset this restored selection.
        // Only when nothing is frozen — a frozen set already shows its own persistent count.
        cueClear();
      } else {
        setAnchor(null);  // anchored node no longer in the graph — drop the stale breadcrumb
      }
    }

    var s = g.stats;
    if (s) {
      var ppc = g.nodes.filter(function (n) { return n.data.layer === "preprint" && !n.data.iso; }).length;
      var st = document.getElementById("clef-map-statline");
      if (st) st.textContent = s.nodes + " nodes · " + s.edges + " edges · pre-print kernel " + ppc + "/" + ((s.categories && s.categories.preprint) || 6);
    }
  }

  // Always fetch the graph live on open (the user wants current state every time they pull
  // it up). Cytoscape itself loads once; the data and the rendered instance are rebuilt each
  // open, so a re-indexed graph shows immediately without a page reload.
  function spinner(on) {
    var sp = document.getElementById("clef-map-spinner");
    if (sp) sp.hidden = !on;
  }

  function loadGraphLive() {
    var c = document.getElementById("clef-map-cy");
    // Cancel any pending Clear-button cue from a prior open so a stale timer can't revert the
    // button text against a freshly rendered graph.
    if (cueTimer) { clearTimeout(cueTimer); cueTimer = null; }
    // Tear down any prior graph up front so the query always runs over a clean canvas with the
    // spinner on top — never over a stale mid-session graph (which reads as a jarring flash when
    // it's swapped) and never a blank canvas with no spinner. The spinner IS the interstitial.
    if (cy) { try { cy.destroy(); } catch (e) {} cy = null; }
    if (c) c.innerHTML = "";
    spinner(true);
    return loadCytoscape()
      .then(function () {
        // cache-bust so a CDN/browser cache never serves a stale graph
        var url = GRAPH_URL + (GRAPH_URL.indexOf("?") === -1 ? "?" : "&") + "t=" + Date.now();
        return fetch(url, { cache: "no-store" }).then(function (r) { return r.json(); });
      })
      .then(function (g) {
        if (c) c.innerHTML = "";
        spinner(false);
        render(g);
      })
      .catch(function (err) {
        spinner(false);
        if (c) c.innerHTML = '<div style="padding:2rem;color:#9ca3af">Map unavailable: ' + err + "</div>";
      });
  }

  function open() {
    var m = modal(); if (!m) return;
    m.style.display = "";
    document.body.style.overflow = "hidden";
    // The graph always reveals itself when it hydrates, so a re-hydrate never snaps in. The
    // session flag only decides HOW MUCH: the first open of the session gets the full unveiling
    // (a longer fade WITH an upward slide), every later open gets a quick fade only (no slide,
    // so identical node positions never appear to move — nothing to re-locate). Arm the class
    // BEFORE the fetch so no fully-drawn graph flashes before the reveal begins.
    var c = document.getElementById("clef-map-cy");
    var full = c && !hasBeenSeen();
    if (c) { c.classList.add("clef-map-hydrate"); if (full) c.classList.add("clef-map-hydrate-full"); }
    loadGraphLive().then(function () {
      if (cy) cy.resize().fit(undefined, 30);
      // Graph is positioned and fit. Drop the arming class on the next frame so the CSS
      // transition runs. Two rAFs so the hidden (opacity:0) state paints before the transition.
      if (c) {
        requestAnimationFrame(function () {
          requestAnimationFrame(function () { c.classList.remove("clef-map-hydrate", "clef-map-hydrate-full"); });
        });
        markSeen();
      }
    });
  }
  function close() {
    var m = modal(); if (!m) return;
    m.style.display = "none";
    document.body.style.overflow = "";
  }

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && isOpen()) { e.preventDefault(); close(); }
  });

  window.clefMap = { open: open, close: close, isOpen: isOpen };
})();
