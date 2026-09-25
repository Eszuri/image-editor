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
        // Crop state
        private bool _isCropping;
        private Rect _cropRect = Rect.Empty;
        private Rect _cropRectStart = Rect.Empty;
        private Point _dragStart;
        private DragMode _dragMode = DragMode.None;
        private Rect? _savedUnappliedCropRect;
        private OverlayImageItem? _croppingOverlayItem;

        private enum DragMode
        {
            None,
            Pan,
            Move,
            ResizeTL,
            ResizeTR,
            ResizeBR,
            ResizeBL,
            ResizeT,
            ResizeB,
            ResizeL,
            ResizeR
        }
        #region Crop Logic

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            if (_isCropping)
            {
                ExitCropMode();
                ActivateCursorMode();
            }
            else
            {
                EnterCropMode();
            }
        }

        private void EnterCropMode()
        {
            if (_currentImage == null)
            {
                return;
            }
            ActivateCursorMode();

            _croppingOverlayItem = null;
            _isCropping = true;
            _dragMode = DragMode.None;
            CropCanvas.Visibility = Visibility.Visible;
            CropBottomBar.Visibility = Visibility.Visible;
            CropBtn.Appearance = ControlAppearance.Primary;
            CursorBtn.Appearance = ControlAppearance.Secondary;

            if (_savedUnappliedCropRect.HasValue &&
                _savedUnappliedCropRect.Value.Width >= 2 &&
                _savedUnappliedCropRect.Value.Height >= 2)
            {
                var r = _savedUnappliedCropRect.Value;
                double w = Math.Min(r.Width, _currentImage.PixelWidth);
                double h = Math.Min(r.Height, _currentImage.PixelHeight);
                double x = Math.Clamp(r.X, 0, Math.Max(0, _currentImage.PixelWidth - w));
                double y = Math.Clamp(r.Y, 0, Math.Max(0, _currentImage.PixelHeight - h));
                _cropRect = new Rect(x, y, w, h);
            }
            else
            {
                _cropRect = new Rect(0, 0, _currentImage.PixelWidth, _currentImage.PixelHeight);
            }

            UpdateCropVisuals();
        }

        public void StartCropOverlayImage(OverlayImageItem item)
        {
            if (_currentImage == null || item == null || item.IsLocked)
            {
                return;
            }
            ActivateCursorMode();

            _croppingOverlayItem = item;
            _isCropping = true;
            _dragMode = DragMode.None;
            CropCanvas.Visibility = Visibility.Visible;
            CropBottomBar.Visibility = Visibility.Visible;
            CropBtn.Appearance = ControlAppearance.Primary;
            CursorBtn.Appearance = ControlAppearance.Secondary;

            _cropRect = new Rect(item.X, item.Y, item.Width, item.Height);
            UpdateCropVisuals();
        }

        private void ExitCropMode()
        {
            if (_isCropping && !_cropRect.IsEmpty && _cropRect.Width >= 2 && _cropRect.Height >= 2 && _croppingOverlayItem == null)
            {
                _savedUnappliedCropRect = _cropRect;
            }
            _isCropping = false;
            _croppingOverlayItem = null;
            _dragMode = DragMode.None;
            CropCanvas.Visibility = Visibility.Collapsed;
            CropBottomBar.Visibility = Visibility.Collapsed;
            CropCanvas.ReleaseMouseCapture();
            CropCanvas.Cursor = Cursors.Arrow;
            CropBtn.Appearance = ControlAppearance.Secondary;
        }

        private void UpdateCropVisuals()
        {
            if (_currentImage == null || _cropRect.IsEmpty)
            {
                return;
            }

            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            double currentScale = CurrentScale;
            double invScale = 1.0 / Math.Max(0.001, currentScale);

            Canvas.SetLeft(CropBoxBorder, _cropRect.X);
            Canvas.SetTop(CropBoxBorder, _cropRect.Y);
            CropBoxBorder.Width = Math.Max(1, _cropRect.Width);
            CropBoxBorder.Height = Math.Max(1, _cropRect.Height);
            CropBoxBorder.BorderThickness = new Thickness(Math.Max(1.0, 2.0 * invScale));

            GridLineH.BorderThickness = new Thickness(0, Math.Max(0.5, 1.0 * invScale), 0, Math.Max(0.5, 1.0 * invScale));
            GridLineV.BorderThickness = new Thickness(Math.Max(0.5, 1.0 * invScale), 0, Math.Max(0.5, 1.0 * invScale), 0);

            var handleScale = new ScaleTransform(invScale, invScale);
            HandleTL.RenderTransform = HandleTR.RenderTransform = HandleBR.RenderTransform = HandleBL.RenderTransform =
                HandleT.RenderTransform = HandleB.RenderTransform = HandleL.RenderTransform = HandleR.RenderTransform = handleScale;

            Canvas.SetLeft(HandleTL, _cropRect.X - 7);
            Canvas.SetTop(HandleTL, _cropRect.Y - 7);

            Canvas.SetLeft(HandleTR, _cropRect.Right - 7);
            Canvas.SetTop(HandleTR, _cropRect.Y - 7);

            Canvas.SetLeft(HandleBR, _cropRect.Right - 7);
            Canvas.SetTop(HandleBR, _cropRect.Bottom - 7);

            Canvas.SetLeft(HandleBL, _cropRect.X - 7);
            Canvas.SetTop(HandleBL, _cropRect.Bottom - 7);

            Canvas.SetLeft(HandleT, _cropRect.X + (_cropRect.Width / 2.0) - 14);
            Canvas.SetTop(HandleT, _cropRect.Y - 5);

            Canvas.SetLeft(HandleB, _cropRect.X + (_cropRect.Width / 2.0) - 14);
            Canvas.SetTop(HandleB, _cropRect.Bottom - 5);

            Canvas.SetLeft(HandleL, _cropRect.X - 5);
            Canvas.SetTop(HandleL, _cropRect.Y + (_cropRect.Height / 2.0) - 14);

            Canvas.SetLeft(HandleR, _cropRect.Right - 5);
            Canvas.SetTop(HandleR, _cropRect.Y + (_cropRect.Height / 2.0) - 14);

            Canvas.SetLeft(MaskTop, 0);
            Canvas.SetTop(MaskTop, 0);
            MaskTop.Width = imgW;
            MaskTop.Height = Math.Max(0, _cropRect.Y);

            Canvas.SetLeft(MaskBottom, 0);
            Canvas.SetTop(MaskBottom, _cropRect.Bottom);
            MaskBottom.Width = imgW;
            MaskBottom.Height = Math.Max(0, imgH - _cropRect.Bottom);

            Canvas.SetLeft(MaskLeft, 0);
            Canvas.SetTop(MaskLeft, _cropRect.Y);
            MaskLeft.Width = Math.Max(0, _cropRect.X);
            MaskLeft.Height = Math.Max(0, _cropRect.Height);

            Canvas.SetLeft(MaskRight, _cropRect.Right);
            Canvas.SetTop(MaskRight, _cropRect.Y);
            MaskRight.Width = Math.Max(0, imgW - _cropRect.Right);
            MaskRight.Height = Math.Max(0, _cropRect.Height);

            if (_croppingOverlayItem != null)
            {
                var src = _croppingOverlayItem.Source;
                double scaleX = (double)src.PixelWidth / Math.Max(1.0, _croppingOverlayItem.Width);
                double scaleY = (double)src.PixelHeight / Math.Max(1.0, _croppingOverlayItem.Height);

                int pw = Math.Max(1, (int)Math.Round(_cropRect.Width * scaleX));
                int ph = Math.Max(1, (int)Math.Round(_cropRect.Height * scaleY));

                CropDimensionsText.Text = $"{pw} × {ph} px";
                CropStatusText.Text =
                    $"|  Layer: '{_croppingOverlayItem.Name}'  " +
                    $"|  Crop: {(int)Math.Round(_cropRect.Width)} × {(int)Math.Round(_cropRect.Height)}  " +
                    $"|  Original: {src.PixelWidth} × {src.PixelHeight} px";
            }
            else
            {
                int pw = Math.Max(1, (int)Math.Round(_cropRect.Width));
                int ph = Math.Max(1, (int)Math.Round(_cropRect.Height));
                int px = Math.Clamp((int)Math.Round(_cropRect.X), 0, (int)imgW - 1);
                int py = Math.Clamp((int)Math.Round(_cropRect.Y), 0, (int)imgH - 1);

                CropDimensionsText.Text = $"{pw} × {ph} px";
                CropStatusText.Text =
                    $"|  Position: ({px}, {py})  " +
                    $"|  Original: {(int)imgW} × {(int)imgH} px  " +
                    $"|  Zoom: {(int)Math.Round(currentScale * 100)}%";
            }
        }

        private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isCropping || _currentImage == null)
            {
                return;
            }

            // Pan triggers: Middle click, Right click, or Space + Left click
            bool isMiddleOrRight = e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed;
            bool isSpacePan = e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space);

            if (isMiddleOrRight || isSpacePan)
            {
                _dragMode = DragMode.Pan;
                _panStart = e.GetPosition(ViewportGrid);
                CropCanvas.CaptureMouse();
                CropCanvas.Cursor = Cursors.ScrollAll;
                e.Handled = true;
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point pt = e.GetPosition(CropCanvas);
            _dragStart = pt;
            _cropRectStart = _cropRect;

            double invScale = 1.0 / Math.Max(0.001, CurrentScale);
            double cornerThreshold = 24.0 * invScale;
            double edgeThreshold = 18.0 * invScale;

            if (Distance(pt, new Point(_cropRect.X, _cropRect.Y)) <= cornerThreshold)
            {
                _dragMode = DragMode.ResizeTL;
            }
            else if (Distance(pt, new Point(_cropRect.Right, _cropRect.Y)) <= cornerThreshold)
            {
                _dragMode = DragMode.ResizeTR;
            }
            else if (Distance(pt, new Point(_cropRect.Right, _cropRect.Bottom)) <= cornerThreshold)
            {
                _dragMode = DragMode.ResizeBR;
            }
            else if (Distance(pt, new Point(_cropRect.X, _cropRect.Bottom)) <= cornerThreshold)
            {
                _dragMode = DragMode.ResizeBL;
            }
            else if (Math.Abs(pt.Y - _cropRect.Y) <= edgeThreshold &&
                     pt.X >= _cropRect.X - edgeThreshold &&
                     pt.X <= _cropRect.Right + edgeThreshold)
            {
                _dragMode = DragMode.ResizeT;
            }
            else if (Math.Abs(pt.Y - _cropRect.Bottom) <= edgeThreshold &&
                     pt.X >= _cropRect.X - edgeThreshold &&
                     pt.X <= _cropRect.Right + edgeThreshold)
            {
                _dragMode = DragMode.ResizeB;
            }
            else if (Math.Abs(pt.X - _cropRect.X) <= edgeThreshold &&
                     pt.Y >= _cropRect.Y - edgeThreshold &&
                     pt.Y <= _cropRect.Bottom + edgeThreshold)
            {
                _dragMode = DragMode.ResizeL;
            }
            else if (Math.Abs(pt.X - _cropRect.Right) <= edgeThreshold &&
                     pt.Y >= _cropRect.Y - edgeThreshold &&
                     pt.Y <= _cropRect.Bottom + edgeThreshold)
            {
                _dragMode = DragMode.ResizeR;
            }
            else if (_cropRect.Contains(pt))
            {
                _dragMode = DragMode.Move;
                CropCanvas.Cursor = Cursors.SizeAll;
            }
            else
            {
                _dragMode = DragMode.Pan;
                _panStart = e.GetPosition(ViewportGrid);
                CropCanvas.Cursor = Cursors.ScrollAll;
            }

            CropCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isCropping || _currentImage == null)
            {
                return;
            }

            Point rawPt = e.GetPosition(CropCanvas);
            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            double minX = _croppingOverlayItem != null ? _croppingOverlayItem.X : 0;
            double maxX = _croppingOverlayItem != null ? _croppingOverlayItem.X + _croppingOverlayItem.Width : imgW;
            double minY = _croppingOverlayItem != null ? _croppingOverlayItem.Y : 0;
            double maxY = _croppingOverlayItem != null ? _croppingOverlayItem.Y + _croppingOverlayItem.Height : imgH;

            // Clamped position strictly inside bounds
            Point pt = new Point(Math.Clamp(rawPt.X, minX, maxX), Math.Clamp(rawPt.Y, minY, maxY));

            // 1. If currently panning the view
            if (_dragMode == DragMode.Pan)
            {
                Point currentViewport = e.GetPosition(ViewportGrid);
                double dx = currentViewport.X - _panStart.X;
                double dy = currentViewport.Y - _panStart.Y;
                _panStart = currentViewport;

                Matrix m = ImageMatrixTransform.Matrix;
                m.Translate(dx, dy);
                ImageMatrixTransform.Matrix = m;
                e.Handled = true;
                return;
            }

            // 2. Hover cursor when idle
            if (_dragMode == DragMode.None)
            {
                if (Keyboard.IsKeyDown(Key.Space))
                {
                    CropCanvas.Cursor = Cursors.ScrollAll;
                    return;
                }

                double invScale = 1.0 / Math.Max(0.001, CurrentScale);
                double cornerThreshold = 24.0 * invScale;
                double edgeThreshold = 18.0 * invScale;

                if (Distance(rawPt, new Point(_cropRect.X, _cropRect.Y)) <= cornerThreshold ||
                    Distance(rawPt, new Point(_cropRect.Right, _cropRect.Bottom)) <= cornerThreshold)
                {
                    CropCanvas.Cursor = Cursors.SizeNWSE;
                }
                else if (Distance(rawPt, new Point(_cropRect.Right, _cropRect.Y)) <= cornerThreshold ||
                         Distance(rawPt, new Point(_cropRect.X, _cropRect.Bottom)) <= cornerThreshold)
                {
                    CropCanvas.Cursor = Cursors.SizeNESW;
                }
                else if ((Math.Abs(rawPt.Y - _cropRect.Y) <= edgeThreshold ||
                          Math.Abs(rawPt.Y - _cropRect.Bottom) <= edgeThreshold) &&
                         rawPt.X >= _cropRect.X - edgeThreshold &&
                         rawPt.X <= _cropRect.Right + edgeThreshold)
                {
                    CropCanvas.Cursor = Cursors.SizeNS;
                }
                else if ((Math.Abs(rawPt.X - _cropRect.X) <= edgeThreshold ||
                          Math.Abs(rawPt.X - _cropRect.Right) <= edgeThreshold) &&
                         rawPt.Y >= _cropRect.Y - edgeThreshold &&
                         rawPt.Y <= _cropRect.Bottom + edgeThreshold)
                {
                    CropCanvas.Cursor = Cursors.SizeWE;
                }
                // Inside crop: cursor to move crop
                else if (_cropRect.Contains(rawPt))
                {
                    CropCanvas.Cursor = Cursors.SizeAll;
                }
                // Outside crop: cursor to pan view
                else
                {
                    CropCanvas.Cursor = Cursors.ScrollAll;
                }
                return;
            }

            switch (_dragMode)
            {
                // Inside crop: move crop box (not pan view)
                case DragMode.Move:
                    double dx = rawPt.X - _dragStart.X;
                    double dy = rawPt.Y - _dragStart.Y;
                    double nx = Math.Clamp(_cropRectStart.X + dx, minX, Math.Max(minX, maxX - _cropRectStart.Width));
                    double ny = Math.Clamp(_cropRectStart.Y + dy, minY, Math.Max(minY, maxY - _cropRectStart.Height));
                    _cropRect = new Rect(nx, ny, _cropRectStart.Width, _cropRectStart.Height);
                    break;

                // 4 Corners: Free 2D dragging, strictly bounded
                case DragMode.ResizeTL:
                    double leftTL = Math.Clamp(pt.X, minX, _cropRectStart.Right - 10);
                    double topTL = Math.Clamp(pt.Y, minY, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(
                        leftTL,
                        topTL,
                        _cropRectStart.Right - leftTL,
                        _cropRectStart.Bottom - topTL);
                    break;

                case DragMode.ResizeTR:
                    double rightTR = Math.Clamp(pt.X, _cropRectStart.X + 10, maxX);
                    double topTR = Math.Clamp(pt.Y, minY, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(
                        _cropRectStart.X,
                        topTR,
                        rightTR - _cropRectStart.X,
                        _cropRectStart.Bottom - topTR);
                    break;

                case DragMode.ResizeBR:
                    double rightBR = Math.Clamp(pt.X, _cropRectStart.X + 10, maxX);
                    double bottomBR = Math.Clamp(pt.Y, _cropRectStart.Y + 10, maxY);
                    _cropRect = new Rect(
                        _cropRectStart.X,
                        _cropRectStart.Y,
                        rightBR - _cropRectStart.X,
                        bottomBR - _cropRectStart.Y);
                    break;

                case DragMode.ResizeBL:
                    double leftBL = Math.Clamp(pt.X, minX, _cropRectStart.Right - 10);
                    double bottomBL = Math.Clamp(pt.Y, _cropRectStart.Y + 10, maxY);
                    _cropRect = new Rect(
                        leftBL,
                        _cropRectStart.Y,
                        _cropRectStart.Right - leftBL,
                        bottomBL - _cropRectStart.Y);
                    break;

                // 4 Side Edges: 1D free dragging, strictly bounded
                case DragMode.ResizeT:
                    double topT = Math.Clamp(pt.Y, minY, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(_cropRectStart.X, topT, _cropRectStart.Width, _cropRectStart.Bottom - topT);
                    break;

                case DragMode.ResizeB:
                    double bottomB = Math.Clamp(pt.Y, _cropRectStart.Y + 10, maxY);
                    _cropRect = new Rect(_cropRectStart.X, _cropRectStart.Y, _cropRectStart.Width, bottomB - _cropRectStart.Y);
                    break;

                case DragMode.ResizeL:
                    double leftL = Math.Clamp(pt.X, minX, _cropRectStart.Right - 10);
                    _cropRect = new Rect(leftL, _cropRectStart.Y, _cropRectStart.Right - leftL, _cropRectStart.Height);
                    break;

                case DragMode.ResizeR:
                    double rightR = Math.Clamp(pt.X, _cropRectStart.X + 10, maxX);
                    _cropRect = new Rect(_cropRectStart.X, _cropRectStart.Y, rightR - _cropRectStart.X, _cropRectStart.Height);
                    break;
            }

            UpdateCropVisuals();
            e.Handled = true;
        }

        private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragMode != DragMode.None)
            {
                _dragMode = DragMode.None;
                CropCanvas.ReleaseMouseCapture();
                CropCanvas.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void CropCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragMode != DragMode.None)
            {
                _dragMode = DragMode.None;
                CropCanvas.Cursor = Cursors.Arrow;
            }
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void ApplyCrop_Click(object sender, RoutedEventArgs e)
        {
            if (_cropRect.IsEmpty || _cropRect.Width < 2 || _cropRect.Height < 2)
            {
                return;
            }

            if (_croppingOverlayItem != null)
            {
                var target = _croppingOverlayItem;
                var oldBmp = target.Source;
                var oldRect = new Rect(target.X, target.Y, target.Width, target.Height);

                double scaleX = (double)oldBmp.PixelWidth / Math.Max(1.0, target.Width);
                double scaleY = (double)oldBmp.PixelHeight / Math.Max(1.0, target.Height);

                double relX = _cropRect.X - target.X;
                double relY = _cropRect.Y - target.Y;

                int px = Math.Clamp((int)Math.Round(relX * scaleX), 0, oldBmp.PixelWidth - 1);
                int py = Math.Clamp((int)Math.Round(relY * scaleY), 0, oldBmp.PixelHeight - 1);
                int pw = Math.Clamp((int)Math.Round(_cropRect.Width * scaleX), 1, oldBmp.PixelWidth - px);
                int ph = Math.Clamp((int)Math.Round(_cropRect.Height * scaleY), 1, oldBmp.PixelHeight - py);

                var newImage = new CroppedBitmap(oldBmp, new Int32Rect(px, py, pw, ph));
                if (newImage.CanFreeze)
                {
                    newImage.Freeze();
                }
                var newRect = new Rect(_cropRect.X, _cropRect.Y, _cropRect.Width, _cropRect.Height);

                ExitCropMode();

                InternalCropLayer(target, newImage, newRect);
                _historyManager.Record(new CropLayerImageAction(this, target, oldBmp, oldRect, newImage, newRect));
                return;
            }

            if (_currentImage == null)
            {
                return;
            }

            var (oldImage, oldStrokes, baseSource) = GetTransformBase();

            int pxBase = Math.Clamp((int)Math.Round(_cropRect.X), 0, baseSource.PixelWidth - 1);
            int pyBase = Math.Clamp((int)Math.Round(_cropRect.Y), 0, baseSource.PixelHeight - 1);
            int pwBase = Math.Clamp((int)Math.Round(_cropRect.Width), 1, baseSource.PixelWidth - pxBase);
            int phBase = Math.Clamp((int)Math.Round(_cropRect.Height), 1, baseSource.PixelHeight - pyBase);

            var newCanvasImage = new CroppedBitmap(baseSource, new Int32Rect(pxBase, pyBase, pwBase, phBase));
            if (newCanvasImage.CanFreeze)
            {
                newCanvasImage.Freeze();
            }

            ExitCropMode();
            _savedUnappliedCropRect = null;
            ApplyImageTransform(oldImage, oldStrokes, newCanvasImage, Array.Empty<Stroke>());
        }

        private void CancelCrop_Click(object sender, RoutedEventArgs e)
        {
            ExitCropMode();
            _savedUnappliedCropRect = null;
            ActivateCursorMode();
        }

        #endregion
    }
}