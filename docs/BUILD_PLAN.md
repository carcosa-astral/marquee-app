# Marquee build plan

This is the hand-off for building Marquee. The screens are already mocked in `docs/design/`. Build to match them.

## 1. Goal and principles

A private diary for movies, TV and anime, with rich metadata, tracking, and yearly and all-time recaps. No social features.

- Self-contained and portable. One Marquee folder holds everything.
- Local-first. One SQLite file. Works offline after metadata is saved.
- Starts empty. Nothing is preloaded, including theaters, people and tags.
- User edits always win over fetched metadata.
- It may be shared with others later, so nothing about the owner's own data may be hard-coded.

## 2. Tech choices

| Area | Choice | Why |
| --- | --- | --- |
| Language | C# | Owner already uses it for another app. |
| Runtime | Current .NET LTS | Long support, good tooling. |
| UI | Avalonia (latest stable) | Real desktop app on Windows, Mac and Linux, fully themeable. |
| Storage | SQLite | One file, easy to back up. |
| Data access | EF Core with the SQLite provider | Migrations for schema changes over time. |
| Metadata | TMDB (movies, TV) and AniList (anime) | TMDB is weak on OVAs, AniList is strong. |
| Packaging | Self-contained, single-file publish | No install, no .NET needed on the machine. |

Use MVVM with CommunityToolkit.Mvvm. Keep logic out of views.

## 3. Solution layout

```
Marquee.sln
src/
  Marquee.Core/        domain types and rules, no UI, no database
  Marquee.Data/        EF Core, migrations, repositories, JSON export and import
  Marquee.Metadata/    TMDB and AniList clients, matching and merging
  Marquee.App/         Avalonia UI, view models, theme
tests/
  Marquee.Core.Tests/
  Marquee.Data.Tests/
  Marquee.Metadata.Tests/
docs/
```

## 4. Portable folder layout

The app finds its data next to the executable. It must never write to AppData, the registry, or any system folder.

```
Marquee/
  Marquee(.exe)         the app
  data/
    marquee.db          the diary (SQLite)
    images/             saved posters and photos
  backups/              automatic and manual backups
  exports/              JSON exports
```

Resolve the root from `AppContext.BaseDirectory`. On macOS, an `.app` bundle makes the executable path awkward, so choose the folder next to the bundle and test this early. Settings, including the TMDB key, live in the database or in `data/`, never outside the folder.

## 5. Data model

Use string GUIDs as primary keys so exports can be merged cleanly.

- **Title**: id, kind (`movie`, `tv`, `anime`), format (`movie`, `series`, `ova`, `ona`, `special`, `film`), name, original name, year, runtime, overview, release date, status, poster and backdrop paths, external ids (`tmdb_type`, `tmdb_id`, `anilist_id`, `mal_id`, `imdb_id`). TMDB ids are only unique together with `tmdb_type` (`movie` or `tv`).
- **Season** and **Episode**: belong to a Title. Anime series may have a flat episode list with one season.
- **Person**: name, birth and death dates, bio, photo path, external ids.
- **Credit**: Title, Person, role (`director`, `actor`, `voice`), character name, order.
- **Genre** and **TitleGenre**.
- **Watch**: one diary entry. Title, optional Episode, watched on (date), optional start time, rating (0.5 to 5), is rewatch, where kind (`theater`, `home`, `streaming`, `other`), optional Theater, screen, seat, format, service, note.
- **WatchCompanion**: links a Watch to a Companion.
- **Companion**: a person the user watches with. Created by the user.
- **Theater**: name, city, chain, formats offered. Created by the user. Never preloaded.
- **WatchlistItem**: Title, added on, optional expected release date.
- **IceItem**: Title, state (`paused` or `dropped`), stopped at (season and episode), reason, note, iced on.
- **FieldOverride**: Title, field name, user value. Overrides always win.
- **FieldSource**: Title, field name, source (`tmdb`, `anilist`, `user`). Drives the source tags in the UI.
- **Setting**: key and value.

## 6. Rules

- **Watched** means at least one Watch exists. Rewatches create new Watch rows and count separately.
- **Watching** means a TV or anime Title has some episode Watches, is not finished, and is not on ice.
- **On Ice** and Watching are mutually exclusive. Resuming moves a title back to Watching and keeps the history.
- **Watchlist, coming soon** means the release date is in the future. When the date arrives it appears under "Out now" with no user action.
- **Person pages** count only Titles where the person has an `actor`, `voice` or `director` credit on a movie, TV or anime Title. Ignore talk shows, cameos listed as "Self" and archive footage.
- **Completion bar** on a person page shows watched, on watchlist and not seen across that person's counted Titles.
- **Metadata precedence:**
  1. Movies and TV: TMDB first.
  2. Anime: AniList first, TMDB fills what is missing (for example cast photos and backdrops).
  3. A FieldOverride beats both.
- **Matching across sources:** link by external ids first (AniList carries the MAL id, TMDB has external ids). If there is no id match, fall back to title, year and format. The user can always fix a wrong match, and entries, ratings and watches stay attached to the Title.
- **Refreshing metadata** never touches Watches, overrides or user-created data.
- **Recap:** one view with Yearly (with back and forward) and All time. Same layout for both. Measures: hours, films, episodes, average rating, theater visits, activity chart, top genres, decades, rating distribution, top directors, top actors, top theaters, highest rated, firsts and lasts.

