# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- "Add a title" search page that searches TMDB and AniList at the same time, with Movies, TV and Anime filters.
- TMDB client (accepts the API key or the read access token) and AniList client (no key needed).
- Results that are the same title on both sources are matched by MAL id, or by title, year and format, and shown as one merged result.
- Adding a title saves its details, genres, directors, cast and voice actors, seasons and episodes, and which source each field came from.
- Anime uses AniList first, with TMDB filling gaps such as backdrops and cast photos. For the poster, TMDB's art is preferred when TMDB has one.
- Posters, backdrops and person photos are saved to `data/images/`.
- Movies, TV and Anime library pages show a poster grid.
- The TMDB key can be added or changed in Settings.
- Ctrl K jumps to the search box.

### Changed

- Titles now store the TMDB type (movie or TV), because TMDB reuses the same id numbers for both. Applied by a new database migration.

## [0.1.0] - 2026-10-24

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

[Unreleased]: https://github.com/carcosa-astral/marquee-app/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/carcosa-astral/marquee-app/releases/tag/v0.1.0
