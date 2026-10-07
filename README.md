# Spotlight Native

A Spotlight-style launcher for Windows.

![Screenshot placeholder](docs/screenshot.png)

## What it does

- Opens from a global hotkey. The default for new installations is `Ctrl+Alt+Space`.
- Runs in the system tray.
- Searches Start Menu and Desktop shortcuts.
- Searches files and folders in the configured locations. The first index is built in the background.
- Supports fuzzy matching, including partial words, skipped letters, typos, word reordering, and accented names.
- Shows a calculator result for supported expressions such as `12*(3+4)/2`, `sqrt(16)`, and `2^10`.
- Searches Google with `g ` and YouTube with `yt `.
- Remembers launch counts and recent launches when ranking results.
- Supports Start with Windows.

## Download and install

Open the project's **Releases** page and download `Setup.exe`.

Run `Setup.exe` and follow the installer. It installs the app under Program Files, adds a Start Menu shortcut, and can add a Desktop shortcut and Start with Windows entry.

Windows SmartScreen may warn because this application is not code-signed. If that happens, select **More info**, then **Run anyway**. Only do this when you downloaded the installer from the project's Releases page.

## How to use it

1. Press `Ctrl+Alt+Space` to open the launcher.
2. Type an app name, file name, folder name, calculator expression, or web query.
3. Use Up and Down to select a result.
4. Press Enter to run the selected result.
5. Press Ctrl+Enter to open the containing folder for a file.
6. Press Escape or click another window to hide the launcher.

For web searches, type for example:

```text
g windows updates
yt lo-fi music
```

The web search result is selected automatically for these prefixes.

## Changing the hotkey

The configuration file is:

```text
%AppData%\SpotlightNative\config.json
```

Change the `hotkey` value, for example:

```json
{
  "hotkey": "Ctrl+Alt+Space"
}
```

The app reloads the file and registers the new hotkey without a restart. Do not use `Alt+Space`; Windows uses that shortcut for window menus. Supported modifier names are `Ctrl`, `Alt`, `Shift`, and `Win`. The key can be a single letter or `Space`.

The same file controls indexed folders, excluded folder names, result limits, fuzzy matching, calculator angle mode, web search URL templates, and `autostart`.

## Requirements

- Windows 10 or Windows 11.
- The installer is self-contained. You do not need to install .NET.

## Uninstalling

Open **Settings > Apps > Installed apps**, find **Spotlight Native**, and choose **Uninstall**. You can also use the **Uninstall** shortcut in the Start Menu.

Uninstalling removes the installed program and shortcuts. User data such as the configuration, search index, usage history, and startup log remains in `%AppData%\SpotlightNative`.

## Known limitations

- This is a Windows-only WPF application.
- The first file index can take time on a PC with many files. The launcher remains usable while it runs.
- Search is limited to the folders configured in `indexedFolders`; system folders and excluded folder names are skipped.
- The app is not code-signed, so SmartScreen can show a warning.
- The app uses the Windows Run registry entry for Start with Windows. It does not run as a Windows service.
- Search shortcut icons depend on Windows shell icon extraction and may be missing for some shortcuts.

## Building from source

Requires the .NET 10 SDK and Windows:

```powershell
dotnet restore .\SpotlightNative.csproj
dotnet build .\SpotlightNative.csproj
dotnet run --project .\SpotlightNative.csproj
```

## License

No license file is currently included. Add a license before distributing this project.
