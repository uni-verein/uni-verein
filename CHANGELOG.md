# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
