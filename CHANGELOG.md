# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Watchlist page: "Coming soon" with release dates and a countdown, and "Out now, not yet seen" as a poster grid with Movies, TV, Anime and Under 2 hours filters. Titles move from coming soon to out now on their release day by themselves.
- "Pick something for me" opens a random title from Out now.
- Add to watchlist from search results and title pages. Titles you add are saved (with images) but stay out of your library until you watch them.
- On Ice page for paused and dropped shows, with where you stopped, how far you got, your reason and when you iced it. Resume, or move between paused and dropped.
- Put on ice dialog: paused or dropped, where you stopped (filled in from your diary), date, a quick reason or your own, and a note.
- Title pages show Paused or Dropped for shows on ice, with a Resume button.

### Changed

- Watching something takes it off your watchlist, and logging an episode of a show on ice takes it off ice.

## [0.4.0] - 2026-10-24

### Added

- Title pages for movies, TV and anime: backdrop, poster, details, overview, who directed it, cast with how many of their titles you have watched, and a Details card showing where each field came from.
- Movie pages list every watch (first watch and rewatches) and where you saw it; click one to edit it.
- TV and anime pages show progress for the whole show, with season tabs, an episode grid for long anime seasons and an episode list with check marks. Click an episode to mark it watched today, or a watched one to open its entry.
- "Mark through episode" for single-season shows and "Mark season watched" for TV seasons.
- Average rating per season on TV pages.
- Edit details: change the title, original title, year, runtime, release date, status and overview. Your values win over TMDB and AniList, even after a refresh, and each field can be reset to the source's value without going online.
- Fix match: pick a different TMDB or AniList entry. Diary entries, ratings and edits stay attached.
- Refresh details fetches the latest data again without touching watches or edits.
- Library posters open the title page, and search results already in your diary have an Open link.
- IMDb ratings: Settings downloads IMDb's free ratings file (about 8 MB) into `data/imdb/`. Titles logged later get their rating from the saved file, offline.
- Title pages show a row of scores: IMDb first, then TMDB's user score, then AniList's average score (for anime). TMDB and AniList scores are saved when a title is added and updated by Refresh details.

### Changed

- TV shows and anime series are rated as a whole, not per episode. Set the rating in the log dialog or with the stars on the show's page. Existing episode ratings become one show rating (their average).
- Logging a TV or anime episode no longer asks where you watched; episodes count as Home, and the service field stays.
- The start time hint only mentions 12-hour times.

## [0.3.1] - 2026-10-24

### Fixed

- The date picker's calendar now uses the Nord colors and fonts instead of Fluent's black defaults, with days from other months dimmed.
- The date text and calendar icon in the date box have proper spacing from the edges.
- Text color now comes from the window instead of a style that forced one color on every piece of text, so built-in controls can show their own states (such as dimmed days).

## [0.3.0] - 2026-10-24

### Added

- Log a watch dialog: date (with Today and Yesterday), optional start time, half-star rating, rewatch, where you watched, theater details (screen, seat, format), service, who you watched with, and a note.
- For TV and anime, the dialog picks a season and episode and suggests the next unwatched one.
- Rewatch switches on by itself when you have seen the title or episode before, and shows when you first saw it.
- Diary page: entries by month for one year at a time, with Movies, TV and Anime filters plus Theater only, Rewatches and Has a note. Click an entry to edit or delete it.
- Theaters page: your theaters with visits, last visit and average rating, a detail panel with recent visits, and add and edit.
- Theaters and companions can be created on the spot while logging. Nothing is preloaded.
- Log a watch works from the top bar, from search results and from library posters.
- "Erase all data" in Settings starts Marquee over. You type ERASE to confirm, a backup copy goes to `backups/` first (on by default), and you can also erase settings and the TMDB key to see the setup screen again.

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

[Unreleased]: https://github.com/carcosa-astral/marquee-app/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/carcosa-astral/marquee-app/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/carcosa-astral/marquee-app/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/carcosa-astral/marquee-app/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/carcosa-astral/marquee-app/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/carcosa-astral/marquee-app/releases/tag/v0.1.0
