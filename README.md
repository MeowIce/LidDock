# LidDock

Intelligent macOS-style Clamshell Mode for Windows 11 laptops.

Still opening Control Panel -> Hardware and Sound -> Power Options -> "Choose what closing the lid does" twice every single day like it is 2006? Still baking your laptop in your backpack because you forgot to revert "Do Nothing" back to "Sleep" before unplugging?

LidDock solves this forever. Set it and forget it.

---

## What It Does

When an external monitor is plugged in, closing your laptop lid keeps your machine awake (Do Nothing).
When you unplug the monitor, your laptop automatically reverts to sleeping when closed (Sleep).

If an abrupt disconnect occurs while the lid remains closed (for example, kicking out your USB-C dock cable), LidDock activates a configurable Safety Grace Period (5 to 10 seconds). If no monitor reconnects within that window, it actively initiates system sleep via native Win32 `SetSuspendState` to eliminate overheating and battery drain.

---

## Features

- Zero Polling Event-Driven Architecture: Uses native Win32 Connecting and Configuring Displays (CCD) APIs (`QueryDisplayConfig`) and Power Setting Notifications (`GUID_LIDSWITCH_STATE_CHANGE`). Zero CPU consumption when idle.
- Extreme Efficiency: Native AOT single-file binary consuming approximately 350 KB Working Set memory in physical RAM.
- Fail-Safe Grace Period: Actively triggers system suspension if a dock or display is disconnected while the lid is closed.
- Profiles:
  - Smart Docked (Recommended): Automatic clamshell activation when docked to an external physical display, switching back to sleep when undocked.
  - AC Power Only: Clamshell mode restricted strictly to wall power connection to preserve battery health.
  - Always Clamshell: Clamshell mode remains active whenever an external monitor is attached, even on battery power.
- Fluent WinUI 3 Design: Settings interface styled after modern Windows 11 with Mica and Acrylic backdrops.
- Silent System Tray Daemon: Color-coded tray icon indicating docked and lid states with context menu controls.
- Automatic Update Checking: Background release checking against GitHub Releases with manual check triggers.
- In-Memory Diagnostics Ring Buffer: Thread-safe diagnostic logging and system state capture without disk wear.

---

## How to Use

### 1. First-Time Setup
- Download and run `LidDock.exe`. The Settings window opens automatically on first launch.
- Under General, toggle **Start with Windows** on so LidDock runs silently in the background on startup.
- Choose your preferred Profile under the Profiles tab (`Smart Docked` is recommended).

### 2. Daily Workflow
- **Docking**: Plug in your external monitor or USB-C/Thunderbolt dock, then close your laptop lid. Your display continues running smoothly on the external monitor without sleeping.
- **Undocking**: Unplug your cable. LidDock instantly restores standard Windows sleep behavior when closing the lid.
- **Accidental Disconnect**: If you unplug your dock while the lid is closed, LidDock's grace period countdown begins. If not reconnected within 5 to 10 seconds, it safely initiates system sleep to prevent overheating in laptop sleeves or bags.

### 3. System Tray Controls
- **Left-Click / Double-Click**: Opens the LidDock Settings window.
- **Right-Click Menu**: Quick access to toggle clamshell mode, switch operational profiles, open Diagnostics, check for updates, or exit the application.
- **Icon Status**:
  - Blue: Docked with lid open (external display connected, ready for clamshell).
  - Green: Clamshell active (external display connected, laptop lid closed and running).
  - Orange: Disconnect pending (safety grace period countdown active).
  - Gray: Undocked / Standard (no external display, normal sleep behavior).

---

## Architecture

LidDock is structured into modular layers:

- [LidDock.Core](file:///C:/Users/MeowIce/Documents/LidDock/src/LidDock.Core): Domain models, profile evaluation, state machine logic.
- [LidDock.Windows](file:///C:/Users/MeowIce/Documents/LidDock/src/LidDock.Windows): Low-level Win32 interop, CCD display queries, power scheme overrides, native message pump.
- [LidDock.Diagnostics](file:///C:/Users/MeowIce/Documents/LidDock/src/LidDock.Diagnostics): In-memory ring buffer logger and hardware snapshot diagnostics.
- [LidDock.Daemon](file:///C:/Users/MeowIce/Documents/LidDock/src/LidDock.Daemon): Native AOT background daemon and system tray coordination.
- [LidDock.App](file:///C:/Users/MeowIce/Documents/LidDock/src/LidDock.App): Fluent WPF user interface for configuration and diagnostics.
- [LidDock.Tests](file:///C:/Users/MeowIce/Documents/LidDock/tests/LidDock.Tests): Automated xUnit test suite for state machine transitions and safety triggers.

---

## Requirements

- Windows 11 (build 22000 or higher recommended)
- .NET 10.0 SDK (for building from source)
- Desktop C++ tools (for Native AOT compilation)

---

## Installation

1. Download the latest release from GitHub Releases.
2. Place `LidDock.exe` in your preferred directory (or let it self-install to `%LocalAppData%\LidDock`).
3. Run `LidDock.exe`.
4. Open Settings from the tray icon or main window, and enable "Start with Windows".

---

## Building from Source

Ensure .NET 10.0 SDK and MSVC C++ build tools are installed.

Clone the repository and run the build script:

```cmd
git clone https://github.com/MeowIce/LidDock.git
cd LidDock
build.bat
```

The build script will:
1. Publish the Fluent UI payload (`LidDock.App`) in self-contained mode.
2. Compile the Native AOT single-file executable (`LidDock.Daemon`) into `publish\LidDock.exe`.

To run automated unit tests:

```cmd
dotnet test
```
