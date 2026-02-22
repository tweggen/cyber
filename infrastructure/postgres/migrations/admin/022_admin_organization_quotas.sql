-- Create OrganizationQuotas table for admin panel
-- Organization-level default resource quotas for notebook platform

CREATE TABLE admin."OrganizationQuotas" (
    "OrganizationId" uuid NOT NULL,
    "MaxNotebooks" integer NOT NULL,
    "MaxEntriesPerNotebook" integer NOT NULL,
    "MaxEntrySizeBytes" bigint NOT NULL,
    "MaxTotalStorageBytes" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrganizationQuotas" PRIMARY KEY ("OrganizationId")
);
