# ConnectWise Manage Connector

ConnectWise Manage integration over REST API 3.0. Wave-1 reference connector (with Autotask).

## Connection setup
A `PsaConnection` with `Provider = ConnectWisePsa` and `ApiEndpoint` set to the instance API base
(e.g. `https://api-na.myconnectwise.net/v4_6_release/apis/3.0/`). The credential fields required are:

| Key | Value |
|---|---|
| `CompanyId` | ConnectWise company identifier |
| `PublicKey` | API member public key |
| `PrivateKey` | API member private key |
| `ClientId` | Developer `clientId` GUID |
| `WebhookSecret` | (optional) HMAC secret for callback validation |

Auth is HTTP Basic (`base64(CompanyId+PublicKey:PrivateKey)`) plus the `clientId` header.

## Terminology mapping (portal ⇄ ConnectWise)
| Portal | ConnectWise |
|---|---|
| Company | Company |
| Queue / Board | **Service Board** |
| Technician | **Member** |
| Category | **Type** |
| Ticket title | **summary** (capped at 100 chars) |
| Status / Priority | nested `{id, name}` references |

## Field semantics & limitations
- References are nested objects; the connector sends `{id}` when the mapped value is numeric,
  otherwise `{name}`. Production mappings supply ids.
- **Updates are JSON-Patch** operations replacing whole reference objects.
- Public vs internal notes use `internalAnalysisFlag` (public notes are not flagged internal);
  internal notes are never mirrored to the portal.
- List endpoints return **bare JSON arrays** (no envelope) — different from Autotask.
- Supports **outbound webhooks (callbacks)**, which Autotask does not — captured in the capability
  matrix so the UI/sync treat the providers differently rather than assuming parity.

## Error mapping
401 → Authentication · 403 → PermissionDenied · 404 → NotFound · 429 → RateLimited (honours
`Retry-After`) · 5xx → ProviderError · timeout → Timeout · other 4xx → InvalidRequest.

## Certification
Passes the shared `ConnectorCertificationSuite` end-to-end against an in-memory `FakeConnectWiseServer`,
and the cross-provider normalization tests confirm it yields the same `UnifiedTicket` shape as
Autotask. Status: *Ready for Integration Testing* (needs a live instance before production).
