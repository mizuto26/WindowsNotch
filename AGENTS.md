# AGENTS.md

## Project Overview

WindowsNotch is a WPF desktop app that recreates a mac-like notch UI on Windows.

Main user-facing features:
- notch-style overlay at the top of the screen
- file shelf for temporary storage
- iCloud Drive drop zone for sharing files to iPhone
- now playing / media session view
- settings and share guide windows

## Tech Stack

- .NET 8
- WPF
- Windows-specific interop via `user32.dll` / `dwmapi.dll`

Do not migrate this project to WinUI unless explicitly requested.

## Repository Layout

- `src/WindowsNotch.App/App.xaml`
  Application resources and shared styles
- `src/WindowsNotch.App/Views/MainWindow.xaml`
  Main notch UI
- `src/WindowsNotch.App/Views/MainWindow.xaml.cs`
  Main window state and shared fields
- `src/WindowsNotch.App/Views/MainWindow.Animation.cs`
  notch open / close animation logic
- `src/WindowsNotch.App/Views/MainWindow.DragAndShelf.cs`
  drag and drop, shelf behavior
- `src/WindowsNotch.App/Views/MainWindow.Media.cs`
  media session UI and playback progress
- `src/WindowsNotch.App/Views/MainWindow.SettingsOverlay.cs`
  overlay visibility and top/hidden behavior
- `src/WindowsNotch.App/Views/MainWindow.Interop.cs`
  native Windows interop helpers
- `src/WindowsNotch.App/Services`
  app services such as settings, startup registration, iCloud location, shelf storage
- `scripts/package-release.ps1`
  shared packaging script for release zip creation
- `.github/workflows/release.yml`
  GitHub Release automation

## Important Commands

Use the local .NET install:

- build
  `C:\Users\user\.dotnet\dotnet.exe build .\WindowsNotch.sln`
- run
  `C:\Users\user\.dotnet\dotnet.exe run --project .\src\WindowsNotch.App\WindowsNotch.App.csproj`
- package zip
  run the VS Code task `package WindowsNotch zip`
  or:
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -DotnetCommand C:\Users\user\.dotnet\dotnet.exe`

## Working Rules

- Prefer targeted edits over broad refactors.
- Preserve existing behavior unless the user explicitly asks for behavior changes.
- Do not remove or rewrite notch animation / overlay logic casually. Small changes there can cause visual regressions.
- Avoid adding unnecessary abstractions, helper layers, or new projects.
- Keep XAML readable and compact.
- Prefer keeping this as a WPF app.
- Do not commit generated files from `bin/`, `obj/`, or temporary logs.

## UI / UX Rules

- Keep the notch visually minimal, black, and mac-inspired.
- Preserve smoothness of open / close transitions.
- Be careful with:
  - hover detection
  - hide / overlay mode
  - top position changes
  - mode switching between Files and Music
- Music and Files layouts should feel visually related.
- Avoid adding heavy borders, bright backgrounds, or Windows-default looking UI unless requested.

## Drag / Shelf Rules

- Shelf items should remain draggable to external folders.
- iCloud drop and shelf drop are separate behaviors and should stay distinct.
- Do not reintroduce custom drag-preview code unless explicitly needed.

## Release Rules

- Release packaging should create `WindowsNotch-win-x64.zip` at the repository root.
- Reuse `scripts/package-release.ps1` for packaging logic instead of duplicating publish commands.
- GitHub Release automation is expected to attach the generated zip when a Release is published.

## Branch Rules

- Use short-lived working branches. Do not work directly on `main`.
- Preferred branch format:
  - `type/issue-number-short-summary`
- Examples:
  - `feat/123-music-tab`
  - `fix/456-not-hiding-correctly`
  - `refactor/789-cleanup-overlay-logic`
- Use one main purpose per branch.
- Prefer branch types that match the commit / PR title type.
- Use the VS Code task `create WindowsNotch branch` or `scripts/new-branch.ps1` to generate branch names consistently.
- `dependabot/*` branches are allowed as an exception for automation.

## Commit Rules

- Use conventional-commit style subjects.
- Preferred types:
  - `feat`
  - `fix`
  - `refactor`
  - `docs`
  - `style`
  - `test`
  - `build`
  - `ci`
  - `chore`
  - `perf`
- Subject format:
  - `type: short summary`
  - `type(scope): short summary`
- Keep the first line short and action-oriented.
- Use lowercase type prefixes.
- Good examples:
  - `fix: prevent notch from hiding on desktop click`
  - `feat(media): add progress slider drag seek`
  - `refactor: remove unused drag preview code`
- After the subject, prefer a short bullet list in the body when multiple changes are included.
- Include `Generated with Codex` in the commit body when Codex materially helped produce the change.
- Do not mix unrelated changes into one commit.

## Pull Request Rules

- PR title should follow the same conventional format as commits.
- PR body should stay concise and easy to scan.
- Include the related issue in the PR body with `Closes #<issue-number>` when working from an issue.
- Prefer this structure:
  - `Related`
  - `Summary`
  - `Testing`
  - `UI Notes`
- In `Summary`, explain the user-visible change first.
- In `Testing`, explicitly say what was run, or say `Not run` if nothing was run.
- In `UI Notes`, mention animation, hover, overlay, drag-and-drop, or media behavior when affected.
- Include `Generated with Codex` in the PR body attribution section.
- Keep PRs focused on one main purpose.
- If a change affects notch animation, overlay mode, or drag behavior, call that out explicitly in the PR body.
- PR labels are automatically derived from the conventional type when possible.
- PR assignees and issue linkage are synchronized automatically from the PR author and branch issue number when possible.
- Keep branch name, PR title, and main commit type aligned.

## Issue Rules

- Prefer using the GitHub issue templates instead of blank issues.
- Use conventional prefixes in issue titles when practical:
  - `feat:`
  - `fix:`
  - `docs:`
  - `ci:`
  - `chore:`
- Issue creator should be the default assignee when possible.
- Issue labels should be synchronized automatically:
  - matching conventional titles get `type: ...`
  - otherwise they fall back to `needs-triage`
- Include `Generated with Codex` in the issue body attribution section when Codex materially helped draft the issue.

## GitHub Protection Expectations

- `main` should be protected in GitHub settings.
- Direct pushes to `main` should be disabled.
- Require at least one review before merge.
- Require status checks to pass before merge.
- The important required checks should include:
  - `Build`
  - `PR Title Check`
  - `Branch Name Check`

## GitHub Automation

- PR metadata sync should keep assignees, linked issues, and attribution updated.
- Issue metadata sync should keep assignees, labels, and attribution updated.
- PR / Issue guide comments should be updated in-place instead of spamming new comments.
- PR CI automation should:
  - post a short CI status comment
  - auto squash-merge eligible PRs to the default branch after required checks pass
- Merged PR branches should be deleted automatically when safe.

## When Editing

- Check whether a change affects:
  - notch visibility
  - click-through behavior
  - hover expansion timing
  - foreground window detection
  - media progress slider behavior
- If changing native interop or overlay logic, keep the diff as small as possible.

## Preferred Style

- concise code
- clear names
- minimal comments
- no dead code left behind
