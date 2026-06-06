# IPXtream v1.9.0 — The Download Engine Update

## ⬇️ Full Download & Offline Support
IPXtream now features a built-in download engine! You can now download **Movies and Series** directly to your device for offline viewing.

* **Dedicated Downloader**: A fully managed, multi-threaded download queue capable of downloading large files efficiently.
* **Resume Capability**: Internet dropped? App closed? IPXtream supports download resumption gracefully. Cancel and resume later without losing your place.
* **Max Concurrent Downloads**: Queues automatically throttle to 2 simultaneous downloads to ensure stable performance and avoid provider rate limits. 
* **Custom Tray UI**: A brand-new slide-up Downloads Tray tracks file name, live progress bar, real-time MB/s speed, size info, and cancel control for everything in your queue. (Accessible from the sidebar and automatically opens when you start a download). 
* **Auto-saving**: Downloads land smartly sanitized in `%USERPROFILE%\Downloads\IPXtream\`. 
* **Note:** Downloads are explicitly disabled for Live TV feeds due to continuous streaming constraints.

## 📦 Enhanced "Deep Library Cache"
Building on previous stability updates, the "Cache Entire Library" mechanism is robust. Coupled with the new download feature, you can browse thumbnails offline and watch downloaded mp4s fully disconnected from the source servers. 

## 🔧 Polish & Under-the-hood Refinements
* Fixed the `Check for Updates` button binding bug the caused the updater interface to disable itself randomly.
* Optimized image rendering across stream cards for less memory usage and faster load times via disk-caching mechanisms and resolution downscaling.
* Numerous UI fixes adding cleaner active states to sidebar buttons and customized tracking badges for background processes.

---

### 📂 File Locations

| Type | Path |
|---|---|
| **Downloads** | `~\Downloads\IPXtream\` |
| **API JSON Cache** | `%LOCALAPPDATA%\IPXtream\Cache\` |
| **Thumbnail Disk Cache** | `%LOCALAPPDATA%\IPXtream\ImageCache\` |

---

### 📦 Installation Notes

- **OS:** Windows 10 x64 or later
- **.NET Runtime:** Self-contained — no separate install required
- **FFmpeg Engine:** Bundled natively (Flyleaf v3.10.x) — no separate codec installation required
- Existing installations can be upgraded in-place using the installer; credentials and existing cache are preserved!
