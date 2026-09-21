# TILVEX POS Web

The `web` directory is the Phase 1 web sibling of the `TILVEX` WPF application. It remains independently deployable from the desktop client.

## Setup

Requirements: Node.js 25+, npm, and a local SQLite-compatible Prisma setup.

```powershell
Copy-Item .env.example .env
# Replace AUTH_SECRET with a long random value
npm install
npm run db:generate
npm run db:push
npm run dev
```

Open http://localhost:3000. `npm run lint`, `npm run typecheck`, and `npm run build` are the release checks.

## Desktop download

Registered users can download TILVEX Desktop from the authenticated dashboard. Build the Windows package before starting the web app:

```powershell
.\scripts\package-desktop.ps1
```

This creates `web/private-downloads/TILVEX-desktop.zip`, which is intentionally ignored by Git. In deployment, set `TILVEX_DESKTOP_DOWNLOAD_PATH` to the packaged archive location instead of storing the installer in the public web directory.

## What Phase 1 includes

- Next.js App Router with strict TypeScript, Tailwind, responsive dark command-center shell, and installable PWA manifest/service worker.
- Prisma SQLite schema for businesses, locations, users/roles, and durable sync events. `db:push` is intentionally used for this early schema; migration history can be introduced when the domain stabilises.
- Environment validation, secure credentials foundation (bcrypt password hashes, signed HTTP-only session cookie, role claims), and Zod request validation.
- Zustand client preferences and an IndexedDB-backed offline queue with a browser-only sync hook.
- Onboarding for business type and the five role-aware dashboard starter states.
- `/api/health` and `/api/auth/*` foundation endpoints.

The login and onboarding endpoints are production-shaped foundations, not a claim that a payment processor, accounting provider, tax service, hardware bridge, or external identity provider is live. Those integrations have explicit configuration points but are outside Phase 1.

## Phase boundaries

Phase 1 establishes identity, business context, local persistence, navigation, and offline plumbing. It does not process real card payments, print receipts, connect to fiscal/tax services, or synchronise with external systems. Those must be added behind authenticated, audited adapters in later phases.
