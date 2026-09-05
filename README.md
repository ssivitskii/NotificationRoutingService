# Notification Routing Service

An in-memory ASP.NET Core backend for asynchronously dispatching notifications to topic subscribers. Recipient composition supports importance filtering, delivery logging, keyword alerts, user inboxes, a global archive, and allowlisted HTTP webhooks.

## Features

- Creates users and topics through a REST API.
- Subscribes users with an inclusive minimum-importance filter.
- Accepts publications into one bounded `System.Threading.Channels` queue and processes them with a single hosted consumer.
- Routes immutable target snapshots through Composite, Decorator, Strategy, and Adapter components.
- Tracks unread/read inbox state and archives every published message.
- Records per-target status and complete attempt history, with deterministic retry delays, dead-letter inspection, and safe manual retry.
- Deduplicates concurrent publication requests through a required `Idempotency-Key`.
- Sends webhook JSON through `IHttpClientFactory` with a stable delivery ID and an explicit host/scheme policy.
- Returns validation errors and expected failures as RFC-compatible Problem Details.
- Includes a responsive Angular 22 operations console for setup, publication, live delivery tracking, inbox/archive inspection, read state, and dead-letter retries.
- Limits request bodies to 128 KiB, caps and validates alert keywords, and exposes Swagger UI in Development plus a health endpoint in every environment.

## Tech Stack

C# · .NET 9 · ASP.NET Core Web API · Angular 22 · TypeScript · RxJS · System.Threading.Channels · BackgroundService · IHttpClientFactory · built-in dependency injection · Swagger/OpenAPI · xUnit · Vitest · WebApplicationFactory

## Architecture

`NotificationRouting.Domain` owns messages, topics, users, result values, and recipient patterns. A topic snapshots archive, local inbox, alert, and optional webhook targets when a publication is accepted. `NotificationRouting.Application` owns dispatch state, attempt history, retry decisions, idempotency, and use-case orchestration. `NotificationRouting.Infrastructure` supplies the bounded channel, hosted worker, thread-safe stores, webhook policy/client, and structured logging. `NotificationRouting.Api` maps HTTP DTOs to application operations and lifecycle resources.

The archive/local subscriber pipeline and each webhook are independent targets. Retrying a webhook therefore cannot duplicate an inbox or archive entry.

## Project Structure

- `src/NotificationRouting.Domain` — domain state and recipient pipeline.
- `src/NotificationRouting.Application` — use cases, delivery lifecycle, retry processing, and ports.
- `src/NotificationRouting.Infrastructure` — bounded queue, hosted consumer, concurrent stores, webhook adapter, and logging.
- `src/NotificationRouting.Api` — controllers, DTOs, Problem Details, Swagger, health.
- `frontend` — Angular operations console with a same-origin development proxy.
- `tests/NotificationRouting.UnitTests` — recipient, archive, formatter, and result behavior.
- `tests/NotificationRouting.IntegrationTests` — HTTP workflow through `WebApplicationFactory`.

## Getting Started

Requires the .NET 9 SDK. Dispatches, attempts, idempotency entries, dead letters, inboxes, and archive entries are intentionally process-local and reset on restart.

## Build

```bash
dotnet build NotificationRoutingService.slnx -c Release
```

## Run

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/NotificationRouting.Api --urls http://localhost:5082
```

Open Swagger at `http://localhost:5082/swagger` or check `GET http://localhost:5082/health`.

Swagger is intentionally registered only when the API runs in the `Development` environment.

## Run the operations console

Keep the API running on `http://localhost:5082`, then open a second terminal:

```bash
cd frontend
npm install
npm start
```

Open `http://localhost:4200`. The Angular proxy keeps browser requests same-origin; the API does not enable CORS. Use **Connect pipeline** to create a user, topic, and subscription in order, then publish and follow the delivery to a terminal state. The record panels expose the inbox, archive, read state, and any retryable dead letters.

If the subscription request reaches the server but its response is lost, the console cannot distinguish that success from a conflicting subscription because the intentionally small API has no subscription query endpoint. Reset the console to start a new uniquely named workspace in that rare case.

## Tests

```bash
dotnet test NotificationRoutingService.slnx -c Release
```

Frontend checks:

```bash
cd frontend
npm test
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm run format:check
npm run build
```

## Examples

Create a user:

```bash
curl -i http://localhost:5082/api/users \
  -H 'Content-Type: application/json' \
  -d '{"name":"Alice","alertKeywords":["security"]}'
```

Create a topic, substitute its returned ID, then subscribe the user:

```bash
curl -i http://localhost:5082/api/topics \
  -H 'Content-Type: application/json' \
  -d '{"name":"Operations"}'

curl -i http://localhost:5082/api/topics/TOPIC_ID/subscribers \
  -H 'Content-Type: application/json' \
  -d '{"userId":"USER_ID","minimumImportance":"High"}'
```

Publish and inspect the asynchronous dispatch:

```bash
curl -i http://localhost:5082/api/topics/TOPIC_ID/messages \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: operation-001' \
  -d '{"title":"Security notice","body":"Review required","importance":"Critical"}'

curl http://localhost:5082/api/deliveries/MESSAGE_ID
curl http://localhost:5082/api/users/USER_ID/messages
curl http://localhost:5082/api/archive
```

Publication returns `202 Accepted` and `Location: /api/deliveries/{messageId}`. Poll that resource until its status is `Succeeded`, `PartiallyFailed`, or `DeadLettered`. Replaying the same key and payload returns the same message ID with `Idempotency-Replayed: true`; changing the payload for that key returns `409`.

Dead letters and manual retry:

```bash
curl http://localhost:5082/api/deliveries/dead-letter
curl -i -X POST http://localhost:5082/api/deliveries/DELIVERY_ID/retry
```

Webhook registration is disabled until hosts are explicitly configured. HTTPS is required by default:

```json
{
  "Webhooks": {
    "AllowedHosts": ["hooks.example.test"],
    "AllowHttp": false
  }
}
```

Create a user with an optional `webhookUrl` only after its host is allowlisted. Redirects are disabled. HTTP may be enabled explicitly for a controlled local endpoint, but should remain disabled for normal use.

User creation accepts at most 10 case-insensitively unique alert keywords; each trimmed keyword must contain 1–50 characters. The included `NotificationRouting.Api.http` file contains starter requests for IDE HTTP clients.

## Design Decisions

One bounded channel and one consumer keep ownership and ordering understandable. `POST` only snapshots targets and enqueues; it does not deliver inline. Local recipients return `ValueTask` without artificial thread-pool work, while webhook I/O is genuinely asynchronous and cancellation-aware. Retryable failures are network/timeouts, HTTP 408/429/5xx, and unexpected recipient failures; other 4xx responses dead-letter immediately. Automatic delays are exponential without jitter, making behavior reproducible. Logging occurs only after the wrapped recipient succeeds.

## Limitations / Future Improvements

Data and accepted work are not durable, so a process crash can lose queued deliveries, attempt history, idempotency state, and dead letters. Graceful shutdown completes the writer and drains queued work until the host deadline; a forced stop cancels the active delivery. The single consumer also means a retry delay temporarily holds later messages. The API has no authentication, and in-memory history has no retention policy. A durable broker/database, authorization, and retention cleanup remain possible future improvements when those operational guarantees are required.
