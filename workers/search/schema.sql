-- Clef Search D1 Schema
-- Content sections table (source of truth for metadata)
-- FTS5 virtual table for BM25 full-text search

CREATE TABLE IF NOT EXISTS content_sections (
    id TEXT PRIMARY KEY,                        -- e.g. "blog/rust-revisited#3"
    content_type TEXT NOT NULL,                 -- blog, design, internals, reference, guides, spec
    page_slug TEXT NOT NULL,                    -- e.g. "rust-revisited"
    page_title TEXT NOT NULL,
    page_url TEXT NOT NULL,                     -- e.g. "/blog/rust-revisited/"
    section_index INTEGER NOT NULL,             -- 0-based section within page
    section_title TEXT NOT NULL DEFAULT '',      -- H2/H3 heading text
    content TEXT NOT NULL,                      -- section body text (markdown stripped)
    tags TEXT DEFAULT '',                       -- comma-separated
    summary TEXT DEFAULT '',
    published_at TEXT DEFAULT '',               -- YYYY-MM-DD authorship date, for recency ranking
    content_hash TEXT NOT NULL,                 -- for change detection
    indexed_at TEXT NOT NULL,                   -- ISO8601 timestamp
    vector_indexed INTEGER NOT NULL DEFAULT 0   -- 1 if embedding exists in Vectorize
);

CREATE INDEX IF NOT EXISTS idx_content_sections_page ON content_sections(page_slug);
CREATE INDEX IF NOT EXISTS idx_content_sections_type ON content_sections(content_type);

-- FTS5 virtual table with weighted columns
-- Weights applied at query time via bm25(): page_title=10, section_title=5, content=1
-- tokenchars includes .-_#+ so technical terms like "F#", "C++", ".NET" stay intact
CREATE VIRTUAL TABLE IF NOT EXISTS content_fts USING fts5(
    page_title,
    section_title,
    content,
    content=content_sections,
    content_rowid=rowid,
    tokenize='unicode61 tokenchars .-_#+'
);

-- Triggers to keep FTS5 in sync with content_sections
CREATE TRIGGER IF NOT EXISTS content_fts_insert AFTER INSERT ON content_sections BEGIN
    INSERT INTO content_fts(rowid, page_title, section_title, content)
    VALUES (new.rowid, new.page_title, new.section_title, new.content);
END;

CREATE TRIGGER IF NOT EXISTS content_fts_delete AFTER DELETE ON content_sections BEGIN
    INSERT INTO content_fts(content_fts, rowid, page_title, section_title, content)
    VALUES('delete', old.rowid, old.page_title, old.section_title, old.content);
END;

CREATE TRIGGER IF NOT EXISTS content_fts_update AFTER UPDATE ON content_sections BEGIN
    INSERT INTO content_fts(content_fts, rowid, page_title, section_title, content)
    VALUES('delete', old.rowid, old.page_title, old.section_title, old.content);
    INSERT INTO content_fts(rowid, page_title, section_title, content)
    VALUES (new.rowid, new.page_title, new.section_title, new.content);
END;

-- ── Corpus graph (the "Map" modal — another index sharing this DB) ──────────
-- Page-grained: node id = canonical page_url (trailing-slash). Distinct from the
-- section-grained content_sections.id. Populated by the CLI `graph` command via
-- POST /graph/rebuild (idempotent full rebuild). No FK/CASCADE (D1 doesn't enforce
-- reliably); no degree triggers (they break under upsert) — degree is read at query time.

CREATE TABLE IF NOT EXISTS graph_nodes (
    page_url     TEXT PRIMARY KEY,          -- canonical trailing-slash URL = node id
    content_type TEXT NOT NULL,             -- blog|spec|design|internals|reference|guides|preprint|external
    layer        TEXT NOT NULL,             -- derived ring: preprint|external|spec|docs|blog
    title        TEXT NOT NULL DEFAULT '',
    summary      TEXT DEFAULT '',
    tags         TEXT DEFAULT '',
    published_at TEXT DEFAULT '',
    ext_url      TEXT DEFAULT '',           -- for preprint/external nodes: the arxiv URL to open
    updated_at   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_graph_nodes_layer ON graph_nodes(layer);

CREATE TABLE IF NOT EXISTS graph_edges (
    source_url TEXT NOT NULL,               -- a graph_nodes.page_url
    target_url TEXT NOT NULL,               -- a graph_nodes.page_url (or arxiv: id)
    edge_type  TEXT NOT NULL,               -- 'href' | 'cites' | 'tag'  (plain string)
    weight     REAL NOT NULL DEFAULT 1.0,
    label      TEXT DEFAULT '',
    updated_at TEXT NOT NULL,
    PRIMARY KEY (source_url, target_url, edge_type)
);
CREATE INDEX IF NOT EXISTS idx_graph_edges_source ON graph_edges(source_url);
CREATE INDEX IF NOT EXISTS idx_graph_edges_target ON graph_edges(target_url);
CREATE INDEX IF NOT EXISTS idx_graph_edges_type   ON graph_edges(edge_type);
