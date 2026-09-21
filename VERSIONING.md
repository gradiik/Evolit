# Evolit versioning protocol

This file is mandatory reading for any coding agent before modifying the repository.

## Source of truth

1. Read `versions.json`.
2. Read this file.
3. Inspect the current `main` HEAD before editing.
4. `main` is the active development branch.
5. A completed version is identified by an immutable Git tag `vX.Y.Z`. The tag/commit is the authoritative historical checkpoint.
6. `version/X.Y.Z` is a convenience branch pointing at that checkpoint. It may be repaired to match the tag if it drifted, but historical code must never be rewritten.
7. Never increment the application version unless the user explicitly requests a new version.
8. Never silently switch the requested version or continue work on a different version.

## Development lifecycle

Normal development happens on `main` with as few commits/pushes as practical. Do not run GitHub Actions for ordinary development, intermediate checks, experiments, or each commit.

When the user says a version is ready:
1. Finish development on `main`.
2. Run available checks that do not consume GitHub Actions.
3. Perform the final audit and report findings to the user.
4. Wait for explicit release approval.
5. Fix approved findings and recheck changed areas.
6. Freeze the version: create/update `version/X.Y.Z`, create immutable tag `vX.Y.Z`, and update `versions.json` with the exact commit.
7. Only after explicit release approval, run the minimum necessary final GitHub Actions build/check if required.
8. Produce release assets and create the matching GitHub Release. Stable versions are Releases; alpha/beta/rc/preview versions are Pre-releases.

Every version normally has a prepared ZIP release asset. Automatic GitHub source archives do not replace it.

## In-app version screen

The in-app Versions screen is informational/selection UI. Selecting an old version inside the running game does not perform a Git checkout and must not pretend to replace the executable. A future launcher may use release builds to actually launch installed historical versions.

## Agent safety

Before changing code, compare the requested version with `versions.json` and the repository state. If they conflict, do not guess: preserve the existing checkpoint and report the mismatch. Never invent commits, tags, branches, releases, artifacts, or version history.

Current project-specific rule: development is now 0.0.5. Do not advance to 0.0.6 or another version without explicit user instruction.
