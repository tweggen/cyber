-- Migration 003: Graph schema for entry relationships
-- Depends on: init.sql (graph 'notebook_graph' created), 002_schema.sql

-- Set search path for AGE operations
LOAD 'age';
SET search_path = ag_catalog, "$user", public;

-- Create vertex labels for entries
-- Each entry becomes a vertex in the graph
SELECT create_vlabel('notebook_graph', 'entry');

-- Create edge labels for relationships between entries
-- 'references' - explicit reference from one entry to another
SELECT create_elabel('notebook_graph', 'references');

-- 'revision_of' - revision chain linking new version to previous
SELECT create_elabel('notebook_graph', 'revision_of');

-- 'coherence' - semantic similarity links (computed by entropy service)
SELECT create_elabel('notebook_graph', 'coherence');

-- Add comments explaining the graph structure
COMMENT ON TABLE ag_catalog.notebook_graph IS 'Apache AGE graph for entry relationships';

-- Create helper function to add an entry vertex
-- Called when a new entry is inserted
-- Note: p_author_id is passed as hex string (64 chars) since AuthorId is 32 bytes
CREATE OR REPLACE FUNCTION add_entry_vertex(
    p_entry_id UUID,
    p_notebook_id UUID,
    p_topic TEXT,
    p_author_id TEXT,
    p_sequence BIGINT
) RETURNS void AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            CREATE (:entry {
                id: %L,
                notebook_id: %L,
                topic: %L,
                author_id: %L,
                sequence: %s
            })
        $$) AS (v agtype)',
        p_entry_id::text,
        p_notebook_id::text,
        COALESCE(p_topic, ''),
        COALESCE(p_author_id, ''),
        p_sequence
    );

    EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Create helper function to add reference edges
-- Called for each reference when an entry is inserted
CREATE OR REPLACE FUNCTION add_reference_edge(
    p_from_entry_id UUID,
    p_to_entry_id UUID
) RETURNS void AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH (a:entry {id: %L}), (b:entry {id: %L})
            CREATE (a)-[:references]->(b)
        $$) AS (e agtype)',
        p_from_entry_id::text,
        p_to_entry_id::text
    );

    EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Create helper function to add revision_of edge
CREATE OR REPLACE FUNCTION add_revision_edge(
    p_new_entry_id UUID,
    p_old_entry_id UUID
) RETURNS void AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH (new:entry {id: %L}), (old:entry {id: %L})
            CREATE (new)-[:revision_of]->(old)
        $$) AS (e agtype)',
        p_new_entry_id::text,
        p_old_entry_id::text
    );

    EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Create helper function to add coherence edge (bidirectional semantic link)
CREATE OR REPLACE FUNCTION add_coherence_edge(
    p_entry_id_1 UUID,
    p_entry_id_2 UUID,
    p_similarity FLOAT
) RETURNS void AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH (a:entry {id: %L}), (b:entry {id: %L})
            CREATE (a)-[:coherence {similarity: %s}]->(b)
        $$) AS (e agtype)',
        p_entry_id_1::text,
        p_entry_id_2::text,
        p_similarity
    );

    EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Function to find all entries reachable from a given entry via references
CREATE OR REPLACE FUNCTION find_reference_closure(
    p_entry_id UUID,
    p_max_depth INT DEFAULT 10
) RETURNS TABLE(entry_id TEXT, depth INT) AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH path = (start:entry {id: %L})-[:references*1..%s]->(reachable:entry)
            RETURN reachable.id, length(path)
        $$) AS (entry_id agtype, depth agtype)',
        p_entry_id::text,
        p_max_depth
    );

    RETURN QUERY EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Function to find revision chain for an entry
CREATE OR REPLACE FUNCTION find_revision_chain(
    p_entry_id UUID
) RETURNS TABLE(entry_id TEXT, depth INT) AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH path = (start:entry {id: %L})-[:revision_of*]->(ancestor:entry)
            RETURN ancestor.id, length(path)
            ORDER BY length(path)
        $$) AS (entry_id agtype, depth agtype)',
        p_entry_id::text
    );

    RETURN QUERY EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Function to find entries citing a given entry
CREATE OR REPLACE FUNCTION find_citations(
    p_entry_id UUID
) RETURNS TABLE(citing_entry_id TEXT) AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH (citing:entry)-[:references]->(cited:entry {id: %L})
            RETURN citing.id
        $$) AS (citing_entry_id agtype)',
        p_entry_id::text
    );

    RETURN QUERY EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

-- Function to find coherent entries (semantically related)
CREATE OR REPLACE FUNCTION find_coherent_entries(
    p_entry_id UUID,
    p_min_similarity FLOAT DEFAULT 0.5
) RETURNS TABLE(related_entry_id TEXT, similarity FLOAT) AS $$
DECLARE
    cypher_query TEXT;
BEGIN
    LOAD 'age';
    SET search_path = ag_catalog, "$user", public;

    cypher_query := format(
        'SELECT * FROM cypher(''notebook_graph'', $$
            MATCH (a:entry {id: %L})-[c:coherence]-(b:entry)
            WHERE c.similarity >= %s
            RETURN b.id, c.similarity
            ORDER BY c.similarity DESC
        $$) AS (related_entry_id agtype, similarity agtype)',
        p_entry_id::text,
        p_min_similarity
    );

    RETURN QUERY EXECUTE cypher_query;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION add_entry_vertex IS 'Create graph vertex for a new entry';
COMMENT ON FUNCTION add_reference_edge IS 'Create reference edge between entries';
COMMENT ON FUNCTION add_revision_edge IS 'Create revision chain edge';
COMMENT ON FUNCTION add_coherence_edge IS 'Create semantic similarity edge';
COMMENT ON FUNCTION find_reference_closure IS 'Find all entries reachable via references';
COMMENT ON FUNCTION find_revision_chain IS 'Find all ancestors in revision chain';
COMMENT ON FUNCTION find_citations IS 'Find entries that cite a given entry';
COMMENT ON FUNCTION find_coherent_entries IS 'Find semantically related entries';
