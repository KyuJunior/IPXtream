---
name: media-player-lifecycle
description: Media Player Lifecycle (VLC & Flyleaf) & Hardware Acceleration optimization rules
---

# media-player-lifecycle

This skill provides guidelines and patterns for managing the lifecycle of Flyleaf/VLC media player integrations, handling hardware acceleration fallbacks, preventing memory leaks, and managing viewport aspect ratios and fullscreen controls.

## Core Guidelines

### 1. Explicit Resource Disposal
* **Unmanaged DLL Hooks**: Media engines like Flyleaf and LibVLC rely heavily on unmanaged C/C++ libraries. If these instances are not explicitly disposed of when navigating away, they can cause major memory leaks or silent app crashes.
* **Implement `IDisposable`**: Views or controls hosting the media player must implement and call `.Dispose()` on the players and their underlying audio/video contexts.
* **Navigation Cleanup**: Hook into the page or window's `Unloaded` events, or page navigation handlers, to stop playback, detach event hooks, and release player handles:
  ```csharp
  // Example cleanup pattern
  player?.Stop();
  player?.Dispose();
  player = null;
  ```

### 2. Hardware Acceleration Toggle & Fallback
* **HW Acceleration Preferences**: Enable DXVA2, D3D11VA, or CUDA hardware acceleration whenever possible to keep CPU utilization minimal.
* **Failover Logic**: Implement safety checks or catch rendering exceptions. If hardware acceleration initialization fails (e.g., due to unsupported graphics drivers), fall back gracefully to software rendering to ensure playback still functions.
* **User Configurable Toggles**: Allow users to enable/disable hardware acceleration inside settings, modifying dynamic config properties that load upon player initialization.

### 3. Aspect Ratio & Fullscreen Control
* **Aspect Ratio Handling**: Compute aspect ratios correctly. Provide layout strategies (such as Uniform, UniformToFill, Stretch) and let the player control handle scaling bounds in WPF.
* **Smooth Fullscreen Toggling**: 
  * Avoid tearing down the player instance when toggling fullscreen.
  * Move the player host control to a fullscreen container or change window properties (`WindowStyle.None`, `ResizeMode.NoResize`, `WindowState.Maximized`) dynamically to ensure seamless transitions.
  * Ensure keyboard shortcuts (e.g., double click, Escape key) are mapped to exit fullscreen.
