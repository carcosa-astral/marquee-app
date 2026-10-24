# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Log a watch dialog: date (with Today and Yesterday), optional start time, half-star rating, rewatch, where you watched, theater details (screen, seat, format), service, who you watched with, and a note.
- For TV and anime, the dialog picks a season and episode and suggests the next unwatched one.
- Rewatch switches on by itself when you have seen the title or episode before, and shows when you first saw it.
- Diary page: entries by month for one year at a time, with Movies, TV and Anime filters plus Theater only, Rewatches and Has a note. Click an entry to edit or delete it.
- Theaters page: your theaters with visits, last visit and average rating, a detail panel with recent visits, and add and edit.
- Theaters and companions can be created on the spot while logging. Nothing is preloaded.
- Log a watch works from the top bar, from search results and from library posters.

### Changed

- Adding a title now always means logging a watch. Search results have "Log a watch" instead of "Add to library", and a title picked from search is saved (with its images) only when you save the watch. Cancelling leaves nothing behind.
- Movies, TV and Anime pages and their sidebar counts show only titles you have watched.
- "Where you watched" is now Theater, Home or Other. Streaming services go in the service field, and existing Streaming entries become Home with their service kept.

## [0.2.0] - 2026-10-24

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

[Unreleased]: https://github.com/carcosa-astral/marquee-app/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/carcosa-astral/marquee-app/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/carcosa-astral/marquee-app/releases/tag/v0.1.0
