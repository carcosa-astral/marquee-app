# Working in this repo

Marquee is a personal, local-first movie, TV and anime diary. Read `README.md` and `docs/BUILD_PLAN.md` first.

## How to work with the owner

- The owner is not a programmer and relies on you to handle the code. Make sensible decisions, explain what you did in short, clear sentences, and avoid walls of text.
- Keep explanations concise and easy to scan. The owner has ADHD, so lead with the outcome, then give a few bullets if needed.
- Never use em dashes in any text you write (code comments, docs, commit messages, replies).
- Never suggest or recommend Hetzner.
- Ask before big or hard-to-undo decisions. Otherwise make the call and say what you chose.

## Git rules

- Before the first commit in a session, set the identity:
  - `git config user.name "Carcosa Astral"`
  - `git config user.email "337445296+carcosa-astral@users.noreply.github.com"`
- Before every push, run `git log -n 5 --format='%an <%ae>'` and confirm every new commit uses that name and email. Fix it before pushing if not.
- Never force push or rewrite history without asking first.
- Never move, delete or reuse a tag without asking first.
- Work on a branch and open a pull request instead of committing straight to `main`.

## Versioning and changelog

- Semantic versioning (MAJOR.MINOR.PATCH). The first release is 0.1.0.
- Keep `CHANGELOG.md` in Keep a Changelog format. Update the `Unreleased` section as you make changes.
- To release: move the Unreleased entries under the new version and date, commit, then create an annotated tag `vX.Y.Z` on that commit and push the tag.
- When you choose a version bump, say which one and why in one sentence.

## Product rules to keep

- Self-contained and portable: the app finds its data folder next to the executable. Never write to AppData, the registry or other system folders.
- No preloaded theaters, people, tags or lists. The app starts empty.
- No accounts, no telemetry, no social features.
- User edits to metadata always win over TMDB and AniList.
- Person pages count only titles where the person acted, voiced or directed.

## Design

The Nord design lives in `docs/design/`. Match it. Use the Nord palette as theme tokens, not hard-coded colors scattered in views.
