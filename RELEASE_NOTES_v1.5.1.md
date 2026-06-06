# IPXtream v1.5.1 — What's New

## 🎬 Complete Player Engine Migration (LibVLC → FlyleafLib)

The video player has been completely rebuilt using **FlyleafLib v3.10.3** with native **FFmpeg v7.1 / DirectX** rendering — no separate codec installation required.

- ✅ **Bundled FFmpeg v7.1** — `avcodec`, `avformat`, `avutil`, and friends ship inside the installer
- ✅ **Hardware-accelerated DirectX rendering** — smoother playback, especially on high-bitrate streams
- ✅ **No more black or white bars** around the video — viewport fills the container perfectly on any screen size or window state

---

## 🎛️ New Player Controls

| Control | Description |
|---|---|
| ⏪ **Skip Back 5s** | Jump backward 5 seconds |
| ▶ / ⏸ **Play / Pause** | Toggle playback |
| ⏩ **Skip Forward 5s** | Jump forward 5 seconds |
| ⏹ **Stop** | Stop and return to library |
| 🔊 **Volume Slider** | Live volume with mute toggle |
| **SPEED** | Playback speed: `0.5×` `0.75×` `1×` `1.25×` `1.5×` `2×` |
| **DUB** | Switch audio track (multi-audio streams & MKV files) |
| **SUB** | Switch subtitle track (embedded subtitle streams) |

---

## ⏱️ Silky Smooth Seekbar

- Timeline position refreshes at **20 fps (50 ms interval)** — the thumb glides naturally
- **Click-anywhere-to-seek** — instantly jumps to the click point, no dragging required

---

## 🐭 Fixed: Controls Now Appear on Mouse Hover

Resolved a deep WPF/DirectX "Airspace" bug where the native DirectX surface was intercepting all mouse events, preventing the controls bar from appearing on hover. The overlay is now rendered in a **transparent WPF Popup** that correctly layers above the DirectX surface.

---

## 🎵 Fixed: Audio (DUB) and Subtitle (SUB) Track Lists Now Populated

Previously both dropdowns were empty. Fixed by binding to the correct FlyleafLib API:

- `Player.Audio.Streams` (was incorrectly `.Tracks`)
- `Player.Subtitles.Streams` (was incorrectly `.Tracks`)
- Track switching uses `Player.OpenAsync(stream)` — the proper public API

Each track displays as `Language (Codec)` — e.g. `eng (aac)`, `ara (ac3)`.

> **Note:** Live TV streams typically have a single muxed audio track (DUB will show one entry). MKV movies and multi-audio VOD files will populate both lists dynamically.

---

## ⌨️ Keyboard Shortcuts

| Key | Action |
|---|---|
| `Space` | Play / Pause |
| `F` / `F11` | Toggle Fullscreen |
| `M` | Mute / Unmute |
| `↑` / `↓` | Volume +5 / −5 |
| `Escape` | Exit Fullscreen or Close Player |
| `Double-click video` | Toggle Fullscreen |

---

## 🐛 Bug Fixes

- Fixed black/white bars above and below the video on all screen sizes
- Fixed "Playback Error" indicator persisting after stream starts successfully
- Fixed all player controls being non-functional after FlyleafLib migration
- Fixed player spawning a separate floating window on first launch (caused by zero-size host)
- Fixed DUB and SUB ComboBoxes showing empty lists on all streams
- Fixed controls overlay not responding to mouse hover

---

## 📦 Installation Notes

- **OS:** Windows 10 x64 or later
- **.NET Runtime:** Self-contained — no separate install required
- **FFmpeg:** Bundled — no separate codec install required
- Existing installations can be upgraded in-place; credentials are preserved
