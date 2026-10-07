# Initial API implementation

This implementation is the first end-to-end Direct Trade slice. It does not
implement the full product vision, a client application, account provisioning,
contract execution, logistics, document storage, or payment settlement.

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
| `POST` | `/api/trades/{tradeRecordId}/verifications` | Inspector | Record a separate measured quantity and evidence reference |
| `GET` | `/api/trades/{tradeRecordId}/verifications` | Trade parties | Read verification reports |

The platform records parties' declarations; it does not become a party to their
commercial contract or independently accredit inspectors.

## Configuration and database

Provide configuration through the deployment environment or a secret manager;
do not commit credentials.

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
  workflows before pending or confirmed records can be changed or closed.
- Decide which evidence storage, retention, source-verification, and inspector
  accreditation arrangements are acceptable. Current reports store references
  only.
- Define legal review and signing requirements for recording an external
  producer–buyer agreement. Current producer confirmation is an auditable
  platform action, not an electronic signature or legal opinion.
- Define tenant administration, producer ownership checks, audit retention,
  rate limits, deployment TLS, backups, and operational monitoring before
  production use.
