# Sync Hub

A demo integration pipeline connecting **SAP Business One** (order management) and **Wrike** (fulfillment tracking) — the two systems don't talk to each other natively, so this is the middleware layer that keeps them in sync.

**[Live demo →](https://sugarsheriff.github.io/sync-hub/)**

## What this is

An interactive dashboard that simulates a real integration pattern: a SAP B1 order comes in, gets picked up by a sync queue, gets transformed and validated, then gets pushed to Wrike as a fulfillment task — with retry/backoff on transient failures and a dead-letter path for anything that doesn't recover.

The dashboard itself is a simulation (static HTML/CSS/JS, no live SAP or Wrike connection). The `/src` and `/scripts` folders contain the real, runnable pieces this kind of integration needs in production:

| File | Language | Purpose |
|---|---|---|
| [`src/IntegrationService.cs`](src/IntegrationService.cs) | C# | Webhook receiver with exponential backoff and dead-letter queue |
| [`src/mapping.ts`](src/mapping.ts) | TypeScript | Typed field-mapping contract between the two systems |
| [`scripts/Check-SyncHealth.ps1`](scripts/Check-SyncHealth.ps1) | PowerShell | Scheduled ops check for queue depth and stuck records |

## Why it's built this way

- **Retry only on transient failures.** A 503 from Wrike gets retried with backoff; a 400 (bad payload) fails immediately and gets dead-lettered — retrying a bad request just wastes time and hides the real bug.
- **Typed mapping layer.** The SAP ↔ Wrike field contract is explicit and typed, so a schema change on either side breaks the build instead of failing silently at runtime.
- **Observability isn't an afterthought.** The health check script exists because "it usually works" isn't good enough for a sync job nobody's watching — stuck records should page someone, not wait to be discovered by an angry customer.

## Running the demo locally

```bash
git clone https://github.com/SugarSheriff/sync-hub.git
cd sync-hub
# just open index.html in a browser — no build step, no dependencies
```

## Author

Built by [Liam Hulsey](https://sugarsheriff.github.io/lookatme/) — integration engineer working across SAP Business One, Wrike, SQL Server, and EDI.
