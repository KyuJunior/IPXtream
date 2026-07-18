---
name: wpf-mvvm-and-styling
description: WPF & XAML Styling Optimization and MVVM pattern rules for IPXtream
---

# wpf-mvvm-and-styling

This skill provides guidelines and patterns for styling, layout, custom converters, data binding, glassmorphism, responsive grids, and design themes within the IPXtream WPF (.NET 8) desktop client.

## Core Guidelines

### 1. Strict MVVM Separation
* **View Responsibility**: Views should only handle UI rendering, visual effects (e.g., glassmorphism, transitions), and event triggers that are purely aesthetic. Avoid placing business logic in code-behind.
* **ViewModel Responsibility**: Use standard MVVM Community Toolkit patterns (or standard `INotifyPropertyChanged`) to manage active state, list items, and asynchronous commands.
* **No Direct View Coupling**: ViewModels must never hold references to UI Controls or elements. Communication from ViewModel to View should happen via bindings or decoupled messaging (e.g., `WeakReferenceMessenger`).

### 2. UI Virtualization & List Optimization
IPXtream deals with huge lists of IPTV streams (channels, movies, episodes). Standard WPF layouts will freeze if thousands of items are loaded at once.
* **Use `VirtualizingStackPanel`**: Always ensure that list-rendering controls (like `ListBox`, `ListView`, or `DataGrid`) have virtualization turned on:
  ```xml
  VirtualizingStackPanel.IsVirtualizing="True"
  VirtualizingStackPanel.VirtualizationMode="Recycling"
  ScrollViewer.CanContentScroll="True"
  ```
* **Avoid Height Auto-Sizing inside ScrollViewers**: Setting a ListBox's height to auto inside a parent ScrollViewer will break virtualization by measuring the entire height of all items. Let the control scroll itself.

### 3. Themes and DynamicResource Management
* **DynamicResource vs. StaticResource**: Use `DynamicResource` for colors, brushes, and styles that change at runtime (e.g., switching between dark mode, light mode, or custom color accents via `ThemeHelper`). Use `StaticResource` only for non-volatile assets (like geometry templates, static thickness, etc.).
* **Dynamic Theme Injection**: Remember that themes are managed dynamically in [ThemeHelper.cs](file:///c:/Myapps/ipxtream/IPXtream/Helpers/ThemeHelper.cs). Ensure any new custom theme resources are registered in the main `App.xaml` or injected dictionary.

### 4. Custom Converters
* Custom converters (like [SectionToStyleConverter.cs](file:///c:/Myapps/ipxtream/IPXtream/Helpers/SectionToStyleConverter.cs)) convert states/enums directly to styles or brushes.
* **Reliability Check**: Ensure converters are case-insensitive and handle null or unexpected values gracefully to avoid silent UI failures or application crashes.

### 5. Responsive Grids & Design
* Use `Grid` with proportional star (`*`) and auto sizing for responsive layouts.
* Implement hover states, opacity animations, and glassmorphic backgrounds using blur effects to preserve the high-end premium media-center feel.
