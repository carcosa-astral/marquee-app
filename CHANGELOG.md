# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- README describing what Marquee is and its principles.
- Build plan covering the data model, rules, JSON export format, portable folder layout and milestones.
- Design mocks for every screen (Nord theme) and a design guide.
- CLAUDE.md with working conventions for this repo.
- .NET 10 solution with Core, Data, Metadata and App projects, plus Core and Data test projects.
- Avalonia desktop shell with the Nord theme: sidebar, top bar and a placeholder page for every section.
- Nord palette defined once as theme tokens, with Bricolage Grotesque and Hanken Grotesk bundled (OFL).
- Portable folder resolution: data, images, backups and exports live next to the executable, including next to a macOS app bundle.
- SQLite database with the full data model in the first EF Core migration. Nothing is preloaded.
- First-run setup screen: shows the Marquee folder, saves the optional TMDB key and starts an empty diary.
- Settings page showing folder locations, the version and TMDB and AniList attribution.
- Tests for folder resolution, migrations, settings and that nothing is written outside the Marquee folder.
