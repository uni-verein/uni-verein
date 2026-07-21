# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
