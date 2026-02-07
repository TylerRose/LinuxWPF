# WPF on Linux with Proton

This project demonstrates running Windows Presentation Foundation (WPF) applications on Linux using Steam's Proton compatibility layer, with a hot-reload development workflow.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Linux Host                              │
│  ┌───────────────────┐     ┌─────────────────────────────────┐  │
│  │   dotnet build    │────▶│  MyWPFApp.dll (win-x64)         │  │
│  │   (Linux .NET)    │     │  Built on Linux, runs in Wine   │  │
│  └───────────────────┘     └─────────────────────────────────┘  │
│                                           │                      │
│  ┌────────────────────────────────────────▼──────────────────┐  │
│  │                    Proton / Wine                          │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │                   WpfHost.exe                       │  │  │
│  │  │  • Persistent WPF Dispatcher                        │  │  │
│  │  │  • Loads MyWPFApp.dll dynamically                   │  │  │
│  │  │  • FileSystemWatcher for changes                    │  │  │
│  │  │  • AssemblyLoadContext (collectible) for reload     │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Projects

### MyWPFApp

The main WPF application containing your UI and business logic.

**Key Files:**
- `MainWindow.xaml` - Main window UI definition
- `MainWindow.xaml.cs` - Code-behind for the main window
- `MyWPFApp.csproj` - Project configuration with Proton launch settings

**Project Configuration:**
```xml
<TargetFramework>net10.0-windows</TargetFramework>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<UseWPF>true</UseWPF>
<EnableWindowsTargeting>true</EnableWindowsTargeting>
```

**Proton Integration:**

The `.csproj` includes MSBuild properties to launch the app via Proton when running `dotnet run`:

```xml
<RunCommand>bash</RunCommand>
<RunArguments>-c "STEAM_COMPAT_CLIENT_INSTALL_PATH='$(SteamRoot)' 
  STEAM_COMPAT_DATA_PATH='$(ProtonPrefix)' 
  PROTON_USE_WINED3D=1 
  '$(ProtonPath)/proton' run '...MyWPFApp.exe'"</RunArguments>
```

### WpfHost

A lightweight WPF shell that provides a persistent Windows message loop and dynamically loads the main application.

**Purpose:**
- Maintains a stable WPF Dispatcher running in Proton
- Loads `MyWPFApp.dll` at runtime using `AssemblyLoadContext`
- Watches for DLL changes and reloads automatically
- Provides manual Reload/Unload controls

**Key Features:**
- **Collectible AssemblyLoadContext**: Allows unloading and reloading assemblies without restarting
- **FileSystemWatcher**: Monitors the build output directory for changes
- **Content Extraction**: Extracts `MainWindow.Content` from the loaded app and hosts it

**How It Works:**

1. WpfHost creates its own window with a `Border` container
2. When loading, it reads `MyWPFApp.dll` into a new `AssemblyLoadContext`
3. It instantiates `MyWPFApp.MainWindow` via reflection
4. It extracts the window's `Content` and places it in the host's container
5. When changes are detected, it unloads the old context and repeats

## Prerequisites

- **.NET 10 SDK** (Linux native)
- **Steam** with Proton Experimental installed
- **Proton prefix** configured at `~/.proton`

### Setting Up Proton Prefix

```bash
# Create the prefix directory
mkdir -p ~/.proton

# First run will initialize the prefix
STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.local/share/Steam" \
STEAM_COMPAT_DATA_PATH="$HOME/.proton" \
"$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/proton" run \
  /bin/true
```

## Usage

### Quick Start (Standalone App)

Build and run the app directly:

```bash
cd MyWPFApp
dotnet run
```

This builds the app and launches it via Proton in a single command.

### Development with Hot Reload (WpfHost)

For active development with hot-reload capability:

```bash
# Start the host (builds both projects automatically)
./run-host.sh
```

Then, in another terminal, make changes and rebuild:

```bash
dotnet build MyWPFApp/MyWPFApp.csproj
```

