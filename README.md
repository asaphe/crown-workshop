# Crown Workshop

A local Windows save editor for **Defender of the Crown: The Legend Returns — Retro and Classic modes**. Unofficial and not affiliated with the game's developers or publisher.

## Features

- Player-first editing of gold, troops, character skills and existing saved values.
- Opponents grouped by lord.
- Territories contain land settings and their castle garrison; the home territory is marked.
- One Changes tab, Undo, revert, export, automatic backups and save discovery.
- Advanced fields for inspecting IDs and raw values.
- Standalone WebView2 desktop window; no web server or network access required while editing.

## Download and run (Windows)

1. Choose **Code > Download ZIP** on GitHub, then **Extract all** in Windows.
2. Open the extracted folder and double-click **Launch Crown Workshop.cmd**.
3. On the first run, choose **Y** to download the Microsoft WebView2 SDK and prepare the editor. Later launches open the editor directly.

Keep the extracted folder together. Do not launch from inside the ZIP. The SDK and compiled files stay in `.sdk` and `build` inside this folder; no Python or system-wide SDK installation is needed. First-time setup needs internet access. Close the editor before replacing/updating its files.

Requires Windows x64, the .NET Framework compiler included with Windows, and the Microsoft Edge WebView2 Runtime. If the runtime is missing, install it separately from Microsoft's [WebView2 download page](https://developer.microsoft.com/microsoft-edge/webview2/).

### Manual build (developers)

```powershell
.\Build.ps1 -RestoreSdk
.\build\CrownWorkshop.exe
```

This downloads Microsoft.Web.WebView2 1.0.4258.31 from NuGet. Later manual builds can omit `-RestoreSdk`. HTML, CSS and JavaScript source live at the repository root and are copied into `build/web` during the build. Third-party SDK components retain their own license.

Game artwork, logos, extracted fonts, game DLLs, saves and generated binaries are not included. The public UI uses system font fallbacks and a plain gradient. An optional local `enum-labels.tsv` provides friendly enum labels; without it, stored values remain editable numerically. It is not required to parse a save.

## Use

Close the game before saving. Choose save lists Retro and Classic campaigns, including Classic named slots and autosaves. It excludes metadata and backups. Browse files opens other saves or backups. The mode is detected from save contents. Retro uses `Classic/ClassicSave.sav`; modern Classic uses the `Updated` folder. Classic garrisons appear inside their territories, even without a castle.

Edits remain in memory until Save changes. Review all pending edits in Changes. Every overwrite creates a backup beside the destination in `CrownWorkshopBackups`. To restore, open a backup and export it to the desired save path. Existing files are backed up before replacement. Steam Cloud may subsequently replace local files.

The editor blocks saving while the game is running and refuses to overwrite an opened file whose bytes have changed on disk. Account progression and Kingdom mode are not supported. Adding or removing entities is not supported. Some values may be recalculated by the game; arbitrary values are not guaranteed to produce valid gameplay.

For Classic, edit an existing save slot and load that same slot in-game. The editor leaves save-list metadata unchanged; exported copies may not appear in that list. Classic support passed local parser and session checks and a manual in-game test. This does not validate every possible edit.

## Source and validation

`CrownSave.cs` parses the binary save and preserves untouched bytes. It does not execute serialized objects. `Desktop.cs` contains the Windows host and editing session. `SessionTests.cs` exercises edits, validation, Undo, and model relationships using a supplied Retro save without writing it. `Tests.cs` is a deeper, fixture-specific parser suite; its known-player assertions require the original development fixture, which is intentionally not published. No automated in-game test is included.

## License

MIT; see LICENSE. Game names and third-party assets belong to their respective owners.
