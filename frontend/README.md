# Signal Desk frontend

Angular 22 operations console for the Notification Routing Service. The UI intentionally uses the existing HTTP API as its only source of routing, filtering, idempotency, delivery, retry, and archive behavior.

## Run locally

Start the API from the repository root:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/NotificationRouting.Api --urls http://localhost:5082
```

Then start Angular:

```bash
cd frontend
npm install
npm start
```

Open `http://localhost:4200`. The development proxy forwards `/api` and `/health` to port `5082`, so CORS is neither required nor enabled.

## Quality checks

```bash
npm test
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run format:check
npm run build
```

The console covers the local inbox/archive workflow. Webhook registration stays outside the UI because endpoints require an explicit server-side allowlist. Data remains process-local and is lost whenever the API restarts. If a subscription succeeds but its HTTP response is lost, use reset to create a new uniquely named workspace; without a subscription query endpoint the console cannot safely infer whether a later `409` represents that lost success or a real conflict.
