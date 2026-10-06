# TradeBridge API setup

## Local setup

1. Configure `ConnectionStrings:TradeBridge` for PostgreSQL.
2. For a fresh database, apply `Data/Migrations/000_initial_schema.sql` followed by `Data/Migrations/001_trade_workflows.sql`. For an existing TradeBridge database, apply `001_trade_workflows.sql` only.
3. Run `dotnet restore` and `dotnet run` from this directory. The development profile listens on `http://localhost:5000`.
4. Start the Angular app from `web` with `npm start`.

Set the frontend API address in `web/public/config.json` (`apiBaseUrl`). Angular serves this file as `/config.json`; the frontend loads it before startup, and the authentication, RFQ, shipment, and KYC services all build their API URLs from that value. Change it to the deployed API origin before publishing the frontend; do not put secrets in this public file.

In Development, browse the API endpoint catalog at `http://localhost:5000/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`).

The API creates importer and exporter accounts. CHA signups are saved as `PendingCHA` and cannot use CHA endpoints until an administrator changes their role to `CHA`. Public signup never grants `Admin` or active `CHA` access.

## Sensitive data

KYC file bytes, importer-only RFQ details, bid payloads, messages, and contract terms are protected with ASP.NET Core Data Protection before being stored. Configure `DataProtection:KeyDirectory` to a persistent directory shared by API instances. Protect the key ring with an operating-system certificate or managed key service, restrict access to the API identity, and back it up: losing the keys makes encrypted records unreadable. Do not use an ephemeral container filesystem for this directory.

KYC download endpoints are restricted to active CHA/admin roles and log reads. Counterparty status responses do not include document bytes. Bid payloads stay encrypted until CHA unseals the RFQ; exporters never receive other bids. Messages are held for CHA review before counterparties can read them.

## External integrations still required

Escrow and settlement tables model the ledger state, but no payment gateway is configured, and this API does not move funds. Connect a verified payment provider and implement signed webhook handling before enabling deposits or automatic release. CHA approval is an administrator provisioned account role. Run behind HTTPS, add rate limits and monitoring to auth endpoints, and use a managed key ring before production deployment.
