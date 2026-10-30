# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.10.0] UV-29, UV-60, UV-64, UV-65, UV-66, UV-67, UV-68, UV-70

### Added
- UV-65: The membership fees page is now the income page "Einnahmen". The "Mitgliedsbeiträge" tab shows the SEPA fees, every further tab is a configurable income category ("Einnahmekategorien verwalten", e.g. "Spenden"). Admins and financial managers can add, edit, mark as paid, delete and (admins) restore incomes. Summary cards and a date range filter (default: current year) were added; the page works without a bank connection.
- UV-65: Payment acknowledgements ("Quittung"): a PDF with a consecutive number (`YYYY-NNNN`) for every paid fee or income. It is stored as issued, afterwards the payment can no longer be changed.
- UV-65: EÜR export as spreadsheet (`GET /finance/eur?year=YYYY`, from 2020, admins and financial managers, button on the "Finanzauswertung" page) with the sheets "EÜR", "Stammdaten", "Journal", "Beitragsnachweis" and "Hinweise". The tax checks use the limits of the exported year (45,000 € until 2025, 50,000 € / 50,000 € / 100,000 € from 2026). Opening balances and the cash/bank reconciliation are empty cells to fill in by hand.
- UV-65: Income and receipt categories have a tax area ("Steuerlicher Bereich", editable by admins and financial managers), the bank connection settings an optional "Steuernummer". **The migration sets all existing categories to "Ideeller Bereich", assign the areas before the first export.**
- UV-66: Demo: `docker-compose-demo.yml` builds nothing and needs no SQL dump. `VERSION` in `.env` selects the published images, `DEMO_DATA_PATH` is the data folder. With `Demo__Enabled=true` the backend wipes database and receipt files at every start and nightly at 03:00 UTC and generates sample data relative to the current date. Mails are caught by Mailpit at `/mail/`.
- UV-66: `GET /web-page-config` returns `demo`, so the published frontend image shows the demo hint without a `VITE_APP_DEMO` build.
- UV-67: Two-factor authentication (TOTP) under "Profileinstellungen" → "Sicherheit", with QR code and 10 recovery codes. With 2FA enabled `POST /auth/login` returns a challenge instead of a session, `POST /auth/login/2fa` completes the login. Wrong codes are locked out per address and per account (recovery codes still work during a lock). Admins can reset the 2FA of a user (`DELETE /users/{id}/2fa`). The secret is stored encrypted, optional setting `TwoFactor__Issuer`. Not available in demo mode; there is no recovery if the only admin loses device and recovery codes.
- UV-67: Logging out, changing the password and changing the 2FA end the sessions on all other devices (new column `Users.session_version`). **Everybody has to log in once again after the update.**
- UV-68: Stable booking numbers: `E-YYYY-NNNN` for fees and incomes, `A-YYYY-NNNN` for receipts, assigned when booked as paid, never changed or reused. Payments get an explicit payment date (default: today); EÜR, finance analytics and income summaries use it. `POST /contributions/{id}` and `POST /income/{id}` now take `{ "paid", "paymentDate" }` in the body instead of the query parameter `paid` (**API clients must send the body**), `POST /receipts/{id}/pay` and `POST /income` accept an optional `paymentDate`. Existing paid entries keep the date they were reported with before, so earlier figures do not move.

### Fixed
- UV-64: Backup restore: an uploaded SQL file can no longer run shell commands in the backend container (`psql` runs in restricted mode, needs PostgreSQL 17.6 or later and refuses the restore otherwise).
- UV-64: Demo mode on the server: backup download and restore answer 403 `DEMO_MODE_DISABLED`. Before, the demo mode only existed in the frontend, so anyone could use the admin-only restore.
- UV-64: Self-enrollment: all text fields have a maximum length and `POST /self-enrollment` rejects bodies above 32 KB. Requests above a body size limit (also receipt uploads) are answered with 413 instead of 500.
- UV-64: Receipt and income amounts are limited to 1,000,000. Before, receipts near the maximum decimal value made the finance analytics fail with HTTP 500. Amounts already stored above the limit are not changed.
- UV-64: CSV exports (receipts ZIP and member export) neutralise spreadsheet formulas (fields starting with `=`, `+`, `-`, `@`).
- UV-64: Receipt PDF preview: pages are rendered with a limited size, so a PDF with a huge page can no longer exhaust the browser. A tile that cannot be rendered shows a placeholder with the link to the original.
- UV-64: Mail settings: the SMTP/IMAP password must be entered again on every save, so the stored password cannot be redirected to another mail host.
- UV-65: Demo mode: the wipe of the database now needs an explicit `Demo__Enabled` of `true`, `1`, `yes` or `on`. Before, any other value (e.g. `disabled`) would have wiped a production database.
- UV-65: Income summaries assign paid amounts to the period by payment date, paid incomes without a category are no longer left out.
- UV-65: A deleted contribution can no longer be marked as paid.

