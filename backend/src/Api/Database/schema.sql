CREATE TABLE IF NOT EXISTS short_links (
    code text PRIMARY KEY,
    long_url text NOT NULL,
    created_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NULL,
    is_custom_alias boolean NOT NULL DEFAULT false,
    click_count bigint NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_short_links_created_at_utc
    ON short_links (created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_short_links_expires_at_utc
    ON short_links (expires_at_utc)
    WHERE expires_at_utc IS NOT NULL;
