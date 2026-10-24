# Marquee

A private, local-first diary for movies, TV and anime. Think Trakt and Letterboxd without the social side: rich metadata, a clean Nord-themed desktop app, tracking, and yearly and all-time recaps.

> **Status:** early build. Milestone 0.1.0 (the skeleton) runs: the Nord shell, the first-run screen and an empty diary. Most pages are placeholders for now.

## What it does

- **Diary:** every watch is its own entry, so rewatches count. Each entry can hold the date, a rating, where you watched (theater, home, streaming), the theater, screen, seat and format, who you watched with, and a note.
- **Movies, TV and anime:** anime is its own section and includes series, films, OVAs, ONAs and specials.
- **Progress:** episode tracking for TV and anime.
- **Watchlist:** titles coming soon (they move to "out now" on their own) and titles that are out but not yet seen.
- **On Ice:** things you paused or dropped, with where you stopped and why.
- **People pages:** actors and directors, with a "watched 5 of 12" completion bar.
- **Theaters:** your own list, with visits, formats and average rating per theater.
- **Recap:** one screen with Yearly (go back in time) and All time.
- **Export and import:** a single readable Marquee JSON file.

## Principles

- **Self-contained and portable.** Everything lives in the Marquee folder: the app, the diary file, saved images, backups and exports. No installer, no hidden system folders. Copy the folder and you have copied everything.
- **Local-first.** One SQLite file you own. Works offline once a title's details are saved.
- **No accounts, no tracking, no social features.**
- **Starts empty.** No preloaded theaters, people or lists. Anyone with their own copy builds their own.
- **Your edits win.** Metadata comes from TMDB and AniList, and anything you change by hand is kept, even after a refresh.

## Tech

C#, .NET and Avalonia for the desktop UI, SQLite for storage. Movie and TV metadata from TMDB, anime metadata from AniList with TMDB filling gaps. See [docs/BUILD_PLAN.md](docs/BUILD_PLAN.md).

## Build and run

Needs the .NET 10 SDK.

```
dotnet run --project src/Marquee.App
dotnet test
```

The app creates `data/`, `backups/` and `exports/` next to the executable. When run from source that is `src/Marquee.App/bin/Debug/net10.0/`.

## Docs

- [Build plan](docs/BUILD_PLAN.md): data model, rules, JSON format, folder layout and milestones.
- [Design mocks](docs/design/README.md): the Nord design and every screen.

## License

MIT. See [LICENSE](LICENSE).

## Credits

This product uses the TMDB API but is not endorsed or certified by TMDB. Anime data from AniList.
