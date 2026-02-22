SELECT 'CREATE DATABASE thinktank'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'thinktank')\gexec
