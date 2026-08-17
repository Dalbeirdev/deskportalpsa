# Autotask PSA Connector

Datto Autotask PSA integration over the REST API v1.0. Wave-1 reference connector.

## Connection setup
A `PsaConnection` with `Provider = AutotaskPsa` and `ApiEndpoint` set to the account's **zone base
URL** (e.g. `https://webservices2.autotask.net/atservicesrest/`). Credentials are encrypted at rest
and referenced by `CredentialSecretRef`; the credential fields required are:

| Key | Value |
|---|---|
| `ApiIntegrationCode` | API tracking identifier |
| `UserName` | API user (resource) name |
| `Secret` | API user secret |
| `WebhookSecret` | (optional) HMAC secret for inbound webhook validation |

The factory reads these from the secret store and configures an HttpClient; raw secrets never touch
the database or logs.

## Capabilities
Create/update tickets, public + internal notes, attachments, time entries, SLA data, custom fields,
companies/contacts/technicians, queues, incremental sync, inbound webhooks. Max page size 500.

## Field semantics & limitations
- **Statuses / priorities / queues are numeric picklist ids.** The connector transmits values
  verbatim; portal ⇄ Autotask translation is the platform mapping engine's job, discovered live via
  `Tickets/entityInformation/fields`.
- **Notes use a numeric `publish` flag.** Public (client-visible) notes are written with
  `PublicPublishValue` (default 1); internal notes use `InternalPublishValue` (2) and are never
  mirrored to the portal.
- **No native create-idempotency.** Autotask cannot dedupe by an arbitrary key, so duplicate-create
  protection is enforced at the platform layer (sync-event idempotency), not in the connector.
- **Query payloads are PascalCase** (`MaxRecords`, `Filter`) — the connector overrides the default
  camelCase JSON policy to match Autotask.

## Error mapping
401 → Authentication · 403 → PermissionDenied · 404 → NotFound · 429 → RateLimited (honours
`Retry-After`) · 5xx → ProviderError · timeout → Timeout · other 4xx → InvalidRequest.

## Certification
The connector passes the shared connector certification suite (`ConnectorCertificationSuite`),
exercised end-to-end against an in-memory fake Autotask server (`FakeAutotaskServer`). A live-sandbox
integration pass is still required before production (status: *Ready for Integration Testing*).
