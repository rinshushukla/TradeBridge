-- Empty-database schema matching the API's original shipment model.
BEGIN;

CREATE TABLE IF NOT EXISTS "Organizations" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "Iec" varchar(10) NULL,
    "Gstin" varchar(15) NULL,
    "ApedaRegistration" text NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Organizations_Iec" ON "Organizations"("Iec") WHERE "Iec" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Organizations_Gstin" ON "Organizations"("Gstin");

CREATE TABLE IF NOT EXISTS "Shipments" (
    "Id" uuid PRIMARY KEY,
    "Reference" varchar(40) NOT NULL,
    "BuyerId" uuid NOT NULL REFERENCES "Organizations"("Id") ON DELETE RESTRICT,
    "Commodity" varchar(240) NOT NULL,
    "Origin" varchar(120) NOT NULL,
    "Destination" varchar(120) NOT NULL,
    "Status" varchar(40) NOT NULL,
    "StatusSource" varchar(40) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Shipments_Reference" ON "Shipments"("Reference");
CREATE INDEX IF NOT EXISTS "IX_Shipments_BuyerId_UpdatedAtUtc" ON "Shipments"("BuyerId", "UpdatedAtUtc");

CREATE TABLE IF NOT EXISTS "ShipmentEvents" (
    "Id" uuid PRIMARY KEY,
    "ShipmentId" uuid NOT NULL REFERENCES "Shipments"("Id") ON DELETE CASCADE,
    "EventType" varchar(60) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "OccurredAtUtc" timestamptz NOT NULL,
    "Source" varchar(40) NOT NULL,
    "ExternalReference" text NULL,
    "RecordedByUserId" uuid NULL
);
CREATE INDEX IF NOT EXISTS "IX_ShipmentEvents_ShipmentId_OccurredAtUtc" ON "ShipmentEvents"("ShipmentId", "OccurredAtUtc");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShipmentEvents_Source_ExternalReference" ON "ShipmentEvents"("Source", "ExternalReference") WHERE "ExternalReference" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "TradeDocuments" (
    "Id" uuid PRIMARY KEY,
    "ShipmentId" uuid NOT NULL,
    "DocumentType" varchar(60) NOT NULL,
    "ObjectKey" varchar(500) NOT NULL,
    "Status" varchar(40) NOT NULL,
    "UploadedByUserId" uuid NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_TradeDocuments_ShipmentId_DocumentType_CreatedAtUtc" ON "TradeDocuments"("ShipmentId", "DocumentType", "CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "PaymentMilestones" (
    "Id" uuid PRIMARY KEY,
    "ShipmentId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" varchar(3) NOT NULL,
    "Status" varchar(40) NOT NULL,
    "ReleaseCondition" text NOT NULL,
    "ReleasedAtUtc" timestamptz NULL
);
CREATE INDEX IF NOT EXISTS "IX_PaymentMilestones_ShipmentId" ON "PaymentMilestones"("ShipmentId");

COMMIT;