The host will detect the change and reload the content automatically. You can also use the **Reload** button in the host window.

### Manual Commands

```bash
# Build the main app
dotnet build MyWPFApp/MyWPFApp.csproj

# Build the host
dotnet build WpfHost/WpfHost.csproj

# Run the host via Proton
STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.local/share/Steam" \
STEAM_COMPAT_DATA_PATH="$HOME/.proton" \
PROTON_USE_WINED3D=1 \
"$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/proton" run \
  WpfHost/bin/Debug/net10.0-windows/win-x64/WpfHost.exe
```

## How the Projects Interact

```
Developer Workflow
──────────────────

1. Edit MyWPFApp source files (XAML, C#)
           │
           ▼
2. Run: dotnet build MyWPFApp/MyWPFApp.csproj
           │
           ▼
3. Linux .NET builds → MyWPFApp.dll (win-x64)
           │
           ▼
4. FileSystemWatcher (in WpfHost) detects change
           │
           ▼
5. WpfHost unloads old AssemblyLoadContext
           │
           ▼
6. WpfHost loads new MyWPFApp.dll
           │
           ▼
7. Content appears in host window (no restart needed!)
```

### Path Translation (Wine Z: Drive)

Wine maps the Linux root filesystem to the `Z:` drive. The WpfHost uses this for path translation:

```csharp
// Linux path: /home/user/wpf/MyWPFApp/bin/Debug/.../MyWPFApp.dll
// Wine path:  Z:\home\user\wpf\MyWPFApp\bin\Debug\...\MyWPFApp.dll
```

This allows the Windows process to access files built by the Linux .NET SDK.

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `STEAM_COMPAT_CLIENT_INSTALL_PATH` | Steam installation directory |
| `STEAM_COMPAT_DATA_PATH` | Proton prefix (Wine bottle) location |
| `PROTON_USE_WINED3D` | Use WineD3D instead of DXVK (better compatibility) |

## Known Limitations

1. **No Native Hot Reload**: `dotnet watch` cannot inject the Hot Reload agent into Wine processes. The WpfHost architecture provides an alternative.

2. **No VS Code Debugger Attach**: Cannot attach the .NET debugger to Wine processes. Use `Console.WriteLine` for debugging output.

3. **FileSystemWatcher Reliability**: Wine's FileSystemWatcher may not always trigger. Use the manual Reload button as a fallback.

4. **Path Hardcoding**: The WpfHost currently has hardcoded paths. Modify `HostWindow.xaml.cs` if your workspace location differs.

## Project Structure

```
wpf/
├── README.md                 # This file
├── wpf.sln                   # Solution file
├── run-host.sh               # Launch script for WpfHost
│
├── MyWPFApp/                 # Main WPF Application
│   ├── MyWPFApp.csproj       # Project with Proton configuration
│   ├── App.xaml              # Application definition
│   ├── App.xaml.cs
│   ├── MainWindow.xaml       # Main window UI
│   ├── MainWindow.xaml.cs
│   └── watch-dev.sh          # Alternative file watcher script
│
└── WpfHost/                  # Hot Reload Host
    ├── WpfHost.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── HostWindow.xaml       # Host window UI
    └── HostWindow.xaml.cs    # Dynamic loading logic
```

## Troubleshooting

### Window doesn't appear
- Check that Proton prefix is initialized
- Verify Steam and Proton Experimental are installed
- Try running with `PROTON_USE_WINED3D=1`

### "DLL not found" in WpfHost
- Ensure `MyWPFApp` is built: `dotnet build MyWPFApp/MyWPFApp.csproj`
- Verify the Z: drive path in `HostWindow.xaml.cs` matches your system

### Changes not detected
- FileSystemWatcher may be unreliable in Wine
- Use the **Reload** button in the host window
- Or restart the host: `./run-host.sh`

### Build errors about Windows APIs
- Ensure `<EnableWindowsTargeting>true</EnableWindowsTargeting>` is set
- Use .NET 10+ which supports cross-OS Windows targeting