### Changed
- UV-29: The login token is kept in an httpOnly cookie (`uv_auth`) instead of the `localStorage`, so injected scripts can no longer read it. Cookie requests that change data carry the header `X-UV-CSRF`; `Authorization: Bearer` keeps working. New endpoints `GET /auth/me` and `POST /auth/logout`. `?access_token=` on `/backup`, `/receipts/export` and `/emailProgress` is no longer accepted (**API clients must send `Authorization: Bearer`**). **Everybody has to log in once again, and `nginx.conf` must be updated together with the images** (`X-Forwarded-Proto`).
- UV-60: The "Finanzauswertung" replaces the receipt analytics and compares income and expenses (overview, by month and category, category table). Only paid entries count, each in the month of its payment date, so the balance matches the EÜR. **Open receipts no longer appear in the expenses until they are paid.** Only admins and financial managers can open it. `GET /receipts/analytics` is replaced by `GET /finance/analytics`.
- UV-64: Mail settings: an encrypted connection (SSL enabled) is enforced (TLS on ports 465/993, STARTTLS otherwise). Before, the mailbox password could be sent in cleartext when STARTTLS was missing. **Mail servers without encryption no longer work with SSL enabled.** The test mail reports why it failed.
- UV-65: Paid fees are no longer deleted automatically after 14 days. Fees already removed this way are restored by the migration.
- UV-70: Financial managers can open "Beitragstarifverwaltung", "Einnahmekategorien verwalten" and "Belegkategorien verwalten" (the "Einstellungen" menu shows only these three entries for them) and create, edit and delete contribution plans, income categories and receipt categories like admins. `POST`, `PUT`/`PATCH` and `DELETE` on `/contribution-plans`, `/income-categories` and `/receipt-categories` are no longer admin-only.

## [1.9.1] UV-8, UV-10, UV-55, UV-57, UV-61, UV-63

### Added
- UV-58: Membership fees page: new columns for member number and membership status, and a filter by membership status.
- UV-57: Membership fees page: admins and financial managers can delete open fees (with confirmation), e.g. returned direct debits. Deleted fees are soft-deleted and logged; admins can show and restore them.
- Audit log: user changes, creditor configuration, contribution plans, paid/open fees, SMTP/mail settings, backup restore and CSV member import are now logged. Passwords, IBAN/BIC and imported member data are not stored.

### Fixed
- Login lockout: the lockout is now counted per user name and client address instead of per account, so an attacker can no longer lock out a known user (e.g. `Admin`) permanently. IPv6 counts per /64 network, unknown user names are locked like known ones. The state is kept in memory and cleared by a backend restart.
- UV-10: Audit log: entries of deleted users are shown as "Deleted user" instead of being hidden.
- UV-61: Members page: switching the member category filter back to "Alle" shows all members again.
- UV-63: Deleted members and members whose exit date has passed are no longer billed (fee generation and SEPA export). A future exit date is still billed until that date.
- Self-enrollment: the SEPA mandate signature date is set by the server instead of the browser (`sepaConsent` is no longer accepted by `POST /self-enrollment`). HTML in applicant names is encoded in system e-mails and mass mail placeholders, name fields are limited to 100 characters.
- SEPA export: IBAN and creditor ID checks only accept the digits 0-9, so invalid IBANs can no longer break the `pain.008` file.
- Deleted members: users and financial managers can no longer read or change them via `PATCH`/`DELETE /members/{id}` (only admins can). A second delete no longer overwrites the original deletion time.
- Deleted or changed users: login tokens are now checked on every request against the current user and role (otherwise 401), instead of staying valid for up to 8 hours.
- The "new version available" e-mail was sent to the admins every hour when one admin had no e-mail address. Such admins are skipped now.
- UV-55: PWA icon on Android: the web manifest has the correct content type, and a monochrome icon is included for Android 16 themed icons. App icons are RGBA PNGs, the iOS icon is opaque.
- The English texts of the membership fees page were not shown because of a wrong translation key.

