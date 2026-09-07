# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed UV-20
- Cleared out every remaining frontend ESLint/TypeScript warning: `any`-typed props, state, and API helpers (`api`/`apiFile`, `AuditLog`, `Contribution`/`ContributionInfo`, SignalR payloads, TipTap's `toggleUnderline`, the iOS `navigator.standalone` check, …) were replaced with concrete types; data-loading functions used inside `useEffect` are now wrapped in `useCallback` and listed in their effect's dependency array instead of being omitted, and the `useCallback`-plus-`@ts-expect-error`-wrapped `lodash.debounce` calls (Members, Receipts, Sepa, Contributions, RecipientList) were replaced with properly typed `useMemo`-based debouncing so no dependency is silently dropped.
- Every remaining unavoidable `setState` call inside an effect body (token/session checks, responsive layout resets, debounced search effects, etc.) is now explicitly annotated with `// eslint-disable-next-line react-hooks/set-state-in-effect` instead of triggering a warning.
- `PageConfigContext`, `SnackbarContext`, and `ThemeModeContext` each had their hook (`usePageConfig`/`useSnackbar`/`useThemeMode`) and context object moved out into a dedicated `hooks/use*.ts` file, so the `*Context.tsx` files only export the provider component — this satisfies `react-refresh/only-export-components`, which flagged them for exporting non-component values alongside a component. `MuiIcons.tsx`'s `loadAllIcons` helper was split out into `utils/iconLoader.ts` for the same reason.
- The dashboard now reads the logged-in user's name/role/id from the JWT synchronously via a lazy `useState` initializer instead of after mount via an effect, avoiding an initial render with an empty user before the token is parsed.

### Fixed
- Audit log table and card rows were all keyed on `l.id`, a field `AuditLog` entries don't have (it only surfaced once the `any` typing was removed); they're now keyed on `` `${l.timestamp}-${index}` ``, giving React stable, non-colliding keys instead of every row sharing the same `undefined` key.

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
