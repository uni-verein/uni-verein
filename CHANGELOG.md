# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
