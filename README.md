# THE GATE — Africa Global Trade

THE GATE helps producers and buyers discover one another directly and structure
trade workflows. It is a gateway, not a middleman: negotiations and commercial
contracts remain between the trading parties.

## Run the local MVP

Requirements: Visual Studio with the .NET 10 SDK, or the .NET 10 SDK from a
terminal.

### Visual Studio

1. Open `THE GATE.slnx`.
2. If necessary, set `src/TheGate.Api/TheGate.Api.csproj` as the startup project.
3. Select the `http` launch profile and press **F5**.
4. The browser opens `http://localhost:5247/`. Use **Accéder à mon espace** to
   select a local demo persona.

The Development environment creates a local SQLite database and seeds three
illustrative offers on first run. No PostgreSQL server or identity-provider
credentials are needed for local evaluation. Delete
`src/TheGate.Api/App_Data/the-gate-dev.db` to reset the demo data.

### Terminal

```sh
dotnet run --project src/TheGate.Api/TheGate.Api.csproj --launch-profile http
```

The local workspace offers producer, buyer, inspector, logistics-provider, and
payment-partner demo personas. Demo sessions are simulated, fictional, and
Development-only; they are not user accounts and provide no production security.

## MVP workflows

- Producers publish offers and confirm buyer-created direct-trade records.
- Buyers browse/filter offers and record agreed quantities; this does not reserve
  stock until producer confirmation.
- Inspectors record independent measurements separately from declared quantities.
- Trade parties record compliance requirements, evidence references, and
  responsible-party attestations.
- Logistics providers create journeys and append sequential milestone reports.
- Payment partners report settlement status for recorded obligations; THE GATE
  does not move or custody money.
- Producer and buyer can each confirm closure after the recorded readiness gates
  are met.

Declarations and evidence references are not independently certified by THE
GATE. Provider reports are not live carrier/finance integrations.

## Test

```sh
dotnet build "THE GATE.slnx"
dotnet test "THE GATE.slnx"
```

Tests use isolated SQLite databases. The optional PostgreSQL concurrency test
requires `THE_GATE_TEST_POSTGRES_CONNECTION` to point to a disposable empty test
database.

## Production configuration

Non-Development environments use PostgreSQL and OIDC JWT validation. Configure
`ConnectionStrings__TradeDatabase`,
`Authentication__Jwt__Authority`,
`Authentication__Jwt__Issuer`, and
`Authentication__Jwt__Audience` through deployment environment variables or a
secret manager. Apply EF migrations as a deployment step. Never deploy the
Development environment or its simulated demo identities.

## MVP decisions still open

Production OIDC and organization onboarding, cancellations and quantity
releases, evidence storage and retention, corridor-specific compliance,
accredited inspections, real carrier/payment partner integrations, settlement
legal meaning, producer confirmation/e-signature requirements, tenant
administration, audit retention, and operational controls require product,
legal, or operational owners. See
[`docs/IMPLEMENTATION_BASELINE.md`](docs/IMPLEMENTATION_BASELINE.md) for the
API/configuration baseline and remaining decisions.
