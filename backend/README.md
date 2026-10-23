# Backend

ASP.NET Core API (`UniVerein.Api`) with the EF Core data layer (`UniVerein.DAL`), the database
migrator (`UniVerein.DbMigrator`) and the integration tests (`UniVerein.IntegrationTests`).
For the full dev setup (`.env`, Docker stack, checks before a PR) see [CONTRIBUTING.md](../CONTRIBUTING.md).

## Fake SMTP server (Papercut)

The development stack in [`docker-compose.yml`](../docker-compose.yml) includes a
[Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) container. It accepts every
mail the backend sends and shows it in a web inbox, so mail features (mass mails, self-enrollment
confirmations, calendar invites and reminders, notifications) can be tested without sending real
emails. The Playwright e2e suite uses the same container to check sent mails.

| Purpose           | Container (`papercut`)  | On your machine         |
|-------------------|-------------------------|-------------------------|
| Web inbox         | `8080`                  | http://localhost:8080   |
| SMTP              | `2525`                  | `localhost:2525`        |

Papercut is only part of the dev/CI compose file. The production compose files do not start it.

### Mail settings in the app

The SMTP/IMAP settings are not environment variables. They are stored in the database and are
configured in the app under **Einstellungen -> Email konfigurieren** (or `PUT /api/mail`).
Use these values for Papercut:

| Field                    | Value                                     |
|--------------------------|-------------------------------------------|
| SMTP Host / Server       | `papercut`                                |
| SMTP-Port                | `2525`                                    |
| IMAP Host / Server       | `papercut`                                |
| IMAP-Port                | `2525`                                    |
| Benutzername / Email     | any non-empty value, e.g. `test`          |
| Passwort                 | any non-empty value, e.g. `test`          |
| Absender Email (From)    | any address, e.g. `noreply@test.de`       |
| Absendername             | any name, e.g. `Vorstand Test`            |

Papercut does not check credentials, so any username and password work. Papercut speaks plain
SMTP without TLS, so `enableSsl` must be `false`. The form does not show this option (it defaults
to `true` there). If mails do not arrive after configuring Papercut in the form, send the request
below instead.

The host name `papercut` only resolves inside the compose network, i.e. when the backend runs as
the `backend` container. If you run the API directly from your IDE or with `dotnet run`, use
`localhost` as host instead (the port stays `2525`).

The same settings as a request (this is what the e2e suite sends in `frontend/e2e/BackendClient.ts`):

```json
PUT /api/mail
{
  "smtpServer": "papercut",
  "port": 2525,
  "imapServer": "papercut",
  "imapPort": 2525,
  "username": "test",
  "password": "test",
  "fromMail": "noreply@test.de",
  "fromName": "Vorstand Test",
  "enableSsl": false
}
```

### Useful tips

- Open http://localhost:8080 to read the received mails.
- Delete all received mails (used by the e2e tests between runs): `DELETE http://localhost:8080/api/messages`.
- Received mails are kept in the container only. They are gone after `docker compose down`.
- The e2e suite reads the inbox URL from `PAPERCUT_BASE_URL` (default `http://localhost:8080`).
