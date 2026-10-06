# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - Unreleased

First version.

### Added

- Worker that reads error lines from Grafana Loki, groups them by service, exception type, HTTP method, route and status code, and opens one Jira Cloud issue per group.
- Duplicate protection through a fingerprint label on the issue; no database.
- Dry-run mode, on by default.
- Issue policy: minimum status code, minimum occurrences, start time, and a cap on issues per run.
- Optional request data in issues, with masking of sensitive fields and withholding of requests that look like they contain payment details.
- Configurable field names, nested JSON and Loki structured metadata as field sources.
- Read-only API (`GET /api/groups`) behind a shared key.
- Demo: sample API with a reference logging middleware, a Jira stand-in and a Docker Compose file.
