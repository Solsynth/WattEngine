# Task integration providers

Ideask treats external task systems as providers. GitHub is the first provider, but task and comment services do not depend on it directly.

## Server architecture

`ITaskIntegrationProvider` is the provider boundary in `WattEngine.Ideask/Integrations/IntegrationContracts.cs`.

| Component | Responsibility |
|---|---|
| `ITaskIntegrationProvider` | Reconcile one integration and mirror local task/comment changes. |
| `IntegrationProviderRegistry` | Resolves a provider by `IntegrationProvider` enum value. |
| `IntegrationOrchestrator` | Broadcasts local task and comment changes to registered providers. |
| `IntegrationSyncQueue` / `IntegrationSyncWorker` | Runs potentially slow remote reconciliation in a scoped background worker. |

The queue payload is `(provider, integrationId)`, so it does not contain provider-specific credentials or objects. Each provider loads its own integration record and credentials inside the worker scope.

## Adding a provider

1. Add an enum value to `IntegrationProvider`.
2. Create provider-owned EF models and migrations. Keep remote IDs and provider credentials/configuration in those models; do not add provider fields to `WtTask` or `WtTaskComment`.
3. Implement `ITaskIntegrationProvider`:
   - `ReconcileAsync` imports remote changes for one integration.
   - `SyncTaskAsync` mirrors local task changes for that provider’s integrations.
   - `SyncCommentAsync` mirrors local comment changes and deletion.
4. Register the implementation as scoped in `AddAppBusinessServices` using `ITaskIntegrationProvider`.
5. Add a provider-specific controller or callback/webhook handler. It may enqueue `IntegrationJob(provider, integrationId)` after link/setup completes.
6. Define a provider-specific client API and UI. The common task APIs and realtime task updates stay unchanged.

Providers must make inbound webhook delivery idempotent, keep external IDs immutable, and catch outbound sync errors so a remote outage never blocks ordinary Ideask task CRUD.

## GitHub reference implementation

`GitHubIntegrationService` implements `ITaskIntegrationProvider`. It owns GitHub App installation grants, repository integrations, issue links, comment links, webhook validation, and GitHub API calls. The GitHub controller is intentionally separate from the generic dispatcher because installation and webhook protocols are provider-specific.
