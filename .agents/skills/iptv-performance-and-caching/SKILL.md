---
name: iptv-performance-and-caching
description: High-Performance IPTV Parsing & Caching guidelines for Xtream API and large datasets
---

# iptv-performance-and-caching

This skill outlines strategies for handling large IPTV datasets, parsing JSON or M3U streams efficiently, implementing thread-safe local caches, and displaying progress to the user asynchronously.

## Core Guidelines

### 1. Asynchronous & Streaming Parsing
* **Avoid Synchronous Parsing**: IPTV playlists (especially M3U files or Xtream Codes API listings) can contain tens of thousands of items. Never load them entirely into memory synchronously.
* **System.Text.Json Deserialization**: Use modern `System.Text.Json` features to deserialize responses asynchronously and utilize streaming (`Stream` or `IAsyncEnumerable<T>`) when reading large datasets.
* **Garbage Collection (GC) Optimization**: Avoid allocating unnecessary intermediate collections. Process items on-the-fly and map them directly to UI models or localized storage.

### 2. Thread-Safe Caching Patterns
* **Avoid Repeated API Calls**: IPTV servers can easily block clients that query the server too frequently. Implement caching for categories, live streams, VOD playlists, and EPG (Electronic Program Guide) data.
* **Memory Cache**: Use thread-safe caching structures like `MemoryCache` or `ConcurrentDictionary<string, CacheEntry>` to cache API responses.
* **Cache Expiry (TTL)**: Define clear Time-To-Live parameters (e.g., categories cached for 24 hours, EPG for 4 hours, and current channel stream URLs for short durations).

### 3. UI Progress Reporting
* **Use `IProgress<T>`**: When fetching and parsing extensive IPTV playlists in the background, report progress back to the UI thread using standard C# progress patterns.
* **Throttle Progress Updates**: Do not update the progress bar on every single parsed item (which floods the Dispatcher thread and locks the UI). Group progress updates (e.g., update every 100 items or 1% increments).
* **Graceful Cancellation**: Support cancellation via `CancellationToken` for all network requests so that users can stop long-running fetches if they choose.
