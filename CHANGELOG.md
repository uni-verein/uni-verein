# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
