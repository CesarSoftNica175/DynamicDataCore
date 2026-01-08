# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/)
and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.0.2] - 2026-01-07

### Added
- Support for dynamic primary key types (`int`, `Guid`, `string`, etc.) across repositories and services.
- Unified identifier handling using EF Core native `FindAsync(object[])`.

### Changed
- `RetrieveByIdAsync` now accepts `object id` instead of `int`.
- `DeleteAsync` now accepts `object id` instead of `int`.
- Updated repository and service interfaces to be fully key-agnostic.

### Fixed
- Removed implicit coupling to integer-based primary keys.
- Improved long-term scalability for heterogeneous domain models.

---

## [1.0.1]
- Initial GitHub Package release.
