SELECT 'CREATE DATABASE notebook_admin'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notebook_admin')\gexec