### Changed
- Client address behind a reverse proxy: `nginx.conf` no longer trusts `X-Forwarded-For` from the whole `172.16.0.0/12` range, which allowed bypassing rate limits and the login lockout and forging the IP of self-enrollments. Only Caddy of `docker-compose-tls.yml` is trusted (fixed address `172.31.250.2` in the network `edge`). **Installations with their own reverse proxy (README, option B)** must create the folder `trusted-proxies` next to the compose file and put `set_real_ip_from <proxy address>;` into a `.conf` file in it; the proxy must set `X-Forwarded-For` to the visitor address (nginx: `proxy_set_header X-Forwarded-For $remote_addr;`). Without it all visitors share one rate limit and one login lockout.
- Backend log: important actions (logins, user and member management, self-enrollment, CSV import/export, fees, SEPA exports, receipts, mail, calendar, backups, config, background jobs) are logged with IDs and the acting user's ID, plus failed token validation, forbidden access and rate limit hits. No personal data is logged any more (see `CONTRIBUTING.md`).
- Backend log: every request is logged once with status and duration; `/health` and the duplicate `REQUEST:` line (which leaked the calendar feed token) are gone. Failed backups/restores, unhandled exceptions (with stack trace), startup info and the hourly firmware check are logged properly; a leftover debug error on member updates is removed.

## [1.9.0] UV-6, UV-53, UV-54, UV-62

### Added
- UV-6: New club calendar ("Kalender") for event planning.
  - Every user can create events with a description, time or all-day, a location with address (with a map link), an online link and a colored category. Admins manage the categories under "Kalenderkategorien verwalten".
  - Events are visible to the whole club, only to invited users, or privately only to their owner (not even to admins).
  - Other users can be invited as attendees or co-editors. Co-editors can change the event together with the owner. If someone else saved in the meantime, the change is rejected with a reload hint instead of being silently overwritten.
  - Invited users answer with yes, maybe or no.
  - Events can repeat daily, weekly, monthly or yearly. They keep their local time across daylight saving time, and single dates can be cancelled.
  - Reminders can be sent from "at the start" up to one week before.
  - Users can subscribe to events and are notified about invitations, changes, deletions, replies and reminders. Notifications appear in the bell, which is now shown for every role, and by e-mail with an `.ics` invitation. Each kind of e-mail can be turned off in the profile settings. Notifications in the bell can be deleted one by one, and all already read ones can be deleted at once with "Gelesene löschen".
  - Every user can subscribe to their personal calendar in Apple Calendar, Google Calendar or Outlook through a secret webcal link that can be revoked at any time.
  - Admins can enable a public calendar. It shows single events or whole categories marked as public, without participants or other personal data, at `/calendar/public` and as a webcal feed. Optionally it is only reachable through a secret link.
  - The list view of the current month hides past events behind a "Vergangene Termine anzeigen" button, in the club calendar and in the public calendar.
  - On phones the bottom navigation shows "Kalender" instead of "Auswertung". The receipt analytics stay reachable through the menu.
  - Adds the migrations `AddCalendar`, `AddCalendarCollaboration`, `AddCalendarRecurrenceAndReminders` and `AddCalendarFeedsAndPublicCalendar`, and the backend dependency `Ical.Net` and the frontend dependency `FullCalendar` (both MIT).

### Fixed
- UV-53: The description field in the receipt form is now full width and multi-line instead of a small single-line field, so longer descriptions fit.
- UV-54: The public self-enrollment form now has a proper title ("Bei Verein <Vereinsname> beitreten", or "Dem Verein beitreten" without a club name) and a subtitle addressed to the applicant. Previously it showed the title and text of the admin's "new member" dialog.
- UV-62: Admins can change their own username, e-mail and password again. Only an actual change of their own role is still rejected.

## [v1.8.4] UV-51

