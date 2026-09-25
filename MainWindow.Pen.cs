using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace ImageEditor
{
    public partial class MainWindow : FluentWindow
    {
        // Pen & Annotation state
        private bool _isPenActive;
        private StrokeShape _strokeShape = StrokeShape.Freehand;
        private Color _currentColor = (Color)ColorConverter.ConvertFromString("#0078D4");
        private double _currentThickness = 3.0;
        private bool _isDrawingShape;
        private Point _shapeStartPoint;

        public enum StrokeShape
        {
            Freehand,
            Line,
            Arrow,
            DoubleArrow
        }

        private static readonly string[] ColorPaletteHexes =
        {
            "FFEB3B", "FFC107", "FF9800", "E53935", "5E35B1", "8E24AA", "D81B60", "C2185B",
            "00B0FF", "0078D4", "1565C0", "7CB342", "2E7D32", "FFFFFF", "9E9E9E", "212121"
        };
        #region Pen & Markup Logic

        private void Cursor_Click(object sender, RoutedEventArgs e)
        {
            ActivateCursorMode();
        }

        public void ActivateCursorMode()
        {
            if (_isCropping)
            {
                ExitCropMode();
            }
            _isPenActive = false;
            PenSettingsPopup.IsOpen = false;
            PenBtn.Appearance = ControlAppearance.Secondary;
            CropBtn.Appearance = ControlAppearance.Secondary;
            CursorBtn.Appearance = ControlAppearance.Primary;
            MainInkCanvas.IsHitTestVisible = false;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
            MainInkCanvas.UseCustomCursor = false;
            MainInkCanvas.Cursor = null;
            HidePenCursor();
            ViewportGrid.Cursor = Cursors.Arrow;
        }

        private Point _lastPenCursorPos = new Point(-100, -100);

        private double GetCurrentZoomScale()
        {
            if (ImageMatrixTransform == null)
            {
                return 1.0;
            }
            double scale = ImageMatrixTransform.Matrix.M11;
            return (scale <= 0.0001) ? 1.0 : scale;
        }

        private double GetEffectiveCanvasThickness()
        {
            double scale = GetCurrentZoomScale();
            return Math.Clamp(_currentThickness / scale, 0.1, 50000.0);
        }

        private void UpdatePenCanvasThickness()
        {
            if (MainInkCanvas == null)
            {
                return;
            }
            double effective = GetEffectiveCanvasThickness();
            MainInkCanvas.DefaultDrawingAttributes.Width = effective;
            MainInkCanvas.DefaultDrawingAttributes.Height = effective;
            MainInkCanvas.DefaultDrawingAttributes.IgnorePressure = true;
        }

        private void UpdatePenCursor(Point viewPos)
        {
            _lastPenCursorPos = viewPos;
            if (!_isPenActive || _currentImage == null || PenCursorPreview == null ||
                ImageContainer == null || ViewportGrid == null)
            {
                HidePenCursor();
                return;
            }

            Point imgPos = ViewportGrid.TranslatePoint(viewPos, ImageContainer);
            if (imgPos.X < 0 || imgPos.X > _currentImage.PixelWidth ||
                imgPos.Y < 0 || imgPos.Y > _currentImage.PixelHeight)
            {
                HidePenCursor();
                return;
            }

            PenCursorPreview.Width = _currentThickness;
            PenCursorPreview.Height = _currentThickness;
            Canvas.SetLeft(PenCursorPreview, viewPos.X - _currentThickness / 2.0);
            Canvas.SetTop(PenCursorPreview, viewPos.Y - _currentThickness / 2.0);
            PenCursorPreview.Visibility = Visibility.Visible;

            if (MainInkCanvas != null)
            {
                MainInkCanvas.UseCustomCursor = true;
                MainInkCanvas.Cursor = Cursors.None;
            }
        }

        private void HidePenCursor()
        {
            if (PenCursorPreview != null)
            {
                PenCursorPreview.Visibility = Visibility.Collapsed;
            }
            if (MainInkCanvas != null && _isPenActive)
            {
                MainInkCanvas.Cursor = null;
            }
        }

        public void ActivatePenMode()
        {
            if (_isCropping)
            {
                ExitCropMode();
            }
            DeselectLayer();
            _isPenActive = true;
            PenBtn.Appearance = ControlAppearance.Primary;
            CursorBtn.Appearance = ControlAppearance.Secondary;
            CropBtn.Appearance = ControlAppearance.Secondary;
            ApplyPenModeToCanvas();
            UpdatePenPreview();
        }

        private void Pen_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            if (!_isPenActive)
            {
                ActivatePenMode();
                PenSettingsPopup.IsOpen = false;
            }
            else
            {
                PenSettingsPopup.IsOpen = !PenSettingsPopup.IsOpen;
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_isCropping)
            {
                ExitCropMode();
            }
            _historyManager.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_isCropping)
            {
                ExitCropMode();
            }
            _historyManager.Redo();
        }

        private void UpdateHistoryButtonStates()
        {
            if (UndoBtn == null || RedoBtn == null)
            {
                return;
            }
            UndoBtn.IsEnabled = _currentImage != null && _historyManager.CanUndo;
            RedoBtn.IsEnabled = _currentImage != null && _historyManager.CanRedo;
        }

        private void UpdatePenPreview()
        {
            if (PenThicknessPreviewDot != null)
            {
                PenThicknessPreviewDot.Width = _currentThickness;
                PenThicknessPreviewDot.Height = _currentThickness;
                PenThicknessPreviewDot.Fill = new SolidColorBrush(_currentColor);
            }
            if (PenThicknessPreviewLine != null)
            {
                PenThicknessPreviewLine.Stroke = new SolidColorBrush(_currentColor);
                PenThicknessPreviewLine.StrokeThickness = _currentThickness;
            }
            if (PenCursorPreview != null)
            {
                PenCursorPreview.Width = _currentThickness;
                PenCursorPreview.Height = _currentThickness;
                if (PenCursorPreview.Visibility == Visibility.Visible)
                {
                    Canvas.SetLeft(PenCursorPreview, _lastPenCursorPos.X - _currentThickness / 2.0);
                    Canvas.SetTop(PenCursorPreview, _lastPenCursorPos.Y - _currentThickness / 2.0);
                }
            }
            if (PenCursorPreviewFill != null)
            {
                PenCursorPreviewFill.Fill = new SolidColorBrush(
                    Color.FromArgb(0x55, _currentColor.R, _currentColor.G, _currentColor.B));
            }
        }

        public StrokeLayerItem AddStrokeLayer(Stroke stroke, StrokeShape shape, string? name = null, bool autoSelect = false)
        {
            if (_currentImage == null || stroke == null)
            {
                return null!;
            }

            double canvasW = _currentImage.PixelWidth;
            double canvasH = _currentImage.PixelHeight;

            // Clamp stylus points so stroke coordinates never exceed canvas dimensions
            stroke = ClampStrokeToCanvas(stroke, canvasW, canvasH);

            int maxZ = _layers.Count > 0 ? _layers.Max(x => x.ZIndex) : 0;
            int z = maxZ + 1;

            string layerName = !string.IsNullOrWhiteSpace(name) ? GetUniqueLayerName(name) : GetNextStrokeName(shape);

            var item = StrokeLayerItem.Create(stroke, shape, canvasW, canvasH, z, layerName);

            OverlayCanvas.Children.Add(item.HostCanvas);
            _layers.Add(item);

            _historyManager.Record(new AddLayerAction(this, item));

            if (autoSelect)
            {
                SelectLayerItem(item);
            }
            else
            {
                DeselectLayer();
            }

            UpdateLayerListUI();
            return item;
        }

        private void MainInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            MainInkCanvas.Strokes.Remove(e.Stroke);
            AddStrokeLayer(e.Stroke, StrokeShape.Freehand);
        }

        private void ApplyPenModeToCanvas()
        {
            if (_currentImage != null)
            {
                UpdateCanvasClips(_currentImage.PixelWidth, _currentImage.PixelHeight);
            }
            MainInkCanvas.IsHitTestVisible = true;
            MainInkCanvas.UseCustomCursor = true;
            MainInkCanvas.DefaultDrawingAttributes.Color = _currentColor;
            MainInkCanvas.DefaultDrawingAttributes.IgnorePressure = true;
            UpdatePenCanvasThickness();

            if (_strokeShape == StrokeShape.Freehand)
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
            else
            {
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThicknessValueText == null || MainInkCanvas == null)
            {
                return;
            }
            _currentThickness = Math.Round(e.NewValue);
            ThicknessValueText.Text = $"{_currentThickness} px";
            UpdatePenCanvasThickness();
            UpdatePenPreview();

            if (_appConfig != null)
            {
                _appConfig.PenThickness = _currentThickness;
                _appConfig.Save();
            }
        }

        private void ColorSwatch_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string hex)
            {
                SelectColor(hex);
            }
        }

        private void SelectColor(string hex)
        {
            _currentColor = (Color)ColorConverter.ConvertFromString(hex);
            if (PenColorIndicator != null)
            {
                PenColorIndicator.Background = new SolidColorBrush(_currentColor);
            }
            if (MainInkCanvas != null)
            {
                MainInkCanvas.DefaultDrawingAttributes.Color = _currentColor;
            }
            UpdateColorSelectionRing(hex);
            UpdatePenPreview();

            if (_appConfig != null)
            {
                _appConfig.PenColorHex = hex;
                _appConfig.Save();
            }
        }

        private void UpdateColorSelectionRing(string selectedHex)
        {
            string normalized = selectedHex.TrimStart('#').ToUpperInvariant();
            foreach (var h in ColorPaletteHexes)
            {
                if (FindName($"ColorBorder_{h}") is Border el)
                {
                    el.BorderBrush = (h == normalized) ? Brushes.White : Brushes.Transparent;
                }
            }
        }

        private void ShapeBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string tag && Enum.TryParse<StrokeShape>(tag, out var shape))
            {
                _strokeShape = shape;
                UpdateShapeButtonVisuals();
                ApplyPenModeToCanvas();

                if (_appConfig != null)
                {
                    _appConfig.PenShape = _strokeShape.ToString();
                    _appConfig.Save();
                }
            }
        }

        private void UpdateShapeButtonVisuals()
        {
            var activeBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            var inactiveBrush = Brushes.Transparent;

            ShapeFreehandBtn.Background = _strokeShape == StrokeShape.Freehand ? activeBrush : inactiveBrush;
            ShapeLineBtn.Background = _strokeShape == StrokeShape.Line ? activeBrush : inactiveBrush;
            ShapeArrowBtn.Background = _strokeShape == StrokeShape.Arrow ? activeBrush : inactiveBrush;
            ShapeDoubleArrowBtn.Background = _strokeShape == StrokeShape.DoubleArrow ? activeBrush : inactiveBrush;
        }

        private void MainInkCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Pan navigation: Right Click, Middle Click, or Space + Left Click
            bool isMiddleOrRight = e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed;
            bool isSpacePan = e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space);

            if (isMiddleOrRight || isSpacePan)
            {
                HidePenCursor();
                _isDrawingShape = false;
                _isPanning = true;
                _panStart = e.GetPosition(ViewportGrid);
                ViewportGrid.CaptureMouse();
                ViewportGrid.Cursor = Cursors.ScrollAll;
                e.Handled = true;
                return;
            }

            if (_isPenActive && _currentImage != null)
            {
                Point pos = e.GetPosition(MainInkCanvas);
                if (pos.X < 0 || pos.X > _currentImage.PixelWidth || pos.Y < 0 || pos.Y > _currentImage.PixelHeight)
                {
                    e.Handled = true;
                    return;
                }
            }

            if (_isPenActive && _strokeShape != StrokeShape.Freehand && e.LeftButton == MouseButtonState.Pressed)
            {
                _isDrawingShape = true;
                _shapeStartPoint = e.GetPosition(MainInkCanvas);
                MainInkCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainInkCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPenActive && _currentImage != null && !_isPanning)
            {
                UpdatePenCursor(e.GetPosition(ViewportGrid));
            }

            if (_isDrawingShape && e.LeftButton == MouseButtonState.Pressed && _currentImage != null)
            {
                Point current = e.GetPosition(MainInkCanvas);
                double clampedX = Math.Clamp(current.X, 0, _currentImage.PixelWidth);
                double clampedY = Math.Clamp(current.Y, 0, _currentImage.PixelHeight);
                RenderShapePreview(_shapeStartPoint, new Point(clampedX, clampedY));
                e.Handled = true;
            }
        }

        private void MainInkCanvas_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingShape)
            {
                _isDrawingShape = false;
                MainInkCanvas.ReleaseMouseCapture();
                ShapePreviewCanvas.Children.Clear();
                Point endPoint = e.GetPosition(MainInkCanvas);
                if (_currentImage != null)
                {
                    endPoint = new Point(
                        Math.Clamp(endPoint.X, 0, _currentImage.PixelWidth),
                        Math.Clamp(endPoint.Y, 0, _currentImage.PixelHeight));
                }

                if (Distance(_shapeStartPoint, endPoint) >= 2)
                {
                    double thickness = GetEffectiveCanvasThickness();
                    var stroke = CreateShapeStroke(_shapeStartPoint, endPoint, _strokeShape, _currentColor, thickness);
                    AddStrokeLayer(stroke, _strokeShape);
                }
                e.Handled = true;
            }
        }

        private BitmapSource GetComposedBitmap()
        {
            if (_currentImage == null)
            {
                return null!;
            }
            if (MainInkCanvas.Strokes.Count == 0 && _layers.Count == 0)
            {
                return _currentImage;
            }

            int w = _currentImage.PixelWidth;
            int h = _currentImage.PixelHeight;

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));

                dc.DrawImage(_currentImage, new Rect(0, 0, w, h));

                foreach (var layer in _layers.OrderBy(x => x.ZIndex))
                {
                    if (!layer.IsVisible)
                    {
                        continue;
                    }
                    if (layer.Opacity < 1.0)
                    {
                        dc.PushOpacity(layer.Opacity);
                    }
                    layer.RenderTo(dc);
                    if (layer.Opacity < 1.0)
                    {
                        dc.Pop();
                    }
                }

                if (MainInkCanvas.Strokes.Count > 0)
                {
                    MainInkCanvas.Strokes.Draw(dc);
                }

                dc.Pop();
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        #endregion
    }
}