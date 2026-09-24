# Contributing

Thanks for looking at Keyboard Sound. This is a small, focused Windows utility app - the goal
of any change should be to keep it that way. By participating, you're expected to follow the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Setup

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet build KeyboardSound.sln
dotnet test src\KeyboardSound.Tests
dotnet run --project src\KeyboardSound.App
```

See [README.md](README.md) for the production build command and
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the module map and design decisions - read that
before touching `Core/`, it explains why things are split the way they are.

## Before opening a PR

1. `dotnet build KeyboardSound.sln` - zero errors, zero new warnings.
2. `dotnet test src\KeyboardSound.Tests` - all tests green. Add tests for any new logic in
   `KeyboardSound.Core` (the CI workflow runs the same suite on every push/PR).
3. If you touched anything under `assets\soundpacks\`, rebuild before testing manually - those
   files are copied to the build output at build time, so a stale copy is a common false negative.
4. If you can, actually run the app and exercise the feature you changed (start it, type, check
   the widget/tray/main window as relevant) - the test suite covers `Core`, not the UI.

## Where things live

- `KeyboardSound.Core` - all logic (input, audio, soundpacks, settings). No WPF/WinForms
  dependency. If you're adding something that doesn't need Windows UI APIs, it probably belongs
  here, and it should be unit-testable without a real audio device or window.
- `KeyboardSound.App` - the WPF shell. Composition root (`App.xaml.cs`) wires `Core` services
  together; windows/tray code stays thin and delegates to `Core.AppState.ApplicationState`.
- `KeyboardSound.Tests` - xUnit tests for `Core`. No UI tests currently; changes there are
  verified manually (see above).

## Adding a soundpack

Drop a folder under `assets\soundpacks\` with a `pack.json` and category subfolders (`normal`,
`space`, `enter`, `backspace`, `shift`, `ctrl`, `alt`, `tab`, `other`), `.wav` or `.ogg` samples.
Optionally give samples stable, curated ids/display names via pack.json's `"sounds"` array - see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#individual-sounds-and-stable-ids). Nothing in the
code references a pack or sample by name, so this needs no code changes. Only include audio you
have the rights to redistribute, and note the license in the pack's own metadata (see
`assets\soundpacks\KenneyClick\License.txt` for the pattern).

## Code style

- No comments that restate what the code does. A comment earns its place by explaining a
  non-obvious *why* (a constraint, an invariant, a workaround) - see the existing code for the
  bar to clear.
- Don't add abstractions, config options, or error handling for cases that can't happen. Prefer
  the smallest change that correctly solves the actual problem.
- Match the existing layering: input never touches audio directly, audio never touches keys -
  both only know about `SoundCategory`/`Sound`. Keep it that way when adding features.

## Reporting issues

Open a GitHub issue with what you expected vs. what happened, your Windows version, and (if
audio-related) which soundpack was active. Debug logging can be turned on in the main window's
Settings section - logs go to `%LOCALAPPDATA%\KeyboardSound\logs\app.log`.
