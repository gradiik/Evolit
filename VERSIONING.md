# Evolit versioning protocol

This file and `versions.json` are the first sources of truth for version-related work in Evolit.

## Rules

1. Read `VERSIONING.md` and `versions.json` before changing the project.
2. Verify the current branch, `HEAD`, and the actual Git objects before editing.
3. `main` is the development branch.
4. A development commit is not automatically a new application version.
5. Do not change the application version without an explicit user decision.
6. A released or frozen version must be tied to an immutable Git tag and commit.
7. Never rewrite Git history, force-push, or modify a frozen tag.
8. Do not make the running application rewrite the working Git checkout.
9. GitHub Actions, Releases, tags, and release assets require explicit authorization.

## Version records

Each record in `versions.json` should contain only facts that can be verified from the repository:

- version number;
- status;
- date, when confirmed;
- short description;
- commit or tag, when confirmed;
- whether it is the current application version.

The application may display the latest five approved checkpoints. Git remains the full history; do not copy complete source trees into a `versions/` directory.

## Rollback

The in-app Versions screen may display a checkpoint and save a rollback request. It must not run `git checkout`, delete files, or rewrite the repository. The actual checkout is performed explicitly by Codex or Git.

## Adding a future version

When the user explicitly approves the next checkpoint:

1. update the centralized application version source;
2. add the new verified record to `versions.json`;
3. verify the final commit;
4. create a version branch/tag only when explicitly requested;
5. update the registry with the immutable commit/tag after it exists.

Do not advance to `0.0.5` automatically.
