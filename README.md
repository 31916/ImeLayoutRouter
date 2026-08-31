# IME Layout Router

A lightweight Windows utility that automatically routes Japanese IME direct input to your preferred keyboard layout.

For example:

- `あ` → Japanese IME
- `A` → Another selected keyboard layout

This avoids typing Latin characters through the Japanese IME's direct input mode.

## Download

**[Download the latest release](https://github.com/31916/ImeLayoutRouter/releases/latest)**

Download `ImeLayoutRouter-Setup.exe` from the latest GitHub Release.

> Windows only.

## Why?

When using a Japanese IME together with another keyboard layout, switching the IME to direct input (`A`) may still keep the Japanese keyboard profile active.

IME Layout Router automatically redirects that state to your preferred keyboard layout.

Example:

```text
Japanese IME: あ
      ↓
Japanese IME: A
      ↓
Deutsch (Schweiz)
```

When switching back to the Japanese IME, the app restores native IME input mode.

```text
Deutsch (Schweiz)
      ↓
Japanese IME: あ
```

## Features

- Automatically detects Japanese IME direct input mode
- Routes direct input to a selected keyboard layout
- Restores Japanese IME native input mode when switching back
- Selectable source Japanese IME
- Selectable target keyboard layout
- System tray application
- Optional startup with Windows
- Prevents multiple instances from running
- Stores configuration locally

## Installation

1. Open the **[latest GitHub Release](https://github.com/31916/ImeLayoutRouter/releases/latest)**.
2. Download `ImeLayoutRouter-Setup.exe`.
3. Run the installer.
4. Launch **IME Layout Router** after installation.
5. Select:
   - **Source Japanese IME**
   - **Target Keyboard Layout**
6. Click **Save**.

Enable **Start with Windows** if you want IME Layout Router to start automatically after signing in.

## Usage

IME Layout Router runs in the Windows system tray.

Right-click the tray icon to access:

- **Settings**
- **Exit**

Once configured, no manual operation is normally required.

## Supported Environment

- Windows
- Japanese IME
- Standard Windows keyboard layouts

Tested with:

- Google Japanese Input
- Deutsch (Schweiz)

## Known Limitations

Version 1 focuses on Japanese IMEs.

- Chinese and Korean IMEs are not currently supported.
- Systems with multiple Japanese IMEs enabled may not always distinguish between them during runtime monitoring.
- The application currently targets Windows x64.

## How It Works

IME Layout Router uses Windows input APIs including:

- Text Services Framework (TSF)
- IMM32
- Win32 keyboard layout APIs

The application monitors the active input state and switches between the configured Japanese IME and target keyboard layout when necessary.

## Build from Source

Requirements:

- .NET 8 SDK
- Windows

Clone the repository and run:

```powershell
dotnet run
```

To create a release build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## Releases

Stable builds are distributed through **[GitHub Releases](https://github.com/31916/ImeLayoutRouter/releases)**.

For normal use, downloading the installer is recommended instead of building the project manually.
