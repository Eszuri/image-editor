# Image Editor

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-lightgrey.svg)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-blue.svg)](https://dotnet.microsoft.com/)
[![UI Style](https://img.shields.io/badge/Design-Windows%2011%20Fluent-68217A.svg)](https://github.com/lepoco/wpfui)

A fast, clean, and intuitive native Windows desktop image editor built with **C# (.NET 8 WPF)** and **WPF-UI (Windows 11 Fluent Design)**. Image Editor provides a modern workspace for viewing, cropping, annotating, and transforming images with precision.

---

## App Overview

### 🗂️ Collapsible Sidebar
- Compact icon rail (48px) or expanded view with labels (145px).
- Automatically saves and restores sidebar state across sessions.

### 🖐️ Cursor & Canvas Navigation
- **Cursor / Pan Tool (`V`)**: Freely pan the image without modifying canvas content or crop regions.
- **Smooth Zoom**: Wheel-based zoom from 10% to 1000% with centered scaling and one-click reset.
- **Drag & Drop**: Drop images directly into the workspace to start editing immediately.

### 📐 Precision Bounded Crop
- **Full-Frame Default**: Opens with full image boundary selected.
- **Intelligent Panning**: Dragging inside moves the crop rectangle; dragging outside pans the zoomed view.
- **Strict Clamping**: Selection stays strictly within image bounds, preventing clipping or empty borders.
- **Live Dimension Bar**: Docked bottom status showing realtime crop width and height (`W × H px`) with quick Apply and Cancel.
- **State Preservation**: Retains unapplied crop selections when switching between tools.

### 🖊️ Markups & Shapes
- **Snipping Tool Flyout**: Click the pen tool while active to open the options flyout without cluttering the screen.
- **Multiple Drawing Modes**: Freehand sketching, straight lines, directional arrows, and double-ended measurement arrows.
- **16 Curated Colors**: Quick-pick color palette with active selection indicators.
- **Thickness Control**: 1–30 px slider with synchronized live preview dot and stroke line.

### 🔄 Global Undo / Redo
- Unified history manager covering both vector markup strokes and image transformations (crop, rotation, mirror flip).
- Standard shortcuts (`Ctrl+Z` / `Ctrl+Y`) with responsive navigation buttons.

### 🔄 Transformations
- **Rotate 90° Clockwise (`R`)**: Clean rotation maintaining original resolution and annotations.
- **Flip Horizontal (`F`)**: Instant mirror reflection.

### 🗜️ Compress Image (Resolution-Preserving File Size Reduction)
- **100% Original Resolution Preserved**: Retains the exact pixel dimensions (`Width × Height px`) without any downscaling or resampling.
- **Live Size Estimator**: Real-time calculated file size reduction display (`Current Size → New Size (-XX%)`) as the size slider moves smoothly from original down to smallest.
- **Proportional Size Slider**: Smooth compression slider (10%–100%) that sequentially scales file size from original down to the smallest size.
- **Automatic Format Matching**: Automatically matches and saves in the original file format (PNG or JPEG).
- **Clean Alpha Compositing**: Automatically composites transparent PNGs over solid white to prevent black backgrounds when compressing to JPEG.

---

## Shortcuts Reference

| Shortcut | Tool / Action |
| :--- | :--- |
| `Ctrl + O` | Open Image |
| `Ctrl + S` | Save Image |
| `Ctrl + Z` | Undo |
| `Ctrl + Y` | Redo |
| `V` | Cursor / Pan Tool |
| `C` | Crop Tool |
| `P` | Pen Tool (click again for options) |
| `R` | Rotate 90° Clockwise |
| `F` | Flip Horizontal |
| `Ctrl + 0` | Fit to Viewport |
| `Ctrl + 1` | 100% Actual Size |
| `Enter` | Apply Crop |
| `Escape` | Cancel Crop |
