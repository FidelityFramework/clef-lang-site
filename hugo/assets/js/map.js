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
  // radial rings, center → out
  var LEVEL = {
    preprint: 1, external: 2,
    "spec-foundations": 3, "spec-meaning": 4, "spec-machinery": 5,
    "docs-design": 6, "docs-internals": 7, "docs-tooling": 8,
    blog: 9,
  };

  var cy = null, loadingCy = null;

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

  function buildLayout(name, indeg) {
    if (name === "grouped") {
      var ord = {
        preprint: 900, external: 800,
        "spec-foundations": 700, "spec-meaning": 600, "spec-machinery": 500,
        "docs-design": 400, "docs-internals": 300, "docs-tooling": 200, blog: 100,
      };
      return { name: "concentric", concentric: function (n) { return ord[bandOf(n.data())] + (10 - Math.min(9, indeg[n.data("id")] || 0)); },
               levelWidth: function () { return 3; }, minNodeSpacing: 9, spacingFactor: 1.0, animate: true, animationDuration: 500 };
    }
    // 9 bands now: preprint(1)…blog(9). Higher concentric value = innermost, so invert.
    return { name: "concentric", concentric: function (n) { return 10 - LEVEL[bandOf(n.data())]; },
             levelWidth: function () { return 1; }, minNodeSpacing: 12, spacingFactor: 0.9, animate: true, animationDuration: 500 };
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
          "border-width": function (e) { return e.data("layer") === "preprint" ? 1.5 : 0.5; },
          "border-color": function (e) { return e.data("layer") === "preprint" ? "#fff" : (dark ? "#0d1117" : "#fff"); } } },
        { selector: "node[?iso]", style: { "opacity": 0.25 } },
        { selector: 'edge[type="href"]', style: { "width": 0.4, "line-color": dark ? "#484f58" : "#d0d7de", "curve-style": "haystack", "opacity": 0.45 } },
        { selector: 'edge[type="cites"]', style: { "width": 0.8, "line-color": "#e3b341", "curve-style": "haystack", "opacity": 0.55 } },
        { selector: 'edge[type="tag"]', style: { "width": 0.3, "line-color": "#bc8cff", "line-style": "dashed", "curve-style": "haystack", "opacity": 0.2 } },
        { selector: "node:selected", style: { "border-width": 2.5, "border-color": dark ? "#e6edf3" : "#1f2328" } },
        { selector: ".dim", style: { "opacity": 0.05 } },
        { selector: ".hi", style: { "opacity": 0.95, "width": 1.4, "line-color": dark ? "#e6edf3" : "#1f2328" } }
      ],
      layout: buildLayout("concentric", indeg)
    });

    cy.on("mouseover", "node", function (e) {
      var d = e.target.data(), p = e.renderedPosition;
      tip('<div class="tt">' + (d.title || d.id) + '</div><div class="tu">' + (d.url || d.id) + '</div>'
        + '<div class="tm">' + d.layer + " · " + (indeg[d.id] || 0) + " inbound" + (d.iso ? " · isolated" : "") + "</div>", p.x, p.y);
      cy.elements().addClass("dim");
      e.target.removeClass("dim"); e.target.neighborhood().removeClass("dim");
      e.target.connectedEdges().addClass("hi").removeClass("dim");
    });
    cy.on("mouseout", "node", function () { tip(null); cy.elements().removeClass("dim hi"); });
    cy.on("tap", "node", function (e) {
      var d = e.target.data(), u = d.url || d.id;
      if (!u) return;
      // External destinations (pre-print / cited papers, http(s)) open in a new tab so the
      // graph and the reader's context are not lost. Internal pages navigate in place.
      if (/^https?:\/\//.test(u)) {
        window.open(u, "_blank", "noopener,noreferrer");
      } else {
        window.location.href = u;
      }
    });

    // layout + edge toggles
    document.querySelectorAll("#clef-map-modal [data-map-layout]").forEach(function (b) {
      b.onclick = function () {
        document.querySelectorAll("#clef-map-modal [data-map-layout]").forEach(function (x) { x.classList.remove("active"); });
        b.classList.add("active"); cy.layout(buildLayout(b.getAttribute("data-map-layout"), indeg)).run();
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
  function loadGraphLive() {
    var c = document.getElementById("clef-map-cy");
    if (c) c.innerHTML = '<div style="padding:2rem;color:#9ca3af">Loading map…</div>';
    return loadCytoscape()
      .then(function () {
        // cache-bust so a CDN/browser cache never serves a stale graph
        var url = GRAPH_URL + (GRAPH_URL.indexOf("?") === -1 ? "?" : "&") + "t=" + Date.now();
        return fetch(url, { cache: "no-store" }).then(function (r) { return r.json(); });
      })
      .then(function (g) {
        if (cy) { try { cy.destroy(); } catch (e) {} cy = null; }
        if (c) c.innerHTML = "";
        render(g);
      })
      .catch(function (err) {
        if (c) c.innerHTML = '<div style="padding:2rem;color:#9ca3af">Map unavailable: ' + err + "</div>";
      });
  }

  function open() {
    var m = modal(); if (!m) return;
    m.style.display = "";
    document.body.style.overflow = "hidden";
    loadGraphLive().then(function () { if (cy) cy.resize().fit(undefined, 30); });
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
