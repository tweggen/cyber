#!/bin/bash
set -e

# Apply schema migrations to the thinktank database.
# initdb.d scripts run against the default DB (notebook), so we
# explicitly target the thinktank DB with psql -d.
#
# 02-schema.sql is already mounted in initdb.d. The remaining
# migration files are mounted at /thinktank-migrations/.
for f in \
  /docker-entrypoint-initdb.d/02-schema.sql \
  /thinktank-migrations/004_coherence_links.sql \
  /thinktank-migrations/006_notebook_sequence.sql \
  /thinktank-migrations/007_claims_and_jobs.sql \
  /thinktank-migrations/008_original_content_type.sql \
  /thinktank-migrations/009_embeddings.sql \
  /thinktank-migrations/010_job_priority.sql \
  /thinktank-migrations/011_add_source_column.sql \
  /thinktank-migrations/012_integration_status.sql
do
  echo "thinktank: applying $f"
  psql -U "$POSTGRES_USER" -d thinktank -f "$f"
done
