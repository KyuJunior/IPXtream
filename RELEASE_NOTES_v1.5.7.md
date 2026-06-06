# IPXtream v1.5.7 — What's New

## 📦 Deep Library Cache

The **Cache Entire Library** feature has been completely overhauled. It now truly caches everything — not just the API JSON, but also every thumbnail image and every series episode list — so the app can run fully offline or load your entire library at maximum speed on the next launch.

### How it works

Caching now runs in three sequential phases, with a live counter in the status bar:

| Phase | What it caches |
|---|---|
| **1 / 3 — Stream lists** | Live TV, Movies, and Series JSON responses (same as before) |
| **2 / 3 — Thumbnails** | Every channel/movie/series thumbnail image, downloaded to a local `ImageCache` folder — up to 8 concurrent downloads |
| **3 / 3 — Series episodes** | Full episode tree (`get_series_info`) for every series in your library — up to 4 concurrent fetches |

**Example status bar output while caching:**
```
Caching (2/3) — Thumbnails 4,821 / 12,350
Caching (3/3) — Series episodes 214 / 930
✔ Cache complete — 42,830 streams · 12,350 images · 930 series
```

---

## ⚡ Instant Thumbnail Loading

After caching, thumbnails load from disk instead of the network. The app checks the local image cache first on every scroll — if the image is already on disk it loads instantly with no network hit. Streams that haven't been cached yet continue to download from the server as usual.

---

## ⏹ Cancel Button

A new **Cancel Caching** button appears in the sidebar while a cache operation is in progress. Clicking it gracefully stops all in-flight downloads and restores the sidebar to its normal state.

---

## 🐛 Bug Fixes

- Fixed: "Cache Entire Library" button remained active (grayed out) during caching instead of showing a cancel option
- Fixed: Series episodes were never cached — clicking a series always required a network fetch even after "Cache All"
- Fixed: Thumbnails had to re-download from the network on every app launch, slowing down scrolling

---

## 📂 Cache Locations

| Cache type | Location |
|---|---|
| API JSON responses | `%LOCALAPPDATA%\IPXtream\Cache\` |
| Thumbnail images | `%LOCALAPPDATA%\IPXtream\ImageCache\` |

---

## 📦 Installation Notes

- **OS:** Windows 10 x64 or later
- **.NET Runtime:** Self-contained — no separate install required
- **FFmpeg:** Bundled — no separate codec install required
- Existing installations can be upgraded in-place; credentials and existing cache are preserved
