# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Idle TTL for sticky SQL sessions (`Connector:SqlSessionIdleTimeoutSeconds`, default 900). Idle sessions are closed automatically on the next DB request sweep.
- Best-effort `ROLLBACK` when closing a SQL session (`IF @@TRANCOUNT > 0`) so dropped Hub sessions do not leave open FAKIR/MAG transactions.
- Unit tests for SQL session idle tracking (`SqlSessionIdleTracker`).

## [1.0.0] - 2026-06-18

### Added

- Windows Service (`Connector.Service`) with duplex gRPC client to KSeF Hub.
- WPF configurator for SQL/gRPC settings, connection tests, and service restart.
- SQL executor with transactional session support for WAPRO MAG/FAKIR.
- DPAPI encryption for SQL password and connector access token.
- Self-contained Windows installer (`KSeFHubConnectorSetup.exe`).
- Unit tests for connection string builder and SQL command validation.
- GitHub Actions workflow for build, test, and release artifacts.

### Fixed

- gRPC reconnect hang when the server closed the stream without an exception.
- `NON_QUERY` commands now use `ExecuteNonQuery` instead of a data reader.

### Security

- Access token is no longer stored in plain text in `connector.settings.json`.
- Legacy plain-text tokens are migrated automatically on load and removed on save.

[1.0.0]: https://github.com/OWNER/ksef-hub-connector/releases/tag/v1.0.0
