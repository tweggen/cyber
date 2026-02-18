-- Initialize Apache AGE extension for graph queries
-- This runs on first database startup

-- Enable AGE extension
CREATE EXTENSION IF NOT EXISTS age;

-- Load AGE into search path
LOAD 'age';
SET search_path = ag_catalog, "$user", public;

-- Create the graph for notebook references
-- Agent-store will add vertices and edges as needed
SELECT create_graph('notebook_graph');

-- Add helpful comment
COMMENT ON EXTENSION age IS 'Apache AGE graph database extension for notebook reference traversal';
