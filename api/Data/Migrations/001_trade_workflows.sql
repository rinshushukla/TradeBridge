-- Apply once to an existing TradeBridge PostgreSQL database before starting this API version.
BEGIN;

CREATE TABLE IF NOT EXISTS "TradeUsers" (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL REFERENCES "Organizations"("Id"),
    "Email" varchar(320) NULL,
    "PhoneNumber" varchar(16) NULL,
    "FullName" varchar(200) NOT NULL,
    "PasswordHash" varchar(500) NOT NULL,
    "Role" varchar(40) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "DisabledAtUtc" timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TradeUsers_Email" ON "TradeUsers"("Email");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TradeUsers_PhoneNumber" ON "TradeUsers"("PhoneNumber");

CREATE TABLE IF NOT EXISTS "AccessSessions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES "TradeUsers"("Id"),
    "TokenHash" varchar(64) NOT NULL,
    "ExpiresAtUtc" timestamptz NOT NULL,
    "RevokedAtUtc" timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccessSessions_TokenHash" ON "AccessSessions"("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_AccessSessions_UserId_ExpiresAtUtc" ON "AccessSessions"("UserId", "ExpiresAtUtc");

CREATE TABLE IF NOT EXISTS "TradeRequests" (
    "Id" uuid PRIMARY KEY,
    "Reference" varchar(40) NOT NULL,
    "ImporterOrganizationId" uuid NOT NULL REFERENCES "Organizations"("Id"),
    "Product" varchar(240) NOT NULL,
    "Quantity" varchar(120) NOT NULL,
    "Incoterms" varchar(20) NOT NULL,
    "TargetShipmentDate" date NOT NULL,
    "OriginPort" varchar(120) NOT NULL,
    "DestinationPort" varchar(120) NOT NULL,
    "RequiredCertifications" varchar(1000) NOT NULL DEFAULT '',
    "Status" varchar(40) NOT NULL,
    "EncryptedPrivateDetails" bytea NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TradeRequests_Reference" ON "TradeRequests"("Reference");
CREATE INDEX IF NOT EXISTS "IX_TradeRequests_Status_CreatedAtUtc" ON "TradeRequests"("Status", "CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "TradeBids" (
    "Id" uuid PRIMARY KEY,
    "TradeRequestId" uuid NOT NULL REFERENCES "TradeRequests"("Id") ON DELETE CASCADE,
    "ExporterOrganizationId" uuid NOT NULL REFERENCES "Organizations"("Id"),
    "EncryptedPayload" bytea NOT NULL,
    "Status" varchar(40) NOT NULL,
    "SubmittedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TradeBids_TradeRequestId_ExporterOrganizationId" ON "TradeBids"("TradeRequestId", "ExporterOrganizationId");

CREATE TABLE IF NOT EXISTS "KycRecords" (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL REFERENCES "Organizations"("Id"),
    "DocumentType" varchar(80) NOT NULL,
    "EncryptedContent" text NOT NULL,
    "Status" varchar(40) NOT NULL,
    "UploadedAtUtc" timestamptz NOT NULL,
    "ReviewedByUserId" uuid NULL REFERENCES "TradeUsers"("Id")
);
CREATE INDEX IF NOT EXISTS "IX_KycRecords_OrganizationId_DocumentType" ON "KycRecords"("OrganizationId", "DocumentType");

CREATE TABLE IF NOT EXISTS "TradeMessages" (
    "Id" uuid PRIMARY KEY,
    "TradeRequestId" uuid NOT NULL REFERENCES "TradeRequests"("Id") ON DELETE CASCADE,
    "SenderUserId" uuid NOT NULL REFERENCES "TradeUsers"("Id"),
    "RecipientUserId" uuid NULL REFERENCES "TradeUsers"("Id"),
    "EncryptedBody" bytea NOT NULL,
    "Status" varchar(40) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_TradeMessages_TradeRequestId_CreatedAtUtc" ON "TradeMessages"("TradeRequestId", "CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "TradeContracts" (
    "Id" uuid PRIMARY KEY,
    "TradeRequestId" uuid NOT NULL REFERENCES "TradeRequests"("Id") ON DELETE CASCADE,
    "SelectedBidId" uuid NOT NULL REFERENCES "TradeBids"("Id"),
    "EncryptedTerms" bytea NOT NULL,
    "Status" varchar(40) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TradeContracts_TradeRequestId" ON "TradeContracts"("TradeRequestId");

CREATE TABLE IF NOT EXISTS "DocumentAccessLogs" (
    "Id" uuid PRIMARY KEY,
    "TradeDocumentId" uuid NULL REFERENCES "TradeDocuments"("Id") ON DELETE CASCADE,
    "ResourceType" varchar(40) NOT NULL,
    "ResourceId" uuid NOT NULL,
    "ActorUserId" uuid NOT NULL REFERENCES "TradeUsers"("Id"),
    "Action" varchar(40) NOT NULL,
    "OccurredAtUtc" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_DocumentAccessLogs_ResourceType_ResourceId_OccurredAtUtc" ON "DocumentAccessLogs"("ResourceType", "ResourceId", "OccurredAtUtc");

CREATE TABLE IF NOT EXISTS "ShippingDocuments" (
    "Id" uuid PRIMARY KEY,
    "TradeRequestId" uuid NOT NULL REFERENCES "TradeRequests"("Id") ON DELETE CASCADE,
    "UploadedByUserId" uuid NOT NULL REFERENCES "TradeUsers"("Id"),
    "DocumentType" varchar(60) NOT NULL,
    "ContentType" varchar(100) NOT NULL,
    "EncryptedContent" text NOT NULL,
    "Status" varchar(40) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_ShippingDocuments_TradeRequestId_CreatedAtUtc" ON "ShippingDocuments"("TradeRequestId", "CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "Settlements" (
    "Id" uuid PRIMARY KEY,
    "TradeRequestId" uuid NOT NULL REFERENCES "TradeRequests"("Id") ON DELETE CASCADE,
    "GrossAmount" numeric(18,2) NOT NULL,
    "ChaCommission" numeric(18,2) NOT NULL,
    "PlatformFee" numeric(18,2) NOT NULL,
    "ExporterAmount" numeric(18,2) NOT NULL,
    "Currency" varchar(3) NOT NULL,
    "Status" varchar(40) NOT NULL,
    "GatewayReference" varchar(200) NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Settlements_TradeRequestId" ON "Settlements"("TradeRequestId");

COMMIT;
