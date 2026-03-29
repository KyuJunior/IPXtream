# IPXtream — WPF IPTV Player

A modern Windows Desktop IPTV player using the Xtream Codes API.

## Tech Stack
- **.NET 8 / WPF** — MVVM with `CommunityToolkit.Mvvm`
- **LibVLCSharp.WPF** — hardware-accelerated video (HLS/TS/m3u8)
- **Newtonsoft.Json** — Xtream API deserialization
- **Windows DPAPI** — encrypted "Remember Me" credential storage

## Project Structure
```
IPXtream/
├── Models/
│   ├── AuthResponse.cs       # Auth + UserInfo + ServerInfo
│   ├── Category.cs           # Live/VOD/Series category
│   ├── StreamItem.cs         # Live channel / VOD / Series item
│   └── UserCredentials.cs    # Credentials + URL builders
├── Services/
│   └── XtreamApiService.cs   # All API calls
├── Helpers/
│   ├── CredentialStore.cs    # DPAPI save/load
│   ├── Converters.cs         # Bool/String → Visibility converters
│   └── SectionToStyleConverter.cs
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── DashboardViewModel.cs
│   └── PlayerViewModel.cs
├── Views/
│   ├── LoginWindow.xaml(.cs)
│   ├── DashboardWindow.xaml(.cs)
│   └── PlayerWindow.xaml(.cs)
├── App.xaml(.cs)
└── IPXtream.csproj
```

## Build & Run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (x64)

### Restore and build
```powershell
cd c:\Myapps\ipxtream\IPXtream
dotnet restore
dotnet build -c Release
```

### Run (debug)
```powershell
dotnet run
```

## Xtream API Endpoints Used
| Endpoint | Action |
|----------|--------|
| `/player_api.php?username=&password=` | Authentication |
| `...&action=get_live_categories` | Live TV categories |
| `...&action=get_vod_categories` | Movie categories |
| `...&action=get_series_categories` | Series categories |
| `...&action=get_live_streams&category_id=X` | Channels for category |
| `...&action=get_vod_streams&category_id=X` | Movies for category |
| `...&action=get_series&category_id=X` | Series for category |

## Stream URL Formats
| Type | URL Pattern |
|------|------------|
| Live | `{server}/live/{user}/{pass}/{id}.ts` |
| Movie | `{server}/movie/{user}/{pass}/{id}.{ext}` |
| Series | `{server}/series/{user}/{pass}/{id}.{ext}` |

## Keyboard Shortcuts (Player)
| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `F` / `F11` | Toggle fullscreen |
| `M` | Toggle mute |
| `↑` / `↓` | Volume +5 / -5 |
| `Esc` | Exit fullscreen / Close player |
