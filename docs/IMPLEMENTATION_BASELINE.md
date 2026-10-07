# Initial API implementation

This implementation covers the public discovery and first administrative
Direct Trade workflows. It does not implement the full product vision, account
provisioning, regulated operations, actual funds movement, document storage, or
provider integrations.

## Decisions in this slice

- PostgreSQL is the production database. EF Core migrations are explicit; the
  API does not create or alter production schemas at startup.
- Identity is delegated to an OpenID Connect issuer configured by deployment.
  THE GATE does not issue credentials or store passwords. Its API validates the
  issuer, audience, lifetime, and signature from the issuer's discovery keys.
- The trusted issuer must include `organization_id` and
  `organization_role` (`producer`, `buyer`, or `inspector`) claims. Only claims
  from a validated token are used to determine an actor's organization and role.
- Public offer discovery is read-only. Creating offers, recording trades,
  confirming trades, and recording inspections require the corresponding role.
- A buyer's trade record is pending and does not reserve quantity. The producer
  named on the offer must confirm that the parties reached an agreement outside
  the platform. Confirmation and quantity allocation happen in one database
  transaction; a conditional PostgreSQL update prevents concurrent
  over-allocation.
- Declared offer quantities and independently reported measured quantities are
  separate values. A stored evidence reference is not proof that a source,
  signature, or inspection was officially verified.
- Trade-party-selected compliance tasks record their source, responsible party,
  evidence reference, and responsible-party attestation. THE GATE does not
  calculate legal requirements or certify documents.
- Logistics providers can record one shipment's ordered journey milestones.
  Their references and updates are declarations, not live carrier tracking or
  independent proof of delivery.
- Payment obligations record a declared amount/currency and a named external
  partner reference. The designated partner organization may record that it
  reported settlement. THE GATE does not issue payment instructions, receive
  payment webhooks, custody money, perform FX, or verify provider statements.
- Closing a trade requires both trade parties to confirm and every compliance
  task, payment obligation, and shipment recorded in THE GATE to reach its
  designated terminal state. Omitting a record does not prove that an
  off-platform obligation has been fulfilled.
- A same-origin web client provides public offer discovery and browser workflow
  forms. Development uses simulated, Development-only cookie personas; there are
  no real local accounts or passwords. Non-development deployments continue to
  validate OIDC JWTs and must not enable demo identities.

## API surface

| Method | Path | Access | Behavior |
| --- | --- | --- | --- |
| `GET` | `/health` | Public | Process health |
| `GET` | `/api/offers` | Public | Up to 100 offers |
| `GET` | `/api/offers/{offerId}` | Public | One offer |
| `POST` | `/api/offers` | Producer | Publish declared quantity and optional HTTPS contact link |
| `POST` | `/api/offers/{offerId}/trades` | Buyer | Record buyer's declared agreement; no stock reservation |
| `POST` | `/api/trades/{tradeRecordId}/confirm` | Producer | Confirm the trade and atomically allocate stock |
| `GET` | `/api/trades/{tradeRecordId}` | Trade parties | Read the administrative trade record |
| `GET` | `/api/trades/mine` | Producer or buyer | List records involving the caller's organization |
| `POST` | `/api/trades/{tradeRecordId}/verifications` | Inspector | Record a separate measured quantity and evidence reference |
| `GET` | `/api/trades/{tradeRecordId}/verifications` | Trade parties | Read verification reports |
| `GET` | `/api/account/me` | Authenticated user | Read identity and organization claims from the trusted token |
| `GET` | `/api/trades/{tradeRecordId}/compliance-tasks` | Trade parties | Read tasks declared for the trade |
| `POST` | `/api/trades/{tradeRecordId}/compliance-tasks` | Trade party | Record a requirement and accountable organization selected by a party |
| `POST` | `/api/compliance-tasks/{taskId}/evidence` | Responsible organization | Record an evidence reference |
| `POST` | `/api/compliance-tasks/{taskId}/attest` | Responsible organization | Attest the recorded task |
| `GET` | `/api/trades/{tradeRecordId}/payment-obligations` | Trade parties | Read declared payment obligations and partner-reported status |
| `POST` | `/api/trades/{tradeRecordId}/payment-obligations` | Payer or beneficiary | Record an external obligation and designated partner |
| `POST` | `/api/payment-obligations/{obligationId}/partner-report` | Designated payment partner | Record the partner's reported settlement status |
| `POST` | `/api/trades/{tradeRecordId}/shipments` | Logistics provider | Create an identified journey |
| `POST` | `/api/trades/{tradeRecordId}/shipments/{shipmentId}/milestones` | Assigned provider | Append the next ordered milestone |
| `GET` | `/api/trades/{tradeRecordId}/logistics-milestones` | Trade parties | Read recorded journey events |
| `POST` | `/api/trades/{tradeRecordId}/closure-confirmations` | Producer or buyer | Confirm closure after recorded readiness gates pass |