## 7. Marquee JSON format (version 1)

A single readable file. The `version` number lets old exports keep importing as the format grows.

```json
{
  "format": "marquee",
  "version": 1,
  "exportedAt": "2026-10-24T20:00:00Z",
  "app": { "version": "0.1.0" },
  "titles": [],
  "people": [],
  "watches": [],
  "watchlist": [],
  "onIce": [],
  "theaters": [],
  "companions": [],
  "overrides": []
}
```

- Use the same GUIDs as the database so imports can match records.
- Export lets the user choose what to include. Poster images are optional because they add size.
- Import shows a preview (counts and conflicts) and changes nothing until confirmed.
- Import modes: **Add** (keep what exists, skip duplicates) and **Replace** (make a backup first).
- Always check `format` and `version` first. Refuse unknown formats with a clear message. Migrate older versions on import.

## 8. Screens

The mocks live in `docs/design/mocks/`. Each file is one screen.

| Mock | Screen |
| --- | --- |
| `Setup` | First run: confirm the folder, enter the TMDB key, start empty or import |
| `Main` | Home: continue watching, in progress, year strip, recent, watchlist and On Ice previews |
| `Search` | Search and add a title, with merged results across sources |
| `Movie` | Movie page |
| `Show` | Anime series page with episode tracker and source tags |
| `TvShow` | TV page with seasons and episodes |
| `Person` | Director or actor page with completion bar |
| `Diary` | Entries by month with filters |
| `Library` | Movies, TV and Anime grids with filters |
| `Watchlist` | Coming soon and out now |
| `OnIce` | Paused and dropped titles |
| `PutOnIce` | Dialog: paused or dropped, where you stopped, reason |
| `LogWatch` | Dialog: date, rating, rewatch, where, theater details, with, note |
| `Theaters` | The user's theaters with visits and details |
| `EditMeta` | Edit fields, see sources, fix a wrong match |
| `Recap` | Yearly and All time |
| `Settings` | Folder, backups, metadata, appearance, export and import |

`Sidebar` and `Topbar` are shared pieces. The "People" sidebar entry still needs a page: a simple index of people, most-watched first.

## 9. Design

Nord theme, dark first. A light Nord theme is a later option.

- Polar Night: `#2E3440`, `#3B4252`, `#434C5E`, `#4C566A`
- Snow Storm: `#D8DEE9`, `#E5E9F0`, `#ECEFF4`
- Frost: `#8FBCBB`, `#88C0D0`, `#81A1C1`, `#5E81AC`
- Aurora: red `#BF616A`, orange `#D08770`, yellow `#EBCB8B`, green `#A3BE8C`, purple `#B48EAD`

The mocks also use `#272C36` for the sidebar and `#343A47` for cards.

Type colors: Movie uses `#81A1C1`, TV uses `#B48EAD`, Anime uses `#EBCB8B`. Status colors: watched `#A3BE8C`, watchlist `#EBCB8B`, paused `#81A1C1`, dropped `#D08770`.

Fonts in the mocks are Bricolage Grotesque (headings) and Hanken Grotesk (body). Both are open fonts. Bundle them with the app so it stays self-contained, with system fallbacks.

Define the palette once as theme resources and reference them from every view. Do not scatter hex values in views.

## 10. Attribution

The About screen and README must show: "This product uses the TMDB API but is not endorsed or certified by TMDB." Credit AniList too. Check both services' current terms before release.

## 11. Milestones

Each milestone ends with a runnable app and an updated changelog.

1. **0.1.0, skeleton.** Solution, Avalonia shell with the Nord theme and sidebar, portable folder resolution, SQLite with the first migration, first-run screen.
2. **0.2.0, metadata and add.** TMDB and AniList clients, search, merged results, saving a Title with sources, images saved to `data/images/`.
3. **0.3.0, logging.** Log a watch dialog, diary page, theaters (add, edit, list), companions.
4. **0.4.0, title pages.** Movie, anime series and TV pages, episode and season progress, edit metadata and fix match.
5. **0.5.0, watchlist and On Ice.** Both pages, the put on ice dialog, auto move from coming soon to out now.
6. **0.6.0, people.** Actor and director pages, completion bar, add unseen to watchlist, People index.
7. **0.7.0, recap.** Yearly and All time.
8. **0.8.0, data safety.** Settings, backups, JSON export and import with preview.
9. **0.9.0, polish.** Library filters, keyboard shortcuts, empty states, performance on large libraries.
10. **1.0.0.** Packaged, portable builds, attribution, a README with screenshots.

## 12. Testing

- Unit tests for the rules in section 6, especially watched, watching, ice, coming soon, person counts and metadata precedence.
- Round-trip tests for JSON export and import, including old versions.
- Tests that a metadata refresh never changes Watches or overrides.
- A test that nothing is written outside the Marquee folder.

## 13. Open questions for the owner

- Which platforms to publish first (Windows only, or also Mac and Linux).
- Whether a light theme matters for 1.0.
- Whether to add an optional import from Letterboxd or Trakt exports later.
