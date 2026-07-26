# GitHub App task synchronization

Ideask links multiple GitHub repositories to one board (`WtBroad`). Linked issues become tasks, and supported task changes synchronize back to every repository linked to that board. This is a GitHub App integration: users install the app and select repositories, but never create webhooks or provide GitHub tokens to Ideask.

For workspace boards, a completed GitHub App installation is stored at the workspace level. Any board in that workspace reuses it and proceeds directly to repository selection; the user is not asked to install the app again. Personal boards retain an account-scoped installation.

## Deployment setup

Create a GitHub App owned by the WattEngine organization.

| GitHub App setting | Value |
|---|---|
| Setup URL | `https://<ideask-host>/api/github/installation-complete` |
| Webhook URL | `https://<ideask-host>/api/github/webhook` |
| Webhook secret | A random value also supplied as `GitHub:WebhookSecret` |

Subscribe to **Issues**, **Issue comments**, **Installation**, and **Installation repositories**. Grant repository permissions only for Metadata (read-only) and Issues (read/write).

Supply these deployment secrets and settings; do not commit them:

```json
{
  "GitHub": {
    "AppId": "123456",
    "AppSlug": "wattengine",
    "PrivateKeyPath": "/run/secrets/github-app-private-key.pem",
    "WebhookSecret": "same-secret-configured-in-github"
  }
}
```

`GitHub:PrivateKey` may be used instead of `PrivateKeyPath` when the PEM is injected directly by a secret manager. The app uses its private key only to mint short-lived installation tokens.

## Client integration

All requests below require the normal Ideask bearer token. The current board owner is authorized to link and manage the integration.

1. First request `GET /api/github/broads/{broadId}/installation`. If it returns an installation ID, go directly to repository selection. Otherwise request an installation URL and open the returned URL in a browser or popup.

   ```http
   GET /api/github/broads/{broadId}/install-url
   ```

2. The user completes GitHub's installation page. GitHub redirects to the configured setup URL, and Ideask records the installation against the board’s short-lived state.

3. Poll for the completed installation. A `404` means installation is still incomplete.

   ```http
   GET /api/github/broads/{broadId}/installation
   ```

   ```json
   { "installation_id": 12345678 }
   ```

4. List the repositories available to that installation and let the user select one or more repositories.

   ```http
   GET /api/github/broads/{broadId}/installations/{installationId}/repositories
   ```

5. Link the repository. Existing issues import immediately.

   ```http
   POST /api/github/broads/{broadId}
   Content-Type: application/json

   {
     "installation_id": 12345678,
     "owner": "solar-network",
     "repository": "WattEngine"
   }
   ```

6. `GET /api/github/broads/{broadId}` returns an array of linked repositories. `POST /api/github/broads/{broadId}/sync` queues background synchronization and returns immediately. Unlink one repository with `DELETE /api/github/integrations/{integrationId}`.

Show a clear error if an organization needs owner approval or the selected repository is not part of the app installation.

## User flow

1. In board settings, choose **Connect GitHub**.
2. Install the WattEngine GitHub App in GitHub and select the organization/account and repositories it may access. Organization policy may require owner approval.
3. Return to WattEngine and choose each authorized repository to link to the board.
4. Existing GitHub issues import as tasks. New tasks create corresponding GitHub issues in every linked repository. Changes to title, content, tags, and open/closed state synchronize in both directions.
5. Task comments synchronize with issue comments. GitHub-authored comments are read-only in WattEngine; users can edit or delete only their own local comments.

Unlinking stops sync but does not delete GitHub issues, comments, or the GitHub App installation. Removing the app or removing repository access automatically unlinks affected boards.

## Supported fields

| GitHub issue field | Ideask task field |
|---|---|
| Title | Name |
| Body | Content |
| Labels | Tags |
| Open / closed | Incomplete / completed |
| Issue comments | Task comments |

Pull requests are ignored. Groups, priority, due dates, assignments, attachments, sub-tasks, task deletion, and detailed completion reasons stay Ideask-only. Ideask also reconciles linked repositories every 15 minutes.
