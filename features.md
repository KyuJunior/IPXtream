# IPXtream IPTV Client - Application Features & Design Documentation

Welcome to the comprehensive features and design documentation for **IPXtream**, a modern, premium WPF-based IPTV client. Below is a detailed breakdown of the features, architecture, and visual aesthetics engineered into the application.

---

## 📺 Playback Engine Architecture & Media Controls

IPXtream implements a multi-engine media architecture to handle diverse streaming protocols (HLS, M3U8, RTMP, RTSP, and progressive MP4/MKV files).

### 1. Multiple Built-in Playback Engines
* **Flyleaf Engine**: A high-performance FFmpeg-based hardware-accelerated media player providing sub-second buffering and low latency for live streams.
* **VLC Engine**: Integrates libVLC for exceptional format compatibility and robust handling of network streams.
* **MediaElement**: Uses the native Windows Media Foundation framework for lightweight playback of standard media formats.
* **MPV Engine**: A highly optimized player engine popular for its advanced scaling shaders and keyboard responsiveness.
* **WebView2 Engine**: Embeds an Edge-based Chromium browser player, enabling playback of web-hosted content and iframe players.
* **Interactive Engine Selector**: Accessible directly from the client Settings panel, allowing users to switch engines on-the-fly without restarting the app.

### 2. Player Controls & Custom Overlay Selectors
* **Speed, Subtitle, and Audio Track Selectors**: Modernized, styled ComboBox controls built using customized WPF ControlTemplates for choosing subtitles, playback speed, and alternate audio tracks (Dub/SAP).
* **Next Episode Button & Shortcut**: Seamlessly skip to the next episode in a series directly from the overlay player bar or by pressing the `N` key.
* **Precision Seekbar & Hover Tooltips**: Smooth dragging mechanics with instant time updates and hover tooltips showing seek target locations.

---

## ✨ Visual Aesthetics, Glassmorphism & UI Animations

IPXtream has been designed with a premium, state-of-the-art dark aesthetic matching modern streaming platforms like Netflix and Apple TV+.

```
┌─────────────────────────────────────────────────────────┐
│                      IPXtream                           │
│  [Live TV] [Movies] [Series] [My Library] [Settings]    │
│ ┌─────────────────────────────────────────────────────┐ │
│ │                  Hero Banner                        │ │
│ │  ▶ Watch Now (Glow)         [+] Watchlist           │ │
│ └─────────────────────────────────────────────────────┘ │
│  Trending Channels (Smooth Hover Zoom: 1.0 -> 1.04)    │
│ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐  │
│ │ Card 1 │ │ Card 2 │ │ Card 3 │ │ Card 4 │ │ Card 5 │  │
│ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 1. Modern Login Window Experience
* **Geometric & Radial Gradient Backdrop**: Replaced flat dark colors with a deep `#080812` gradient and subtle purple/blue radial light accents.
* **Glassmorphic Card**: A centered login panel with high corner-radius styling (`20px`), a translucent backdrop, and an outer blur drop-shadow effect (`BlurRadius="40"`).
* **Input Field Polish**: Custom-styled TextBox and PasswordBox controls with a CornerRadius of `10px` and sleek focus glow rings.
* **Show/Hide Password Toggle**: Built-in visual eye icon that synchronizes the standard password box with a plain-text TextBox, using reentrancy guards to prevent cursor jitter.
* **Responsive Scrolling**: Form fields automatically wrap in a scrollable view with custom-styled scrollbars, preventing layout clipping on smaller screens or high DPI displays.

### 2. Premium Desktop Interactions
* **Dynamic Hover Animations**: Hovering over channel or stream cards triggers a smooth `DoubleAnimation` scaling them from `1.0` to `1.04` over 150ms with a quadratic ease-out transition.
* **Settings Modal Entrance**: Clicking Settings displays a glassy backdrop and pops in a centered overlay card using an elastic `BackEase` ease-out scaling curve (`0.9` to `1.0` in 220ms).
* **Page Indicators & Hero Buttons**: Fluent capsule-shaped page dots and highlighted primary buttons featuring deep purple-to-blue gradients (`#5B9BFF` to `#8B6BD4`).
* **Vector Icon Assets**: Swapped raw text emojis and Segoe MDL2 fonts with high-fidelity, resolution-independent vector paths derived from Fluent/Lucide iconography.
* **Silver Logo styling**: Replaced standard icon formats with a metallic, modern circular gradient logo.

