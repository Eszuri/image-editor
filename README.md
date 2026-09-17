# Image Editor

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-lightgrey.svg)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-.NET%2010%20WPF-blue.svg)](https://dotnet.microsoft.com/)
[![UI Style](https://img.shields.io/badge/Design-Windows%2011%20Fluent-68217A.svg)](https://github.com/lepoco/wpfui)

A fast and clean native Windows image editor built with **C# (.NET 10 WPF)** and **Fluent Design**.

---

## Features

### 📐 Precision Bounded Crop
- Full-frame default selection with strict clamping to image bounds.
- Drag inside to move crop, drag outside to pan the zoomed view.
- Live dimension bar showing realtime crop size (`W × H px`).
- Retains unapplied crop selections when switching tools.

### 🖊️ Markups & Shapes
- Freehand sketching, straight lines, arrows, and measurement arrows.
- 16 curated colors with 1–30 px thickness slider.
- Snipping Tool-style flyout for pen options.

### 🗜️ Image Compression
- Preserves original resolution — no downscaling.
- Live size estimator (`Current → New Size (-XX%)`).
- Smooth compression slider (10%–100%).
- Auto format matching (PNG/JPEG) with proper alpha compositing.

### 🔄 Undo / Redo & Transforms
- Unified history covering markup strokes and image transforms (crop, rotate, flip).
- Rotate 90° (`R`), Flip Horizontal (`F`).

---

## Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Z / Y` | Undo / Redo |
| `V` | Cursor / Pan |
| `C` | Crop |
| `P` | Pen (click again for options) |
| `Ctrl + 0` | Fit to Viewport |
| `Ctrl + 1` | 100% Actual Size |
| `Enter` | Apply Crop |
| `Escape` | Cancel Crop |
