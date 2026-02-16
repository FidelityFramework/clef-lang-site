-- Query log table for Smart Search analytics
CREATE TABLE IF NOT EXISTS query_log (
    id TEXT PRIMARY KEY,
    query_text TEXT NOT NULL,
    timestamp TEXT NOT NULL,
    response_latency_ms INTEGER NOT NULL DEFAULT 0,
    source_urls TEXT,
    source_count INTEGER NOT NULL DEFAULT 0
);

-- Index for analytics queries
CREATE INDEX IF NOT EXISTS idx_query_log_timestamp ON query_log(timestamp);