---

## 📂 Content Organization & "My Library"

To help users manage thousands of IPTV streams, IPXtream features an advanced content aggregation dashboard.

### 1. "My Library" System
Organizes favorite streams across 5 dedicated shelves:
* **Favorites / Likes**: Easily add channels, movies, or series to your favorites with a single-click outlined heart button that toggles to a filled state.
* **Watch Later**: Queue content to watch in a future session.
* **Custom Shelves**: Organize media by custom categories.
* **Dynamic Library Search**: Real-time filtering search bar scoped specifically to your local library.

### 2. Streaming Tabs
* **Currently Watching**: A tracking shelf that remembers recently watched channels or videos, letting you resume playback with a single click.
* **Categorized Streams**: Live TV, Movies, and Series are organized into structured, flat tab headers with clear category tags.

---

## 🔒 Security & Data Resiliency

IPXtream prioritizes safety, data integrity, and connection speed in the background.

### 1. Robust Local Storage (`CredentialStore.cs`)
* **Thread-Safe File Lock**: Implements file access locking to ensure that concurrent configuration reads/writes (e.g., logging in, modifying favorites, updating settings) never corrupt data.
* **Atomic Disk Writes**: Writes settings to a temporary `.tmp` file and then atomically renames it. In the event of a power failure or app crash, the system will never lose existing configurations.

### 2. Network Resiliency & API Integration
* **Exponential Backoff Retries**: If the Xtream API experiences a network dropout, the service will attempt 3 retries with growing intervals (1s, 2s, 4s).
* **Differentiated Error Messages**: Detects and displays tailored notifications for 401/403 (Invalid Credentials), 429 (Rate Limited), and 5xx (Server Offline) errors instead of generic crash dialogs.
* **Cache Management (TTL)**: Stream directories are cached locally to speed up app loading, with an automatic TTL (Time-To-Live) refresh trigger of 60 minutes to pull new channel lists.

---

## 🎮 Navigation & Keyboard Controls

Designed with convenience in mind, the app supports keyboard-driven media playback control:

* **Seek Keys**: `Left Arrow` (Skip back 5s), `Right Arrow` (Skip forward 5s).
* **Global Search**: `Ctrl + F` immediately moves focus to the header search input.
* **Playback Toggle**: `Spacebar` pauses or resumes video (with focus checks to ensure typing space in a text field doesn't toggle playback).
* **Fullscreen Hotkeys**: `F`, `F11`, or `Double-Click` to enter/exit fullscreen.
* **Escape Key**: Exits fullscreen or closes active overlay panels.

---

## 🛠 Fullscreen Airspace Fix (Engineered Layout Resolution)

IPXtream resolves a classic WPF issue: the **Win32 Airspace Overlay Conflict** (where hardware-accelerated video windows render on top of WPF overlay graphics).

```
Windowed Mode:
┌───────────────────────────────┐
│ Parent WPF Window             │
│   ┌───────────────────────┐   │
│   │ Win32 Player HwndHost │   │
│   └───────────────────────┘   │
│   ┌───────────────────────┐   │
│   │ WPF Control Popup     │   │ <-- Floats on top via Popup HWND
│   └───────────────────────┘   │
└───────────────────────────────┘

Fullscreen Mode:
┌──────────────────────────────────────┐
│ WPF Fullscreen Overlay Window        │ <-- Owned, transparent window
│   ┌──────────────────────────────┐   │     sized to absolute screen area.
│   │ Fullscreen controls, sliders │   │     Dispatcher deferred focus routing.
│   └──────────────────────────────┘   │
└──────────────────────────────────────┘
```

1. **Dual Overlay Approach**: Uses a standard WPF `Popup` in windowed mode, and switches to a borderless, transparent owned WPF `Window` (`_fullscreenOverlayWindow`) in fullscreen mode.
2. **Absolute Resolution Scaling**: Coordinates are mapped using `PointToScreen` and divided by the system DPI, preventing layout clipping and ensuring buttons are fully clickable.
3. **Dispatcher Focus Guard**: Resolves window activation loops (where clicking a control hides the window) by deferring deactivation state evaluation via `Dispatcher.BeginInvoke`.
