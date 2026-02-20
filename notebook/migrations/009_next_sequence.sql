-- Migration 009: Atomically increment notebook sequence via server-side function
-- Avoids EF Core SqlQuery composability issue with UPDATE ... RETURNING

CREATE OR REPLACE FUNCTION next_sequence(p_notebook_id UUID) RETURNS BIGINT AS $$
    UPDATE notebooks SET current_sequence = current_sequence + 1
    WHERE id = p_notebook_id
    RETURNING current_sequence;
$$ LANGUAGE SQL;
