# Keyboard Sound - Architecture

## Technology decision

**C# / .NET 8, WPF UI, Win32 low-level keyboard hook, NAudio (WASAPI) for playback.**

Compared against the options the spec asked to evaluate:

| | Electron | C++ native | Tauri/Rust | **C#/.NET (chosen)** |
|---|---|---|---|---|
| Global key hook | via native addon | direct WinAPI | via native crate | direct WinAPI (P/Invoke) |
| Audio latency | poor (Web Audio via Chromium) | best possible | good (needs a mixing crate) | good (WASAPI shared mode, ~40ms) |
| Idle CPU/RAM | high (bundled Chromium + Node) | lowest | low | low-moderate (CLR + WPF baseline) |
| Transparent/frameless widget | possible, heavy | manual, more code | possible via webview | native WPF support |
| Dev speed / risk for one foundation loop | fast, wrong tradeoffs | slow, high risk of a fragile foundation | needs a toolchain not present here either | fast, mature APIs |
| System tray | ok | manual | ok | native (NotifyIcon) |
| Packaging / later Steam | large installer | smallest | medium | self-contained single-file publish works fine |

Electron was rejected outright: it's exactly the "feels heavy" failure mode the product spec
explicitly warns against (bundled Chromium/Node process, high idle memory). C++ would give the
best possible latency and footprint, but hand-rolling WASAPI mixing, hook lifetime management,
and a modern transparent UI correctly *in a single foundation pass* carries real risk of leaving
a fragile base to build on. Tauri/Rust is attractive long-term but needs a Rust + Node/webview
toolchain that wasn't installed on this machine either, without a clear benefit over .NET for
this specific app. C#/.NET 8 gives mature global-hook interop, WASAPI-backed low-latency audio
via NAudio, a native system tray, cheap borderless/transparent windows in WPF, and a
self-contained single-file publish path that's a reasonable base for a later Steam build.

## Module map

```
src/
  KeyboardSound.Core/          Plain class library. No UI framework dependency beyond
                                Windows-only APIs (P/Invoke, NAudio, registry-free).
    Input/                     Win32 keyboard hook, VK -> LogicalKey mapping, key dedup.
    Audio/                     NAudio-based mixer/playback engine, sample caching.
    SoundPacks/                Soundpack discovery, metadata, category/key mapping,
                                sample selection (with fallback + no-immediate-repeat).
    Settings/                  Persisted user settings model + load/save/repair.
    Persistence/                Generic crash-safe JSON file store.
    Widget/                    Screen-bounds-aware widget position resolution.
    Routing/                   Wires Input -> (mode/key check) -> category -> Audio.
    AppState/                  Central orchestrator: owns startup/shutdown sequencing.
    Configuration/              Centralized filesystem paths.
    Diagnostics/                Lightweight leveled logger (off by default at Debug level).

  KeyboardSound.App/           WPF executable (composition root only; no business logic).
    Ui/                         MainWindow, WidgetWindow, theme, icon generation, converters.
    Tray/                       System tray (NotifyIcon) integration.
    Configuration/              Windows "start with Windows" registry integration.
    App.xaml.cs                 Startup/shutdown sequencing, wires Core services together.

  KeyboardSound.Tests/         xUnit tests for every non-UI module in Core.

assets/soundpacks/CuratedClick/ Default pack: 7 hand-picked real samples, each with curated
                                 metadata (stable id + display name) in pack.json - see
                                 "Individual sounds" below.
assets/soundpacks/KenneyClick/  Real CC0 samples from Kenney's "Interface Sounds" pack
                                 (kenney.nl), mapped to key categories (not raw mechanical-
                                 switch recordings, but genuine licensed audio, not synthetic).
assets/soundpacks/Placeholder/  Synthetic fallback pack, no external assets (see
                                 tools/generate-placeholder-sounds.ps1). No pack is referenced
                                 by name in code - SoundPackManager discovers packs purely from
                                 folder structure, so adding/removing/replacing a pack folder
                                 requires no code changes.
```

Data flow for a keypress:

```
LowLevelKeyboardHook (WH_KEYBOARD_LL)
  -> KeyEvent (LogicalKey, Down/Up)                      [Input]
  -> InputRouter: key-mode check, enabled-key check       [Routing]
  -> KeyCategoryMap.Resolve(key) -> SoundCategory         [SoundPacks]
  -> IAudioEngine.Play(category, preferredSoundId)        [Audio]
       -> preferredSoundId (the pinned "selected" sound, if any) wins deterministically
          when it resolves to a sound loaded for this category;
       -> otherwise SampleSelector picks a sample (category, else Normal fallback, avoids repeats)
       -> CachedSound (already decoded in memory) mixed into the shared WASAPI output
```

### Individual sounds and stable ids

A soundpack isn't just "category -> file paths" - every sample is a `Sound` (`SoundPacks/Sound.cs`)
with a **stable id**, independent of its display name, file path, or position in any list:

```
Soundpack -> Category -> Sound { Id, DisplayName, Category, FilePath }
```

A pack.json can declare curated metadata for its samples:

```json
{
  "sounds": [
    { "id": "crisp_asmr_01", "displayName": "Crisp ASMR", "category": "Normal", "file": "normal/crisp-asmr.wav" }
  ]
}
```