### Fixed
- UV-51: Opening "Mail" on an iPhone no longer shows a blank white screen.
- Errors on a page now show a message with a reload button instead of a blank screen, the navigation stays usable.
- After a deploy, an open app reloads once if a page file of the old version is missing.

## [v1.8.3] UV-48, UV-49, UV-50

### Changed
- The frontend Docker image is built with Node 24 (`node:24-alpine`) instead of Node 20, which is end-of-life since April 2026. This matches the Node version used in CI. The runtime image is unchanged (nginx).

### Fixed
- UV-48: The icons in the mobile bottom navigation are aligned again. Inactive tabs hid their label only visually, so "SEPA Export" wrapped to two lines and pushed the bank icon up. All tabs now show a short, single-line label (e.g. "SEPA", "Mail", "Auswertung") and the active tab no longer shifts. 
- UV-49: The user management no longer overflows the screen on mobile: the long "Benachrichtigungseinstellungen" tab label widened the whole page, it is now shortened to "Benachrichtigungen", the tabs scroll if space runs out, and the tab indicator stays inside the rounded corners.
- UV-50: The SEPA export (`pain.008.001.08`) follows the EPC rules for SDD Core 2025 and the address rules from 15 November 2026: addresses are fully structured, text uses the SEPA character set (e.g. `ä` → `ae`), and every transaction gets a unique ID. Members without a BIC are exported as `NOTPROVIDED` instead of being skipped, the club's BIC, IBANs and creditor identifier are checked including check digits. An export without collectible contributions returns a message instead of an empty file.

## [v1.8.2] UV-17, UV-24, UV-26, UV-30, UV-47

### Changed
- UV-24: The sidebar shows the club logo from the general settings in the top-left corner, both expanded and collapsed. Previously it showed the first letter of the club name, which is still used when no logo is uploaded.

### Security
- UV-17: Mail bodies sent via `/mail/send` are sanitized server-side with `HtmlSanitizer`. Only the formatting the mail editor can produce is kept (text, headings, lists, links, images, color and alignment). Scripts, event handlers, `javascript:` links, forms and iframes are removed, and `data:` URLs are only allowed for inline images, not SVG. Markdown release notes in firmware update mails no longer render raw HTML (`DisableHtml()`).
- UV-26: Member IBANs are only returned in full to ADMIN and FINANCIAL_MANAGER. The USER role now gets them masked except for the last 6 characters (member list, member create/update responses and the "Ausstehende Mitglieder" review queue), so they can still check whether an IBAN is correct and replace it with a new one. A masked IBAN sent back to the API is treated as unchanged and never stored.
- UV-30: Changing your own password now requires the current password, both in the profile settings and when an admin edits their own account in the user management, the field is shown as soon as the edit dialog opens. Wrong attempts count towards the login lockout. Admins can still reset other users' passwords without it.

### Fixed
- UV-47: The mail settings have a new optional "Absendername" (sender name) field that recipients see as the sender, e.g. `Vorstand <vorstand@verein.de>`. Previously the "Absender Email (From)" value was used as both name and address, so entering a name there made sending fail. The sender email field now only accepts a valid address. Adds the migration `AddMailFromName`. Existing settings keep working with an empty name.

## [v1.8.1] UV-18, UV-25, UV-27, UV-28, UV-31, UV-32, UV-33, UV-34, UV-35, UV-37, UV-38, UV-39, UV-40, UV-41, UV-42, UV-43, UV-44, UV-45, UV-46

### Added
- UV-27: Added `docker-compose-tls.yml` + `Caddyfile` for automatic HTTPS via Let's Encrypt, now the recommended installation path. The self-update flow supports both compose files.
- UV-42: Added `npm audit` to CI and Dependabot for npm, NuGet, Docker and GitHub Actions.

### Changed
- UV-40: `Serilog.Sinks.File` pinned to stable `7.0.0`, unused `BCrypt.Net-Next` removed.