The platform records parties' declarations; it does not become a party to their
commercial contract or independently accredit inspectors. The responsive client
at `/` provides public discovery; `/workspace.html` provides Development-only
persona sign-in and forms for existing API workflows. Development uses local
SQLite and sample listings without requiring PostgreSQL or identity-provider
credentials. The browser workspace is for local MVP evaluation only: real users
require a production OIDC provider and account/organization provisioning.

## Configuration and database

Provide configuration through the deployment environment or a secret manager;
do not commit credentials.

### Run locally with Visual Studio

Open `THE GATE.slnx`, set `src/TheGate.Api/TheGate.Api.csproj` as the startup
project if needed, and press **F5** with the `http` launch profile. Visual Studio
opens `http://localhost:5247/`. Previously the API did start, but no browser
opened because `launchBrowser` was `false` in the API project's
`Properties/launchSettings.json`.

The Development profile initializes
`src/TheGate.Api/App_Data/the-gate-dev.db` and seeds illustrative listings on
first launch. Select **Accéder à mon espace** and choose a demo persona to use
the browser workflows. These fictional identities require no password and are
available only when `ASPNETCORE_ENVIRONMENT=Development`; never deploy that
environment to production. Delete the SQLite file to reset local sample data.
SQLite is intentionally Development-only; deployed environments use PostgreSQL
and explicit EF migrations.

The same local profile can be started from a terminal:

```sh
dotnet run --project src/TheGate.Api/TheGate.Api.csproj --launch-profile http
```

Outside Development, configure a real HTTPS OIDC issuer and PostgreSQL
connection string. SQLite startup is rejected outside Development. The example
identity URLs below are placeholders, not a configured identity service.

```sh
export ConnectionStrings__TradeDatabase='Host=localhost;Database=the_gate;Username=the_gate_app'
export Authentication__Jwt__Authority='https://identity.example.org'
export Authentication__Jwt__Issuer='https://identity.example.org/'
export Authentication__Jwt__Audience='the-gate-api'
```

The sample identity URLs above are placeholders, not a configured service. The
issuer must match the OIDC provider's published issuer exactly. Use TLS for the
API and restrict database credentials to the required database operations.

Install the matching EF tool if needed and apply migrations as a deployment
step:

```sh
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef database update \
  --project src/TheGate.Infrastructure/TheGate.Infrastructure.csproj \
  --startup-project src/TheGate.Infrastructure/TheGate.Infrastructure.csproj
dotnet run --project src/TheGate.Api/TheGate.Api.csproj
```

## Tests

```sh
dotnet test 'THE GATE.slnx'
```

The API workflow tests use an isolated SQLite database by default. To also run
the concurrent-allocation test against PostgreSQL, set
`THE_GATE_TEST_POSTGRES_CONNECTION` to a **disposable, empty test database**
connection string before running the test suite. That test creates its schema
with EF Core and issues concurrent producer confirmations.

## Decisions still requiring product or operational owners

- Select and operate the production OIDC provider, including MFA policy,
  onboarding, organization membership, claim issuance, and role administration.
- Define trade-record expiry, rejection, cancellation, and quantity-release
  workflows. There is not yet an API to cancel or release a confirmed trade.
- Decide which evidence storage, retention, source-verification, and inspector
  accreditation arrangements are acceptable. Current reports store references
  only.
- Define and validate corridor-specific compliance requirements with qualified
  parties. A source string and a responsible-party attestation are workflow
  metadata, not legal validation.
- Integrate selected carriers and finance partners using authenticated,
  idempotent webhook protocols before treating their updates as authoritative.
  Current provider reports are authenticated organization claims entered
  through the API, not partner integrations or reconciled evidence.
- Decide the legal meaning and retention requirements of partner-reported
  settlement and bilateral trade closure. Current states record party/provider
  assertions only.
- Define legal review and signing requirements for recording an external
  producer–buyer agreement. Current producer confirmation is an auditable
  platform action, not an electronic signature or legal opinion.
- Define tenant administration, producer ownership checks, audit retention,
  rate limits, deployment TLS, backups, and operational monitoring before
  production use.
