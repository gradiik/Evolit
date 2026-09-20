# Codex task — install latest Evolit locally on Windows

Work with the existing repository `gradiik/Evolit` on this Windows PC.

The goal is NOT to change gameplay. The goal is to get the newest `main` onto this computer as a runnable local Windows test build and create a working desktop shortcut.

## Rules

- Do not run GitHub Actions.
- Do not create a Release, tag, ZIP, or version bump.
- Do not commit generated local build files.
- Do not alter gameplay or UI unless a build/runtime blocker is found.
- Use the repository's existing `Windows Desktop` export preset and branding.
- The application icon is `assets/branding/evolit_icon.svg`.
- Local build output under `.local-build/` is intentionally gitignored.

## Steps

1. Locate the local checkout of `gradiik/Evolit`. If it does not exist, clone it from GitHub.
2. Make sure the checkout is on `main`.
3. Fetch and fast-forward/pull to the latest remote `main`. Do not overwrite uncommitted user work. If local changes exist, inspect them first and preserve them.
4. Verify the current commit SHA and report it.
5. Ensure .NET SDK 8 is installed.
6. Ensure Godot 4.4.1 .NET/Mono is installed, including matching Windows export templates.
7. Run local checks only:
   - `dotnet restore Evolit.csproj`
   - `dotnet build Evolit.csproj --configuration Release --no-restore`
   - Godot headless project import/startup if practical.
8. Run:
   `powershell -ExecutionPolicy Bypass -File .\tools\install_local_windows.ps1 -GodotPath "<resolved Godot 4.4.1 .NET executable if needed>"`
9. The script must export the newest project and install it to:
   `%LOCALAPPDATA%\Programs\Evolit`
10. Verify that `%LOCALAPPDATA%\Programs\Evolit\Evolit.exe` exists and starts.
11. Verify there is a working desktop shortcut:
   `Evolit.lnk`
   pointing to that installed executable.
12. Verify the shortcut launches the installed build.
13. Confirm the application uses the Evolit icon rather than the default Godot icon. If Windows file-icon embedding needs `rcedit`, configure/install it locally and re-export. Do not modify unrelated project code.
14. Launch Evolit and leave it open for me to test.

If any prerequisite is missing, install only what is required locally. Prefer official Godot/.NET sources. Do not use GitHub Actions as a substitute for local setup.

At the end report only:
- checked-out commit SHA;
- build result;
- install path;
- shortcut path;
- whether the shortcut launches successfully;
- whether the Evolit icon is visible;
- any blocker that remains.