### Security
- UV-25: The default admin account is only seeded when no users exist, its default password must be changed on first login, and admins can no longer delete themselves or change their own role.
- UV-28: CORS origins are configured via `Cors:AllowedOrigins` (empty in production) instead of allowing any `localhost` origin; `AllowCredentials()` removed.
- UV-31: Added an IP-based rate limiter to `/auth/login`.
- UV-32: Receipt uploads are validated by magic bytes instead of the client `Content-Type` (SVG and unknown formats rejected), downloads send `nosniff` and `Content-Disposition: attachment`.
- UV-33: Both nginx configs send security headers (CSP, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`).
- UV-34: `X-Real-IP` is only trusted from private/internal networks, so IP-based rate limits can no longer be spoofed.
- UV-35: Self-enrollment confirmation links use a stored `PublicBaseUrl` instead of the request's `Host` header, preventing phishing links via forged hosts.
- UV-37: All containers run as non-root users, with `cap_drop: [ALL]` and `no-new-privileges` where possible. No manual migration steps required.
- UV-38: DB passwords are now randomly generated by the init script, which no longer prints secret values to the log.
- UV-39: `.env.example` no longer ships a real `CRYPTO_KEY`/`HMAC_KEY`, only placeholders.
- UV-41: Endpoints now require authentication by default (`FallbackPolicy`), intentionally public ones are marked `[AllowAnonymous]`.
- UV-43: Request body limit reduced from 10 GB to 100 MB globally, only `/api/backup/restore` keeps 10 GB (nginx and Kestrel). Receipt files and the member CSV import are capped at 10 MB per file, oversized PDFs are rejected client-side.
- UV-44: Login no longer allows username enumeration via response timing, and no longer logs raw usernames.
- UV-45: Failed login attempts expire after 15 minutes, and the lockout is capped at 15 minutes instead of 24 hours.

### Fixed
- UV-18: Fixed a blank screen after login caused by stale lazy-loaded chunks, the app now reloads automatically when a new service worker takes over.
- UV-46: The firmware-update hint now compares real versions and only shows when a newer version actually exists.

## [v1.8.0] UV-19, UV-20, UV-21, UV-22, UV-23

### Added
- UV-21: Self-enrollment (UV-13) is now hardened for permanent use: the public form no longer creates a member directly, but requires email confirmation (48h link) before entering an "Ausstehende Mitglieder" review queue, where any logged-in user can approve or reject it. Applicants provide a "Motivation" text plus required IBAN/BIC instead of self-assigning a category, which a reviewer assigns on approval. Also adds per-IP rate limiting, stricter email validation, a mail-settings prerequisite before enabling self-enrollment, a mobile-friendly pending-members view, and various UX/validation fixes.
- UV-22: Any logged-in user, regardless of role, can now opt in (off by default) to an email notification whenever a new self-enrollment is confirmed and ready for review, via a new toggle in their notification settings, the mail is sent to every subscribed user, not just a single fixed address.

### Changed
- UV-20: Cleared out every remaining frontend ESLint/TypeScript warning: `any`-typed props, state, and API helpers (`api`/`apiFile`, `AuditLog`, `Contribution`/`ContributionInfo`, SignalR payloads, TipTap's `toggleUnderline`, the iOS `navigator.standalone` check, …) were replaced with concrete types; data-loading functions used inside `useEffect` are now wrapped in `useCallback` and listed in their effect's dependency array instead of being omitted, and the `useCallback`-plus-`@ts-expect-error`-wrapped `lodash.debounce` calls (Members, Receipts, Sepa, Contributions, RecipientList) were replaced with properly typed `useMemo`-based debouncing so no dependency is silently dropped.
- UV-20: Every remaining unavoidable `setState` call inside an effect body (token/session checks, responsive layout resets, debounced search effects, etc.) is now explicitly annotated with `// eslint-disable-next-line react-hooks/set-state-in-effect` instead of triggering a warning.
- UV-20: `PageConfigContext`, `SnackbarContext`, and `ThemeModeContext` each had their hook (`usePageConfig`/`useSnackbar`/`useThemeMode`) and context object moved out into a dedicated `hooks/use*.ts` file, so the `*Context.tsx` files only export the provider component — this satisfies `react-refresh/only-export-components`, which flagged them for exporting non-component values alongside a component. `MuiIcons.tsx`'s `loadAllIcons` helper was split out into `utils/iconLoader.ts` for the same reason.
- UV-20: The dashboard now reads the logged-in user's name/role/id from the JWT synchronously via a lazy `useState` initializer instead of after mount via an effect, avoiding an initial render with an empty user before the token is parsed.

### Fixed
- UV-23: A regular user could soft-delete their own receipt even after a Financial Manager/Admin had already marked it as paid, since the delete endpoint only checked receipt ownership and never its paid status (unlike editing, which was already locked once paid). Deleting a paid receipt as a non-privileged user now returns 403 Forbidden and the delete button is hidden for it in the UI. Admins and Financial Managers can still delete paid receipts.
- UV-19: Independently of the above, focusing then blurring an empty outlined field (label shrinking then unshrinking) left a stray fragment of the old border visible in Safari until something else forced a repaint (hovering the field, resizing the window) - a known, still-open WebKit rendering bug in MUI's `notchedOutline`/`legend` mechanism ([mui/material-ui#46891](https://github.com/mui/material-ui/issues/46891)). Fixed globally via the theme with the community-verified workaround: forcing the notch's `<legend>` to stay `visibility: visible` under `@supports (-webkit-appearance: none)`, scoping it to WebKit-based browsers.
- UV-20: Audit log table and card rows were all keyed on `l.id`, a field `AuditLog` entries don't have (it only surfaced once the `any` typing was removed); they're now keyed on `` `${l.timestamp}-${index}` ``, giving React stable, non-colliding keys instead of every row sharing the same `undefined` key.

## [v1.7.0] UV-13

### Added
- Self-enrollment: prospective members can now sign themselves up through a public form, without needing an account. Fields that are admin-only or set automatically (task within the club, entry/exit date, SEPA consent) are hidden from the form and forced server-side regardless of what the request body contains (task is always "Member", entry date and SEPA consent are set to "now"). Duplicate email addresses/IBANs are rejected the same way as when an admin creates a member.
- A "Self-enrollment" toggle in the general club settings enables/disables the public form, together with a button to copy its shareable link while disabled, both the public page and its `/self-enrollment/form-data` endpoint serve nothing.

### Changed
- Member-creation logic (duplicate detection, member-number assignment, contribution-plan/member-category validation) was consolidated into a shared `MemberService.CreateMemberAsync`, now used by both the admin "create member" endpoint and self-enrollment instead of being duplicated across the two.
- The `/member-categories`, `/contribution-plans`, and `/self-enrollment/form-data` endpoints now share their EF Core query logic through a new `ReferenceDataService` instead of each maintaining its own copy of the same projection.
- `frontend/nginx.conf` now serves the SPA with a `try_files` fallback to `index.html`, so a directly opened or shared self-enrollment link resolves correctly instead of 404 status code.

### Fixed
- Bumped `fast-uri` (via a `package.json` override on its transitive dependency) and `@tiptap/*` to close four High-severity SSRF/host-confusion advisories and one Moderate-severity prototype-pollution-to-XSS advisory reported by Dependabot.

## [v1.6.0] UV-16

### Added
- Receipt notifications: Financial Managers now receive an email whenever a new receipt is submitted by a regular user or an admin, enabled by default and configurable via a new "Notification settings" tab in their own account settings (visible to every role as a placeholder for future settings, but the receipt-notification toggle itself only shows for Financial Managers). A Financial Manager is never notified about a receipt they submit themselves. Backed by a new generic per-user settings table (type + enabled flag) designed to hold further notification/preference types in the future without further schema changes.

## [v1.5.0] UV-7

### Added
- Receipt management: any user can submit a receipt (amount, date, optional category/vendor/description, and one or more photo/PDF attachments) from the new "Receipts" page; Admins and Financial Managers see and manage everyone's receipts (with a "submitted by" column), can set/confirm the payment method, mark receipts as paid, and soft-delete/restore/permanently delete them, while regular users can only edit or delete their own unpaid receipt within 15 minutes of submitting it. The list supports filtering by date range, category, and paid/open status (plus showing deleted receipts for Admins), and privileged roles can export the currently filtered receipts as a ZIP containing a CSV plus all attached files.
- On-device OCR when attaching a receipt photo or PDF: the amount and date fields are auto-filled by scanning the attachment locally in the browser (German amount/date recognition, no server round-trip) if they haven't already been filled in manually on installed PWAs a dedicated camera-capture button is offered alongside the regular file picker.
- Receipt categories: a new "Receipt categories" settings page (Admin) to create and delete the categories used to tag and group receipts, a category can't be deleted while still assigned to an active receipt.
- Receipt analytics page (Admin/Financial Manager): year-scoped charts of spending broken down by category a stacked monthly bar chart, a category breakdown pie chart with the yearly total in the center, and a year-over-year totals chart.
- Full system backup: the Backup page can now also download a full backup (`Verein_Backup_Full_<date>.zip`) containing both the database dump and every uploaded receipt file, and restore accepts either a plain `.sql` dump or such a full `.zip` backup.

### Changed
- Backup downloads are now streamed directly to the response instead of being written to a temporary file first and served afterwards.
- `nginx.conf`'s `client_max_body_size` was raised from 50M to 10G and proxy buffering/timeouts were relaxed, to allow the new large receipt exports/full backups to pass through; access logs now strip the query string so the download link's access token isn't written to disk.
- `docker-compose*.yml` gained a `./receipts:/app/receipts` volume so uploaded receipt files persist across container restarts/updates.

### Fixed
- The demo-login dialog's helper text and account labels were using the theme's `text.secondary`/`text.primary` tokens, which turn light-colored in dark mode and became illegible against the dialog's always-light amber background; they're now fixed dark colors matching that background.

## [v1.4.0] UV-5, UV-12

### Added
- Dark mode: the app follows the OS/browser color scheme (`prefers-color-scheme`) by default, or can be set manually to light/dark via a toggle next to the language switcher in the top bar; the choice is persisted in `localStorage` and survives reloads. As part of this, surfaces that were previously hardcoded to light-mode colors (the main content background, table headers, the Backup/EmailEditor/SendProgress panels, the top bar, the login page's background gradient and fallback lock icon) were made theme-aware, a lighter shade of the brand blue is used for `primary.main` in dark mode to keep focus rings/labels/buttons legible, and the login page's logo/icon now has a subtle border in both themes.

- `UniVerein.DbMigrator`, a new console tool published as `ghcr.io/uni-verein/univerein-db-migrator`, that copies all tables (including soft-deleted rows) from an existing MariaDB database into a fresh PostgreSQL database in foreign-key-safe order, verifying row counts per table.

### Changed
- Replaced MariaDB with PostgreSQL (`postgres:17-alpine`) across every `docker-compose*.yml`, the backend's EF Core provider (Pomelo → Npgsql), and the backup service (`mariadb-dump`/`mariadb` → `pg_dump`/`psql`).
- `docker-compose-update.yml` now detects whether the running stack is still on MariaDB. If so, it first updates installations that aren't yet on `v1.3.1` to that version (unchanged, MariaDB-only behavior); once an installation is on `v1.3.1` with MariaDB, the next update migrates the database (schema and data) to PostgreSQL and switches the whole stack over, including backups. Already-PostgreSQL installations keep updating exactly as before.
- The MariaDB → PostgreSQL cutover runs against an isolated, temporary PostgreSQL instance so the live MariaDB container keeps serving until the copy is verified complete; on success the old MariaDB container is stopped (its data volume is kept, not deleted) and a copy of the pre-migration `docker-compose-prod-image.yml` is kept as `docker-compose-prod-image.yml.mariadb-backup` for the duration of the update, for manual rollback, then removed automatically once the stack has restarted successfully on PostgreSQL. On failure, the migration attempt is cleaned up and the MariaDB installation is left completely untouched.

### Fixed
- On local dev the frontend always showed the demo-mode login dialog regardless of `VITE_APP_DEMO`'s actual value: the build-time flag (a string) was used directly as a boolean instead of compared against `'true'`, so any explicitly set value — including `"false"` — was truthy.

## [v1.3.1] UV-11

### Changed
- The frontend's page components (Mail, Members, Sepa, Contributions, user/link/contribution-plan/email/creditor/general/member-category config, Audit, Backup) are now lazily loaded on demand instead of bundled into the initial JavaScript payload, cutting the main bundle from ~1.4 MB to ~577 kB.
- The icon picker ("Symbol auswählen") now caps the number of rendered icon tiles at 250 and prompts the user to refine their search for more, instead of mounting all ~8600 icons at once — this previously froze the UI for over a second every time the dialog was reopened.
- The icon picker now waits for icons to be fully loaded/cached before revealing the grid, showing a loading spinner in the meantime instead of an empty or partially populated dialog.
- `IconPickerDialog.tsx` no longer redundantly dynamically imports `muiIcons` for `loadIconNames` when it already imports it statically elsewhere, resolving a Vite build warning and allowing the module to be chunked correctly.
- GitHub Actions' `enforce-source-branch` CI job now also runs for pull requests targeting `development`, so merging `main` back into `development` isn't blocked by a status check that previously only evaluated (and could report) PRs targeting `main`.
- Bumped the `brace-expansion` npm override to `^5.0.8` to close a high-severity ReDoS advisory (GHSA, exponential-time expansion of consecutive non-expanding `{}` groups); the previous override version had drifted out of sync with the lockfile and a duplicate vulnerable copy remained nested under `filelist`.

### Fixed
- Vite's chunk-size warning for the `@mui/icons-material` barrel (loaded on demand by the icon picker) is now suppressed via `chunkSizeWarningLimit`, since that chunk is expected to be large and is never part of the initial page load.

## [v1.3.0] UV-4

### Added
- Name search in the Broadcast email "Recipient" tab: recipients can now be filtered by first or last name, in addition to the existing member category filter.

### Changed
- `backend`, `frontend`, and `proxy` now have proper health checks (previously only the database did), and each service waits for its dependencies to be actually ready (`service_healthy`) instead of just started, across all `docker-compose*.yml` files.
- CI now fails immediately with container logs attached when the stack doesn't become healthy in time, instead of silently continuing on to the Playwright tests and failing there with no diagnostics.
- Increased the database health check's retry budget to tolerate occasional slow first-time initialization on CI runners.
- `.env.example` now lists `BACKUP_PATH`, matching every value `docker-compose-ini.yml` generates.

### Fixed
- Rundmail recipient category dropdown showed the raw translation key instead of the category name for custom (non-default) member categories.
- `proxy` container could stay unhealthy indefinitely because its health check resolved `localhost` to IPv6 while `nginx.conf` only listens on IPv4.
- Backend Docker image build occasionally failing due to transient package-mirror errors during `apt-get install`.

## [v1.2.0] UV-3

### Added
- Progressive Web App (PWA) support: the app can be installed to the home screen with a fully offline-capable app shell (service worker with navigation fallback, web manifest, immediate background updates) — the cached UI still loads even without a network connection or while the server is temporarily unreachable.
- Mobile-optimized layout: a hamburger menu with bottom navigation (respecting the safe area on devices with rounded corners/a home indicator) replaces the desktop sidebar on smartphones, tables become cards or a compact single-column layout with wrap-safe pagination, dialogs open full-screen, and filter bars stack vertically on narrow screens.
- Offline indicator on the login screen: shows "You're offline" whenever the browser has no connection or the server can't be reached, clearing automatically once reachability returns.

### Changed
- Refactored the frontend page components: extracted mobile-specific views (Audit log entries, member list) and the sidebar navigation into dedicated, reusable components for better maintainability.
- Extracted the create/edit dialogs (contribution plans, users, member categories, links, import errors) into standalone components and grouped all dialog components under `src/components/dialogs`.
- Extracted a shared `ResponsiveTablePagination` component used by every paginated table (Members, Mail recipients, SEPA, Contributions, Audit).
- Applied consistent Prettier formatting across the frontend codebase.
- The "Update Third-Party Notices" workflow now runs on feature branches instead of `main`, so it can commit its generated notices without hitting the protected `main` branch.
- `docker-compose-ini.yml` no longer requires manually bumping a hardcoded `VERSION` value before merging; it now automatically fetches the latest GitHub release and writes it into `.env` on every init run.

### Fixed
- Language switch button requiring two clicks before the language actually changed on first use.
- Init script (`docker-compose-ini.yml`) leaving duplicate `VERSION` lines in `.env` because `sed -i` failed silently against the single-file bind mount.

## [v1.1.0] UV-2

### Added
- Automatic firmware update check that compares the installed version against the latest GitHub release.
- Email notification to admins when a new firmware version is available.
- Notification endpoint and a notification bell in the frontend to surface available firmware updates.

### Changed
- Renamed the backend `Data` folder to `Models` for clearer project structure.

### Fixed
- Corrected the user mail used for sending notifications.
- Fixed new members occasionally being assigned a member number that was already in use by an existing member. [double member numbers](https://github.com/uni-verein/uni-verein/issues/4)
