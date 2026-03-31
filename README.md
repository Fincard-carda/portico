# TikoPay.Portico

`TikoPay.Portico` is the merchant platform backend for the TikoPay product family. It provides merchant directory management, federated `Tessera` auth integration, payment intent lifecycle APIs, reporting projections, realtime dashboard fan-out, and internal `Citadel` ingress for payment execution updates.

## Solution Layout

- `src/TikoPay.Portico.Api`: primary HTTP API host for auth/session, merchant operations, payment intents, public intent resolution, and internal `Citadel` ingress
- `src/TikoPay.Portico.Worker`: background processing host for expiry checks and future event-bus consumers
- `src/TikoPay.Portico.Realtime`: SignalR host for merchant-facing realtime payment/dashboard events
- `src/TikoPay.Portico.Persistence`: EF Core persistence, projections, seed data, and shared operational services
- `src/BuildingBlocks`: shared dispatch and infrastructure primitives
- `src/Contracts`: DTO and internal integration contracts
- `src/Modules`: modular domain boundaries such as `IdentityAccess`, `MerchantDirectory`, `PaymentIntents`, `PaymentTracking`, and `Reporting`
- `tests`: unit, integration, and architecture test projects

## Current Capabilities

- `Tessera` JWT federation bootstrap and local merchant access enrichment
- multi-merchant hierarchy scaffolding for merchants, branches, terminals, and users
- payment intent create, list, detail, cancel, and public token resolution endpoints
- scope-aware branch/terminal validation for payment intent creation and cancellation
- dashboard summary projection refresh and realtime notifications
- internal realtime dispatch from API/Worker to SignalR host
- internal `Citadel` payment status ingress for matched, started, succeeded, failed, and expired events
- development database migrations and seeded sample merchant/payment data

## Local Development

Development configuration is checked in with safe placeholders. Replace local secrets before running:

- `ConnectionStrings:PorticoDb`
- `TesseraFederation:Secret`
- `RealtimeDispatch:InternalApiKey`
- `CitadelIngress:InternalApiKey`

To run locally:

1. Set local configuration values in the host `appsettings.Development.json` files.
2. Run `TikoPay.Portico.Api`.
3. Run `TikoPay.Portico.Realtime`.
4. Optionally run `TikoPay.Portico.Worker` for expiry processing.
5. Authenticate against `Tessera` and obtain a JWT access token.
6. Call Portico endpoints with `Authorization: Bearer <token>`.
7. Use `/api/auth/me`, `/api/payment-intents`, `/api/payments`, `/api/dashboard/summary`, and `/public/payment-intents/resolve/{intentToken}` as the current integration surface.
8. For local `Citadel` simulation, call `/internal/citadel/payments/*` with `X-Portico-Internal-Key`.

## Security Notes

- HTTPS is enabled by default in all hosts.
- SignalR is authorization-aware from day one.
- Sensitive values in checked-in configuration are placeholders only.
- The dependency footprint is intentionally minimal while the platform surface is still stabilizing.

## Verification

Common local verification commands:

- `dotnet test`
- `dotnet build TikoPay.Portico.sln`

## Next Steps

1. Replace bootstrap merchant access with persisted membership and assignment resolution.
2. Apply branch/terminal scope rules consistently to all payment and reporting queries.
3. Switch internal `Citadel` ingress from HTTP simulation to the real message bus subscriber.
4. Add production-ready secret management and deployment configuration.
