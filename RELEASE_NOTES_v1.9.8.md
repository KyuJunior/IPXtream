# IPXtream v1.9.8 Release Notes

Welcome to IPXtream **v1.9.8**! This release brings massive architectural improvements to offline content management, completely overhauls the visual browsing experience, and permanently crushes a few nasty UI behavior bugs.

## 🚀 What's New

### 1. Robust Offline Download Engine
You can now download your favorite Movies and Series episodes directly for offline viewing!
* **Zero-Interruption Downloads:** A brand new, HTTP-Stream binary download manager lives right in your dashboard tray.
* **Pause & Resume:** Downloads can be paused and perfectly resumed exactly where they left off, down to the exact byte offset!
* **Native Subtitles Capture:** We utilize raw MKV/MP4 stream captures, which mathematically guarantees that provider-embedded dual-audio and subtitle tracks are saved natively to your drive.

### 2. Complete Visual UI Overhaul
We completely stripped the old flat-list architecture in favor of a gorgeous, modern visual environment:
* **Poster-Grid Aesthetic:** Movies and Series now automatically parse into beautiful `UniformToFill` dynamic poster grids.
* **Hover Interactions:** Hovering any poster intelligently pulls down quick-access `▶ Play` and `⏬ Download` action buttons.
* **Season Tabs:** Navigating Series no longer dumps massive 400-episode unsorted lists. You now get a sleek horizontal **Season Tab Navigation** bar separating your content cleanly!

## 🛠 Bug Fixes & Improvements

* **WPF Fullscreen Airspace Fix:** We’ve entirely re-engineered how the mouse is tracked. Nasty DirectX "Airspace" layering meant fullscreen videos were swallowing your mouse movements and hiding the control HUD. We hooked directly into the global Windows `InputManager`, so your HUD will always flawlessly drop down instantly upon any mouse movement.
* **Subtitle "None" Feature Added:** The subtractions logic missed edge-cases where users just wanted subtitles off. A `(None)` toggle natively sends a stream detachment command straight to the rendering core.
* **Missing Series/Episode Posters Fixed:** Handled inconsistent API models where specific IPTV providers used `"cover"` and `"movie_image"` tags instead of standard identifiers. Your posters are now guaranteed to look right.

## 📦 How to Update
If you are already running an older version, IPXtream should flag this update automatically on startup! Just allow the installer to run over your existing installation.

---
_A huge thank you to the community for the ongoing feedback and support. Enjoy!_
