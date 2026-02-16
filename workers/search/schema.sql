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