matched to the discovered file by relative path. A sample with no curated entry still gets a
`Sound` record - `SoundPackManager` auto-derives `id = "{packId}_{category}_{sanitizedFileName}"`
and a title-cased display name from the file name. Auto ids are stable across reordering and
across new/removed sounds elsewhere in the pack, but - unlike curated ids - they do change if
that specific file is renamed; a pack that wants fully rename-proof ids should declare them
explicitly.

`AppSettings.FavoriteSoundIds` (favorite individual sounds) and `AppSettings.SelectedSoundId`
(pin one sound as the deterministic choice for its category) both store only this stable id -
never a name, file path, or list index. Both are plain additive fields: an older settings.json
that predates them simply gets the type's default (`[]` / `null`) the first time
`System.Text.Json` deserializes it, so no explicit migration code was needed - verified by
loading a genuinely pre-existing settings file from before this feature and confirming the app
started cleanly and wrote the new fields with their defaults on first save.

The input layer never touches audio directly, and the audio engine never touches keys - both
only know about `SoundCategory`, which keeps either side replaceable independently (e.g. a
future non-Win32 input source, or a different audio backend).

## Key design decisions worth calling out

- **Global hook dedup**: `LowLevelKeyboardHook` tracks currently-pressed virtual-key codes and
  only raises a Down event on the up->down transition, so Windows' OS-level key-repeat while a
  key is held doesn't retrigger the sound on every repeat tick.
- **No per-keypress disk I/O**: every sample in the active soundpack is decoded once into a
  shared `float[]` (`CachedSound`) when the pack is loaded; playing a sound just wraps that
  array in a cheap per-play cursor and hands it to a `MixingSampleProvider`, so fast typing
  never touches the filesystem and overlapping sounds mix for free.
- **Bounded concurrency**: the audio engine caps concurrent mixed voices (48) and precisely
  tracks when each voice's underlying `Read()` starts returning fewer samples than requested
  (the same condition NAudio's mixer uses internally to drop a finished input) - getting this
  condition right, rather than only checking for an exact `0`, is what prevents the voice
  counter from leaking and eventually blocking all further playback.
- **Settings never crash the app**: `JsonFileStore<T>` returns defaults and backs up the bad
  file on any deserialize failure; `SettingsService` additionally clamps/repairs values after
  load (e.g. an out-of-range volume) so a hand-edited or partially-written config can't leave
  the app in a broken state.
- **Widget position is resolution-safe**: `WidgetPositionService` keeps a saved position if any
  current monitor still shows a meaningful portion of the widget, otherwise clamps it back into
  the primary screen - handles a monitor being unplugged or a resolution change without the
  widget becoming unreachable. (Known simplification: it treats monitor bounds and WPF window
  coordinates as the same unit, i.e. assumes ~100% display scaling - see Limitations below.)
- **Extensible soundpack sources**: `ApplicationState` discovers packs from both the built-in
  `soundpacks/` folder next to the executable and a per-user folder under
  `%LOCALAPPDATA%\KeyboardSound\Soundpacks`, merging by id. Adding a "browse for folder" UI
  later is purely additive - it just needs to copy/point into that second directory.
- **Custom key mode's data model is UI-independent**: `AppSettings.EnabledKeys` is a plain
  `List<LogicalKey>`; the current UI exposes a small quick-select grid rather than a full
  virtual keyboard, but nothing about the underlying architecture assumes that - a full virtual
  keyboard view is a UI-only addition on top of `ApplicationState.SetEnabledKeys`.
- **Sample decoding is format-pluggable, not hardcoded to .wav**: `CachedSound.Load` branches on
  file extension - `.ogg` goes through NAudio.Vorbis (a managed Vorbis decoder, since Windows
  Media Foundation doesn't ship Ogg support by default), everything else through NAudio's
  built-in `AudioFileReader`. This was added because real freely-licensed sound libraries
  (including the shipped default pack) commonly distribute as `.ogg`; adding another format
  later is a single new branch, not a rearchitecture.

## Known limitations (this foundation loop)

- Widget positioning assumes ~100% display scaling when reasoning about monitor bounds (see
  above). On a scaled secondary monitor the widget can land slightly off from a mathematically
  perfect position; it is still always clamped to stay visible/reachable.
- The self-contained Release publish is large (~150MB) because it bundles the full .NET + WPF +
  WinForms runtime for a single-file, no-install-required executable. A framework-dependent
  publish (requires the .NET 8 desktop runtime on the target machine) would be a few MB instead;
  trimming was deliberately not attempted since WPF's reflection-based XAML loading makes
  trimming failure-prone without dedicated testing.
- Only a compact preset of keys is exposed for Custom Key mode in the UI (see above) - the full
  virtual-keyboard picker described in the product spec is future UI work on an already-ready
  data model.
- Soundpack switching decodes the newly-activated pack synchronously on the UI thread. With the
  small placeholder pack this is instant; a very large future soundpack could cause a brief UI
  pause worth moving to a background task later.
- No installer/MSIX packaging yet - only a raw publish output. Not needed for this foundation
  loop; worth adding before any real distribution.
- Soundpack files under `assets/soundpacks/` are copied to the build output at **build time**
  (`CopyToOutputDirectory=PreserveNewest` in the .csproj). Editing a pack.json or sample file and
  re-launching an already-built exe without rebuilding will run against the stale copy - always
  `dotnet build` after changing anything under `assets/`.
