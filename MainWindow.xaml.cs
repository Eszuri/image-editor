using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ImageEditor
{
    public partial class MainWindow : FluentWindow
    {
        private BitmapSource? _currentImage;
        private string? _currentPath;

        // Panning state
        private bool _isPanning;
        private Point _panStart;
        private bool _isManualZoom;

        // Crop state
        private bool _isCropping;
        private Rect _cropRect = Rect.Empty;
        private Rect _cropRectStart = Rect.Empty;
        private Point _dragStart;
        private DragMode _dragMode = DragMode.None;
        private Rect? _savedUnappliedCropRect;

        // Pen & Annotation state
        private bool _isPenActive;
        private StrokeShape _strokeShape = StrokeShape.Freehand;
        private Color _currentColor = (Color)ColorConverter.ConvertFromString("#0078D4");
        private double _currentThickness = 3.0;
        private bool _isDrawingShape;
        private Point _shapeStartPoint;

        // App Config & Sidebar State
        private AppConfig _appConfig = new();
        private bool _isSidebarCollapsed;

        // Global History Manager
        private readonly HistoryManager _historyManager = new();

        // Compression state
        private string _compressFormat = "jpg";
        private long _originalFileSize;
        private byte[]? _lastCompressedData;
        private int _lastCompressedQuality = -1;
        private string _lastCompressedFormat = "";

        // Batch Compression state
        private readonly ObservableCollection<BatchItem> _batchItems = new();
        private CancellationTokenSource? _batchCts;
        private bool _isBatchProcessing;
        private string? _lastBatchOutputFolder;
        private static readonly HashSet<string> SupportedBatchExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".webp"
        };

        public enum StrokeShape
        {
            Freehand,
            Line,
            Arrow,
            DoubleArrow
        }

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

        private double CurrentScale => ImageMatrixTransform.Matrix.M11 > 0.0001 ? ImageMatrixTransform.Matrix.M11 : 1.0;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            try
            {
                Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.png"));
            }
            catch { }

            InitConfig();

            MainInkCanvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _currentColor,
                Width = _currentThickness,
                Height = _currentThickness,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };
            MainInkCanvas.StrokeCollected += MainInkCanvas_StrokeCollected;
            _historyManager.HistoryChanged += (s, ev) => UpdateHistoryButtonStates();
            Closing += MainWindow_Closing;

            BatchListView.ItemsSource = _batchItems;
            _batchItems.CollectionChanged += (s, ev) => UpdateBatchSummary();
            InitBatchConfig();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _appConfig.IsSidebarCollapsed = _isSidebarCollapsed;
            _appConfig.PenColorHex = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
            _appConfig.PenThickness = _currentThickness;
            _appConfig.PenShape = _strokeShape.ToString();
            _appConfig.LastCompressSliderValue = Math.Round(CompressQualitySlider?.Value ?? 100.0);
            _appConfig.LastBatchMode = BatchModeSliderRadio?.IsChecked == true ? "Slider" : (BatchModeTargetSizeRadio?.IsChecked == true ? "TargetSize" : "Percentage");
            if (double.TryParse(BatchTargetSizeInput?.Text, out double ts)) _appConfig.LastBatchTargetSize = ts;
            _appConfig.LastBatchTargetUnit = (BatchTargetSizeUnit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KB";
            _appConfig.LastBatchSkipSmaller = BatchSkipSmallerCheck?.IsChecked == true;
            _appConfig.LastBatchPercentage = Math.Round(BatchPercentageSlider?.Value ?? 50.0);
            _appConfig.LastBatchOutputOption = BatchDestSubfolderRadio?.IsChecked == true ? "Subfolder" : (BatchDestCustomRadio?.IsChecked == true ? "Custom" : "Overwrite");
            _appConfig.LastBatchCustomFolder = BatchCustomFolderInput?.Text ?? "";
            _appConfig.Save();
        }

        private void InitConfig()
        {
            _appConfig = AppConfig.Load();
            if (!File.Exists(AppConfig.ConfigFilePath))
            {
                _appConfig.Save();
            }
            _isSidebarCollapsed = _appConfig.IsSidebarCollapsed;
            SetSidebarCollapsed(_isSidebarCollapsed, saveConfig: false);

            if (!string.IsNullOrEmpty(_appConfig.PenColorHex))
            {
                try
                {
                    _currentColor = (Color)ColorConverter.ConvertFromString(_appConfig.PenColorHex);
                }
                catch { }
            }

            if (_appConfig.PenThickness >= 1 && _appConfig.PenThickness <= 30)
            {
                _currentThickness = _appConfig.PenThickness;
            }

            if (Enum.TryParse<StrokeShape>(_appConfig.PenShape, out var shape))
            {
                _strokeShape = shape;
            }

            ThicknessSlider.Value = _currentThickness;
            ThicknessValueText.Text = $"{_currentThickness} px";
            PenColorIndicator.Background = new SolidColorBrush(_currentColor);
            UpdateColorSelectionRing(_appConfig.PenColorHex);
            UpdateShapeButtonVisuals();
            UpdatePenPreview();
        }

        private void InitBatchConfig()
        {
            if (_appConfig == null) return;

            if (_appConfig.LastBatchMode == "TargetSize")
            {
                BatchModeTargetSizeRadio.IsChecked = true;
            }
            else if (_appConfig.LastBatchMode == "Percentage")
            {
                BatchModePercentageRadio.IsChecked = true;
            }
            else
            {
                BatchModeSliderRadio.IsChecked = true;
            }

            BatchTargetSizeInput.Text = _appConfig.LastBatchTargetSize > 0 ? _appConfig.LastBatchTargetSize.ToString() : "500";
            BatchTargetSizeUnit.SelectedIndex = _appConfig.LastBatchTargetUnit == "MB" ? 1 : 0;
            BatchSkipSmallerCheck.IsChecked = _appConfig.LastBatchSkipSmaller;

            double pct = _appConfig.LastBatchPercentage;
            if (pct < 10 || pct > 90) pct = 50;
            BatchPercentageSlider.Value = pct;
            BatchPercentageValueText.Text = $"{pct}%";

            double slider = _appConfig.LastCompressSliderValue;
            if (slider < 10 || slider > 100) slider = 80;
            BatchQualitySlider.Value = slider;
            BatchSliderValueText.Text = $"{slider}%";

            if (_appConfig.LastBatchOutputOption == "Custom")
            {
                BatchDestCustomRadio.IsChecked = true;
            }
            else if (_appConfig.LastBatchOutputOption == "Overwrite")
            {
                BatchDestOverwriteRadio.IsChecked = true;
            }
            else
            {
                BatchDestSubfolderRadio.IsChecked = true;
            }

            BatchCustomFolderInput.Text = _appConfig.LastBatchCustomFolder ?? "";
            UpdateBatchModeVisuals();
        }

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            SetSidebarCollapsed(!_isSidebarCollapsed);
        }

        public void SetSidebarCollapsed(bool collapsed, bool saveConfig = true)
        {
            _isSidebarCollapsed = collapsed;
            if (_isSidebarCollapsed)
            {
                SidebarBorder.Width = 52;
                SidebarToggleIcon.Symbol = SymbolRegular.PanelLeftExpand20;
                SidebarToggleBtn.ToolTip = "Expand Sidebar";
                SidebarToggleText.Visibility = Visibility.Collapsed;

                CursorText.Visibility = Visibility.Collapsed;
                CropText.Visibility = Visibility.Collapsed;
                PenText.Visibility = Visibility.Collapsed;
                RotateText.Visibility = Visibility.Collapsed;
                FlipText.Visibility = Visibility.Collapsed;

                CursorBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
                CropBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
                PenBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
                RotateBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
                FlipBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
                SidebarToggleBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
            }
            else
            {
                SidebarBorder.Width = 145;
                SidebarToggleIcon.Symbol = SymbolRegular.PanelLeftContract20;
                SidebarToggleBtn.ToolTip = "Collapse Sidebar";
                SidebarToggleText.Visibility = Visibility.Visible;

                CursorText.Visibility = Visibility.Visible;
                CropText.Visibility = Visibility.Visible;
                PenText.Visibility = Visibility.Visible;
                RotateText.Visibility = Visibility.Visible;
                FlipText.Visibility = Visibility.Visible;

                CursorBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
                CropBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
                PenBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
                RotateBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
                FlipBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
                SidebarToggleBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
            }

            if (saveConfig)
            {
                _appConfig.IsSidebarCollapsed = _isSidebarCollapsed;
                _appConfig.Save();
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_isCropping) ExitCropMode();

            var dlg = new OpenFileDialog
            {
                Title = "Select Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadFile(dlg.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    _currentImage = decoder.Frames[0];
                    _currentPath = path;
                }

                DisplayImage.Source = _currentImage;
                DisplayImage.Width = _currentImage.PixelWidth;
                DisplayImage.Height = _currentImage.PixelHeight;

                ImageContainer.Width = _currentImage.PixelWidth;
                ImageContainer.Height = _currentImage.PixelHeight;
                MainInkCanvas.Width = _currentImage.PixelWidth;
                MainInkCanvas.Height = _currentImage.PixelHeight;
                MainInkCanvas.Strokes.Clear();
                ShapePreviewCanvas.Width = _currentImage.PixelWidth;
                ShapePreviewCanvas.Height = _currentImage.PixelHeight;
                ShapePreviewCanvas.Children.Clear();
                ActivateCursorMode();
                _savedUnappliedCropRect = null;
                _historyManager.Clear();
                UpdateHistoryButtonStates();

                CropCanvas.Width = _currentImage.PixelWidth;
                CropCanvas.Height = _currentImage.PixelHeight;

                ImageContainer.Visibility = Visibility.Visible;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                UpdateImageInfoText();

                SetControlsEnabled(true);
                _isManualZoom = false;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitImageToViewport();
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open image: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;
            if (_isCropping) ApplyCrop_Click(sender, e);

            BitmapSource src = GetComposedBitmap();

            var dlg = new SaveFileDialog
            {
                Title = "Save Image",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = ".png",
                FileName = string.IsNullOrEmpty(_currentPath) ? "image.png" : System.IO.Path.GetFileName(_currentPath)
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    BitmapEncoder encoder = dlg.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || dlg.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder { QualityLevel = 95 }
                        : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(src));
                    using (var fs = File.Create(dlg.FileName))
                    {
                        encoder.Save(fs);
                    }
                    _currentPath = dlg.FileName;
                    UpdateImageInfoText();
                    System.Windows.MessageBox.Show("Image saved successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void UpdateImageInfoText()
        {
            if (_currentImage == null)
            {
                FileNameText.Text = "";
                return;
            }

            string name = string.IsNullOrEmpty(_currentPath) ? "Untitled" : System.IO.Path.GetFileName(_currentPath);
            int w = _currentImage.PixelWidth;
            int h = _currentImage.PixelHeight;
            long size = GetOriginalFileSize();

            if (size > 0)
            {
                FileNameText.Text = $"— {name} ({w} × {h}, {FormatBytes(size)})";
            }
            else
            {
                FileNameText.Text = $"— {name} ({w} × {h})";
            }
        }

        public void SetImageAndStrokes(BitmapSource img, Stroke[] strokes)
        {
            _currentImage = img;
            DisplayImage.Source = _currentImage;
            DisplayImage.Width = _currentImage.PixelWidth;
            DisplayImage.Height = _currentImage.PixelHeight;

            ImageContainer.Width = _currentImage.PixelWidth;
            ImageContainer.Height = _currentImage.PixelHeight;
            MainInkCanvas.Width = _currentImage.PixelWidth;
            MainInkCanvas.Height = _currentImage.PixelHeight;
            ShapePreviewCanvas.Width = _currentImage.PixelWidth;
            ShapePreviewCanvas.Height = _currentImage.PixelHeight;
            CropCanvas.Width = _currentImage.PixelWidth;
            CropCanvas.Height = _currentImage.PixelHeight;

            MainInkCanvas.Strokes.Clear();
            if (strokes != null)
            {
                foreach (var s in strokes)
                {
                    MainInkCanvas.Strokes.Add(s);
                }
            }

            UpdateImageInfoText();
            _isManualZoom = false;
            FitImageToViewport();
        }

        private void ApplyImageTransform(BitmapSource oldImage, Stroke[] oldStrokes, BitmapSource newImage, Stroke[] newStrokes)
        {
            SetImageAndStrokes(newImage, newStrokes);
            _historyManager.Record(new ImageTransformAction(this, oldImage, oldStrokes, newImage, newStrokes));
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;
            if (_isCropping) ExitCropMode();
            _savedUnappliedCropRect = null;

            BitmapSource oldImage = _currentImage;
            Stroke[] oldStrokes = MainInkCanvas.Strokes.ToArray();
            BitmapSource baseSource = (oldStrokes.Length > 0) ? GetComposedBitmap() : oldImage;

            var newImage = new TransformedBitmap(baseSource, new RotateTransform(90));
            if (newImage.CanFreeze) newImage.Freeze();

            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;
            if (_isCropping) ExitCropMode();
            _savedUnappliedCropRect = null;

            BitmapSource oldImage = _currentImage;
            Stroke[] oldStrokes = MainInkCanvas.Strokes.ToArray();
            BitmapSource baseSource = (oldStrokes.Length > 0) ? GetComposedBitmap() : oldImage;

            var newImage = new TransformedBitmap(baseSource, new ScaleTransform(-1, 1, baseSource.PixelWidth / 2.0, 0));
            if (newImage.CanFreeze) newImage.Freeze();

            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        #region Zoom & Pan Logic (MatrixTransform)

        private void FitImageToViewport()
        {
            if (_currentImage == null) return;

            double viewW = ViewportGrid.ActualWidth;
            double viewH = ViewportGrid.ActualHeight;
            if (viewW <= 20 || viewH <= 20)
            {
                viewW = ViewportGrid.RenderSize.Width > 20 ? ViewportGrid.RenderSize.Width : 800;
                viewH = ViewportGrid.RenderSize.Height > 20 ? ViewportGrid.RenderSize.Height : 500;
            }

            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            double pad = 30.0;
            double availW = Math.Max(20, viewW - pad * 2);
            double availH = Math.Max(20, viewH - pad * 2);

            double scale = Math.Min(availW / imgW, availH / imgH);
            if (scale > 1.0) scale = 1.0;
            if (scale < 0.005) scale = 0.005;

            double offsetX = (viewW - imgW * scale) / 2.0;
            double offsetY = (viewH - imgH * scale) / 2.0;

            Matrix m = Matrix.Identity;
            m.Scale(scale, scale);
            m.Translate(offsetX, offsetY);
            ImageMatrixTransform.Matrix = m;

            _isManualZoom = false;
            UpdateZoomText();
            if (_isCropping) UpdateCropVisuals();
        }

        private void ResetZoom()
        {
            if (_currentImage == null) return;

            double viewW = ViewportGrid.ActualWidth > 20 ? ViewportGrid.ActualWidth : 800;
            double viewH = ViewportGrid.ActualHeight > 20 ? ViewportGrid.ActualHeight : 500;
            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            double scale = 1.0;
            double offsetX = (viewW - imgW * scale) / 2.0;
            double offsetY = (viewH - imgH * scale) / 2.0;

            Matrix m = Matrix.Identity;
            m.Scale(scale, scale);
            m.Translate(offsetX, offsetY);
            ImageMatrixTransform.Matrix = m;

            _isManualZoom = true;
            UpdateZoomText();
            if (_isCropping) UpdateCropVisuals();
        }


        private void UpdateZoomText()
        {
            int pct = (int)Math.Round(CurrentScale * 100);
            ZoomLevelText.Text = $"Zoom: {pct}%";
        }

        private void ZoomAt(Point center, double factor)
        {
            if (_currentImage == null) return;

            Matrix m = ImageMatrixTransform.Matrix;
            double currentScale = m.M11 > 0.0001 ? m.M11 : 1.0;
            double newScale = Math.Clamp(currentScale * factor, 0.02, 50.0);
            double actualFactor = newScale / currentScale;
            if (Math.Abs(actualFactor - 1.0) < 0.0001) return;

            m.ScaleAt(actualFactor, actualFactor, center.X, center.Y);
            ImageMatrixTransform.Matrix = m;
            _isManualZoom = true;

            UpdateZoomText();
            if (_isCropping) UpdateCropVisuals();
        }

        private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentImage == null) return;

            Point mousePos = e.GetPosition(ViewportGrid);
            double zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);

            ZoomAt(mousePos, zoomFactor);
            e.Handled = true;
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null) return;

            bool isPanTrigger = e.MiddleButton == MouseButtonState.Pressed ||
                                e.RightButton == MouseButtonState.Pressed ||
                                e.LeftButton == MouseButtonState.Pressed;

            if (isPanTrigger)
            {
                _isPanning = true;
                _panStart = e.GetPosition(ViewportGrid);
                ViewportGrid.CaptureMouse();
                ViewportGrid.Cursor = Cursors.ScrollAll;
                e.Handled = true;
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point current = e.GetPosition(ViewportGrid);
                double dx = current.X - _panStart.X;
                double dy = current.Y - _panStart.Y;
                _panStart = current;

                Matrix m = ImageMatrixTransform.Matrix;
                m.Translate(dx, dy);
                ImageMatrixTransform.Matrix = m;
                e.Handled = true;
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ViewportGrid.ReleaseMouseCapture();
                ViewportGrid.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void Viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ViewportGrid.ReleaseMouseCapture();
                ViewportGrid.Cursor = Cursors.Arrow;
            }
        }

        private void ViewportGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentImage != null && !_isCropping && !_isManualZoom)
            {
                FitImageToViewport();
            }
        }

        #endregion

        #region Crop Logic

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;

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
            if (_currentImage == null) return;
            ActivateCursorMode();

            _isCropping = true;
            _dragMode = DragMode.None;
            CropCanvas.Visibility = Visibility.Visible;
            CropBottomBar.Visibility = Visibility.Visible;
            CropBtn.Appearance = ControlAppearance.Primary;
            CursorBtn.Appearance = ControlAppearance.Secondary;

            // Restore unapplied crop if saved, otherwise default to full image
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
                // Default crop covers the entire image (full image)
                _cropRect = new Rect(0, 0, _currentImage.PixelWidth, _currentImage.PixelHeight);
            }

            UpdateCropVisuals();
        }

        private void ExitCropMode()
        {
            if (_isCropping && !_cropRect.IsEmpty && _cropRect.Width >= 2 && _cropRect.Height >= 2)
            {
                _savedUnappliedCropRect = _cropRect;
            }
            _isCropping = false;
            _dragMode = DragMode.None;
            CropCanvas.Visibility = Visibility.Collapsed;
            CropBottomBar.Visibility = Visibility.Collapsed;
            CropCanvas.ReleaseMouseCapture();
            CropCanvas.Cursor = Cursors.Arrow;
            CropBtn.Appearance = ControlAppearance.Secondary;
        }

        private void UpdateCropVisuals()
        {
            if (_currentImage == null || _cropRect.IsEmpty) return;

            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            double currentScale = CurrentScale;
            double invScale = 1.0 / Math.Max(0.001, currentScale);

            // Crop Box Border
            Canvas.SetLeft(CropBoxBorder, _cropRect.X);
            Canvas.SetTop(CropBoxBorder, _cropRect.Y);
            CropBoxBorder.Width = Math.Max(1, _cropRect.Width);
            CropBoxBorder.Height = Math.Max(1, _cropRect.Height);
            CropBoxBorder.BorderThickness = new Thickness(Math.Max(1.0, 2.0 * invScale));

            GridLineH.BorderThickness = new Thickness(0, Math.Max(0.5, 1.0 * invScale), 0, Math.Max(0.5, 1.0 * invScale));
            GridLineV.BorderThickness = new Thickness(Math.Max(0.5, 1.0 * invScale), 0, Math.Max(0.5, 1.0 * invScale), 0);

            // Inverse scale handles so they stay a crisp 14px / 28px on screen
            var handleScale = new ScaleTransform(invScale, invScale);
            HandleTL.RenderTransform = handleScale;
            HandleTR.RenderTransform = handleScale;
            HandleBR.RenderTransform = handleScale;
            HandleBL.RenderTransform = handleScale;
            HandleT.RenderTransform = handleScale;
            HandleB.RenderTransform = handleScale;
            HandleL.RenderTransform = handleScale;
            HandleR.RenderTransform = handleScale;

            // 4 Corner Handles (14x14, center at 7,7)
            Canvas.SetLeft(HandleTL, _cropRect.X - 7);
            Canvas.SetTop(HandleTL, _cropRect.Y - 7);

            Canvas.SetLeft(HandleTR, _cropRect.Right - 7);
            Canvas.SetTop(HandleTR, _cropRect.Y - 7);

            Canvas.SetLeft(HandleBR, _cropRect.Right - 7);
            Canvas.SetTop(HandleBR, _cropRect.Bottom - 7);

            Canvas.SetLeft(HandleBL, _cropRect.X - 7);
            Canvas.SetTop(HandleBL, _cropRect.Bottom - 7);

            // 4 Side Edge Handles (T/B: 28x10, center at 14,5; L/R: 10x28, center at 5,14)
            Canvas.SetLeft(HandleT, _cropRect.X + (_cropRect.Width / 2.0) - 14);
            Canvas.SetTop(HandleT, _cropRect.Y - 5);

            Canvas.SetLeft(HandleB, _cropRect.X + (_cropRect.Width / 2.0) - 14);
            Canvas.SetTop(HandleB, _cropRect.Bottom - 5);

            Canvas.SetLeft(HandleL, _cropRect.X - 5);
            Canvas.SetTop(HandleL, _cropRect.Y + (_cropRect.Height / 2.0) - 14);

            Canvas.SetLeft(HandleR, _cropRect.Right - 5);
            Canvas.SetTop(HandleR, _cropRect.Y + (_cropRect.Height / 2.0) - 14);

            // 4 Surrounding Dark Masks
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

            // Realtime Pixel Dimensions & Status
            int pw = Math.Max(1, (int)Math.Round(_cropRect.Width));
            int ph = Math.Max(1, (int)Math.Round(_cropRect.Height));
            int px = Math.Clamp((int)Math.Round(_cropRect.X), 0, (int)imgW - 1);
            int py = Math.Clamp((int)Math.Round(_cropRect.Y), 0, (int)imgH - 1);

            CropDimensionsText.Text = $"{pw} × {ph} px";
            CropStatusText.Text = $"|  Position: ({px}, {py})  |  Original: {(int)imgW} × {(int)imgH} px  |  Zoom: {(int)Math.Round(currentScale * 100)}%";
        }

        private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isCropping || _currentImage == null) return;

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

            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pt = e.GetPosition(CropCanvas);
            _dragStart = pt;
            _cropRectStart = _cropRect;

            double invScale = 1.0 / Math.Max(0.001, CurrentScale);
            double cornerThreshold = 24.0 * invScale;
            double edgeThreshold = 18.0 * invScale;

            // 1. Check Corner Handles (4 Sudut)
            if (Distance(pt, new Point(_cropRect.X, _cropRect.Y)) <= cornerThreshold)
                _dragMode = DragMode.ResizeTL;
            else if (Distance(pt, new Point(_cropRect.Right, _cropRect.Y)) <= cornerThreshold)
                _dragMode = DragMode.ResizeTR;
            else if (Distance(pt, new Point(_cropRect.Right, _cropRect.Bottom)) <= cornerThreshold)
                _dragMode = DragMode.ResizeBR;
            else if (Distance(pt, new Point(_cropRect.X, _cropRect.Bottom)) <= cornerThreshold)
                _dragMode = DragMode.ResizeBL;

            // 2. Check Side Edge Handles (4 Sisi)
            else if (Math.Abs(pt.Y - _cropRect.Y) <= edgeThreshold && pt.X >= _cropRect.X - edgeThreshold && pt.X <= _cropRect.Right + edgeThreshold)
                _dragMode = DragMode.ResizeT;
            else if (Math.Abs(pt.Y - _cropRect.Bottom) <= edgeThreshold && pt.X >= _cropRect.X - edgeThreshold && pt.X <= _cropRect.Right + edgeThreshold)
                _dragMode = DragMode.ResizeB;
            else if (Math.Abs(pt.X - _cropRect.X) <= edgeThreshold && pt.Y >= _cropRect.Y - edgeThreshold && pt.Y <= _cropRect.Bottom + edgeThreshold)
                _dragMode = DragMode.ResizeL;
            else if (Math.Abs(pt.X - _cropRect.Right) <= edgeThreshold && pt.Y >= _cropRect.Y - edgeThreshold && pt.Y <= _cropRect.Bottom + edgeThreshold)
                _dragMode = DragMode.ResizeR;

            // 3. Drag INSIDE crop: move crop box itself, not pan/zoom
            else if (_cropRect.Contains(pt))
            {
                _dragMode = DragMode.Move;
                CropCanvas.Cursor = Cursors.SizeAll;
            }

            // 4. Drag OUTSIDE crop: pan view position
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
            if (!_isCropping || _currentImage == null) return;

            Point rawPt = e.GetPosition(CropCanvas);
            double imgW = _currentImage.PixelWidth;
            double imgH = _currentImage.PixelHeight;

            // Clamped position strictly inside the image canvas [0, imgW] and [0, imgH]
            Point pt = new Point(Math.Clamp(rawPt.X, 0, imgW), Math.Clamp(rawPt.Y, 0, imgH));

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
                else if ((Math.Abs(rawPt.Y - _cropRect.Y) <= edgeThreshold || Math.Abs(rawPt.Y - _cropRect.Bottom) <= edgeThreshold) &&
                         rawPt.X >= _cropRect.X - edgeThreshold && rawPt.X <= _cropRect.Right + edgeThreshold)
                {
                    CropCanvas.Cursor = Cursors.SizeNS;
                }
                else if ((Math.Abs(rawPt.X - _cropRect.X) <= edgeThreshold || Math.Abs(rawPt.X - _cropRect.Right) <= edgeThreshold) &&
                         rawPt.Y >= _cropRect.Y - edgeThreshold && rawPt.Y <= _cropRect.Bottom + edgeThreshold)
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
                    double nx = Math.Clamp(_cropRectStart.X + dx, 0, Math.Max(0, imgW - _cropRectStart.Width));
                    double ny = Math.Clamp(_cropRectStart.Y + dy, 0, Math.Max(0, imgH - _cropRectStart.Height));
                    _cropRect = new Rect(nx, ny, _cropRectStart.Width, _cropRectStart.Height);
                    break;

                // 4 Corners: Free 2D dragging, strictly bounded within [0, imgW] and [0, imgH]
                case DragMode.ResizeTL:
                    double leftTL = Math.Clamp(pt.X, 0, _cropRectStart.Right - 10);
                    double topTL = Math.Clamp(pt.Y, 0, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(leftTL, topTL, _cropRectStart.Right - leftTL, _cropRectStart.Bottom - topTL);
                    break;

                case DragMode.ResizeTR:
                    double rightTR = Math.Clamp(pt.X, _cropRectStart.X + 10, imgW);
                    double topTR = Math.Clamp(pt.Y, 0, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(_cropRectStart.X, topTR, rightTR - _cropRectStart.X, _cropRectStart.Bottom - topTR);
                    break;

                case DragMode.ResizeBR:
                    double rightBR = Math.Clamp(pt.X, _cropRectStart.X + 10, imgW);
                    double bottomBR = Math.Clamp(pt.Y, _cropRectStart.Y + 10, imgH);
                    _cropRect = new Rect(_cropRectStart.X, _cropRectStart.Y, rightBR - _cropRectStart.X, bottomBR - _cropRectStart.Y);
                    break;

                case DragMode.ResizeBL:
                    double leftBL = Math.Clamp(pt.X, 0, _cropRectStart.Right - 10);
                    double bottomBL = Math.Clamp(pt.Y, _cropRectStart.Y + 10, imgH);
                    _cropRect = new Rect(leftBL, _cropRectStart.Y, _cropRectStart.Right - leftBL, bottomBL - _cropRectStart.Y);
                    break;

                // 4 Side Edges: 1D free dragging, strictly bounded
                case DragMode.ResizeT:
                    double topT = Math.Clamp(pt.Y, 0, _cropRectStart.Bottom - 10);
                    _cropRect = new Rect(_cropRectStart.X, topT, _cropRectStart.Width, _cropRectStart.Bottom - topT);
                    break;

                case DragMode.ResizeB:
                    double bottomB = Math.Clamp(pt.Y, _cropRectStart.Y + 10, imgH);
                    _cropRect = new Rect(_cropRectStart.X, _cropRectStart.Y, _cropRectStart.Width, bottomB - _cropRectStart.Y);
                    break;

                case DragMode.ResizeL:
                    double leftL = Math.Clamp(pt.X, 0, _cropRectStart.Right - 10);
                    _cropRect = new Rect(leftL, _cropRectStart.Y, _cropRectStart.Right - leftL, _cropRectStart.Height);
                    break;

                case DragMode.ResizeR:
                    double rightR = Math.Clamp(pt.X, _cropRectStart.X + 10, imgW);
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
            if (_currentImage == null || _cropRect.IsEmpty || _cropRect.Width < 2 || _cropRect.Height < 2) return;

            BitmapSource oldImage = _currentImage;
            Stroke[] oldStrokes = MainInkCanvas.Strokes.ToArray();
            BitmapSource baseSource = (oldStrokes.Length > 0) ? GetComposedBitmap() : oldImage;

            int px = Math.Clamp((int)Math.Round(_cropRect.X), 0, baseSource.PixelWidth - 1);
            int py = Math.Clamp((int)Math.Round(_cropRect.Y), 0, baseSource.PixelHeight - 1);
            int pw = Math.Clamp((int)Math.Round(_cropRect.Width), 1, baseSource.PixelWidth - px);
            int ph = Math.Clamp((int)Math.Round(_cropRect.Height), 1, baseSource.PixelHeight - py);

            var newImage = new CroppedBitmap(baseSource, new Int32Rect(px, py, pw, ph));
            if (newImage.CanFreeze) newImage.Freeze();

            ExitCropMode();
            _savedUnappliedCropRect = null;
            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        private void CancelCrop_Click(object sender, RoutedEventArgs e)
        {
            ExitCropMode();
            _savedUnappliedCropRect = null;
            ActivateCursorMode();
        }

        #endregion

        private void SetControlsEnabled(bool enabled)
        {
            CursorBtn.IsEnabled = enabled;
            CropBtn.IsEnabled = enabled;
            PenBtn.IsEnabled = enabled;
            RotateBtn.IsEnabled = enabled;
            FlipBtn.IsEnabled = enabled;
            SaveBtn.IsEnabled = enabled;
            CompressBtn.IsEnabled = true;
            UpdateHistoryButtonStates();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (BatchCompressModal.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseBatchCompressModal();
                    e.Handled = true;
                    return;
                }
            }

            if (CompressModal.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseCompressModal();
                    e.Handled = true;
                    return;
                }
            }

            if (_isCropping)
            {
                if (e.Key == Key.Enter)
                {
                    ApplyCrop_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelCrop_Click(sender, e);
                    e.Handled = true;
                    return;
                }
            }

            if (_currentImage != null)
            {
                // Mode shortcuts (no modifiers): V (Cursor / Pan), P (Pen), C (Crop)
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (e.Key == Key.V)
                    {
                        ActivateCursorMode();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.P)
                    {
                        if (!_isPenActive)
                        {
                            ActivatePenMode();
                            PenSettingsPopup.IsOpen = false;
                        }
                        else
                        {
                            PenSettingsPopup.IsOpen = !PenSettingsPopup.IsOpen;
                        }
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.C)
                    {
                        Crop_Click(sender, e);
                        e.Handled = true;
                        return;
                    }
                }

                // Redo shortcuts: Ctrl+Shift+Z or Ctrl+Y
                if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z) ||
                    (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y))
                {
                    Redo_Click(sender, e);
                    e.Handled = true;
                    return;
                }

                // Undo shortcut: Ctrl+Z
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
                {
                    Undo_Click(sender, e);
                    e.Handled = true;
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.OemPlus || e.Key == Key.Add)
                    {
                        ZoomAt(new Point(ViewportGrid.ActualWidth / 2.0, ViewportGrid.ActualHeight / 2.0), 1.25);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    {
                        ZoomAt(new Point(ViewportGrid.ActualWidth / 2.0, ViewportGrid.ActualHeight / 2.0), 1.0 / 1.25);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                    {
                        FitImageToViewport();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                    {
                        ResetZoom();
                        e.Handled = true;
                    }
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    LoadFile(files[0]);
                }
            }
        }

        #region Pen & Markup Logic

        private void Cursor_Click(object sender, RoutedEventArgs e)
        {
            ActivateCursorMode();
        }

        public void ActivateCursorMode()
        {
            if (_isCropping) ExitCropMode();
            _isPenActive = false;
            PenSettingsPopup.IsOpen = false;
            PenBtn.Appearance = ControlAppearance.Secondary;
            CropBtn.Appearance = ControlAppearance.Secondary;
            CursorBtn.Appearance = ControlAppearance.Primary;
            MainInkCanvas.IsHitTestVisible = false;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
            ViewportGrid.Cursor = Cursors.Arrow;
        }

        public void ActivatePenMode()
        {
            if (_isCropping) ExitCropMode();
            _isPenActive = true;
            PenBtn.Appearance = ControlAppearance.Primary;
            CursorBtn.Appearance = ControlAppearance.Secondary;
            CropBtn.Appearance = ControlAppearance.Secondary;
            ApplyPenModeToCanvas();
            UpdatePenPreview();
        }

        private void Pen_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;

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
            if (_isCropping) ExitCropMode();
            _historyManager.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_isCropping) ExitCropMode();
            _historyManager.Redo();
        }

        private void UpdateHistoryButtonStates()
        {
            if (UndoBtn == null || RedoBtn == null) return;
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
        }

        private void MainInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            _historyManager.Record(new AddStrokeAction(e.Stroke, MainInkCanvas));
        }

        private void ApplyPenModeToCanvas()
        {
            MainInkCanvas.IsHitTestVisible = true;
            MainInkCanvas.DefaultDrawingAttributes.Color = _currentColor;
            MainInkCanvas.DefaultDrawingAttributes.Width = _currentThickness;
            MainInkCanvas.DefaultDrawingAttributes.Height = _currentThickness;

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
            if (ThicknessValueText == null || MainInkCanvas == null) return;
            _currentThickness = Math.Round(e.NewValue);
            ThicknessValueText.Text = $"{_currentThickness} px";
            MainInkCanvas.DefaultDrawingAttributes.Width = _currentThickness;
            MainInkCanvas.DefaultDrawingAttributes.Height = _currentThickness;
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
            string[] hexes = {
                "FFEB3B", "FFC107", "FF9800", "E53935", "5E35B1", "8E24AA", "D81B60", "C2185B",
                "00B0FF", "0078D4", "1565C0", "7CB342", "2E7D32", "FFFFFF", "9E9E9E", "212121"
            };

            string normalized = selectedHex.TrimStart('#').ToUpperInvariant();
            foreach (var h in hexes)
            {
                var el = FindName($"ColorBorder_{h}") as Border;
                if (el != null)
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
                _isDrawingShape = false;
                _isPanning = true;
                _panStart = e.GetPosition(ViewportGrid);
                ViewportGrid.CaptureMouse();
                ViewportGrid.Cursor = Cursors.ScrollAll;
                e.Handled = true;
                return;
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
            if (_isDrawingShape && e.LeftButton == MouseButtonState.Pressed)
            {
                Point current = e.GetPosition(MainInkCanvas);
                RenderShapePreview(_shapeStartPoint, current);
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

                if (Distance(_shapeStartPoint, endPoint) >= 2)
                {
                    var stroke = CreateShapeStroke(_shapeStartPoint, endPoint, _strokeShape, _currentColor, _currentThickness);
                    MainInkCanvas.Strokes.Add(stroke);
                    _historyManager.Record(new AddStrokeAction(stroke, MainInkCanvas));
                }
                e.Handled = true;
            }
        }

        private void RenderShapePreview(Point start, Point end)
        {
            ShapePreviewCanvas.Children.Clear();
            var stroke = CreateShapeStroke(start, end, _strokeShape, _currentColor, _currentThickness);
            var geom = stroke.GetGeometry();
            var path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = new SolidColorBrush(_currentColor)
            };
            ShapePreviewCanvas.Children.Add(path);
        }

        public static Stroke CreateShapeStroke(Point start, Point end, StrokeShape shape, Color color, double thickness)
        {
            var pts = new StylusPointCollection();
            var attr = new DrawingAttributes
            {
                Color = color,
                Width = thickness,
                Height = thickness,
                FitToCurve = false,
                StylusTip = StylusTip.Ellipse
            };

            if (shape == StrokeShape.Line)
            {
                pts.Add(new StylusPoint(start.X, start.Y));
                pts.Add(new StylusPoint(end.X, end.Y));
            }
            else if (shape == StrokeShape.Arrow)
            {
                Vector dir = end - start;
                double len = dir.Length;
                if (len < 2)
                {
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                }
                else
                {
                    double angle = Math.Atan2(dir.Y, dir.X);
                    double headLen = Math.Clamp(thickness * 4.0, 14.0, 45.0);
                    double barbAngle = Math.PI * 0.82;

                    Point w1 = new Point(end.X + Math.Cos(angle + barbAngle) * headLen,
                                         end.Y + Math.Sin(angle + barbAngle) * headLen);
                    Point w2 = new Point(end.X + Math.Cos(angle - barbAngle) * headLen,
                                         end.Y + Math.Sin(angle - barbAngle) * headLen);

                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w1.X, w1.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w2.X, w2.Y));
                }
            }
            else if (shape == StrokeShape.DoubleArrow)
            {
                Vector dir = end - start;
                double len = dir.Length;
                if (len < 2)
                {
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                }
                else
                {
                    double angle = Math.Atan2(dir.Y, dir.X);
                    double headLen = Math.Clamp(thickness * 4.0, 14.0, 45.0);
                    double barbAngle = Math.PI * 0.82;

                    Point w1 = new Point(end.X + Math.Cos(angle + barbAngle) * headLen,
                                         end.Y + Math.Sin(angle + barbAngle) * headLen);
                    Point w2 = new Point(end.X + Math.Cos(angle - barbAngle) * headLen,
                                         end.Y + Math.Sin(angle - barbAngle) * headLen);

                    Point aw1 = new Point(start.X + Math.Cos(angle + Math.PI - barbAngle) * headLen,
                                          start.Y + Math.Sin(angle + Math.PI - barbAngle) * headLen);
                    Point aw2 = new Point(start.X + Math.Cos(angle + Math.PI + barbAngle) * headLen,
                                          start.Y + Math.Sin(angle + Math.PI + barbAngle) * headLen);

                    pts.Add(new StylusPoint(aw1.X, aw1.Y));
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(aw2.X, aw2.Y));
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w1.X, w1.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w2.X, w2.Y));
                }
            }

            return new Stroke(pts, attr);
        }

        private BitmapSource GetComposedBitmap()
        {
            if (_currentImage == null) return null!;
            if (MainInkCanvas.Strokes.Count == 0) return _currentImage;

            int w = _currentImage.PixelWidth;
            int h = _currentImage.PixelHeight;

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(_currentImage, new Rect(0, 0, w, h));
                MainInkCanvas.Strokes.Draw(dc);
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        #endregion

        #region Image Compression Logic

        private void Compress_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;
            if (_isCropping) ExitCropMode();

            CompressResolutionText.Text = $"{_currentImage.PixelWidth} × {_currentImage.PixelHeight} px";
            _originalFileSize = GetOriginalFileSize();
            CompressOriginalSizeText.Text = FormatBytes(_originalFileSize);

            // Automatically match output format to the loaded image
            string ext = !string.IsNullOrEmpty(_currentPath)
                ? System.IO.Path.GetExtension(_currentPath).ToLowerInvariant()
                : ".png";

            _compressFormat = (ext == ".jpg" || ext == ".jpeg") ? "jpg" : "png";

            // Restore last session slider value, default to 100% (original resolution/size)
            double savedSlider = _appConfig?.LastCompressSliderValue ?? 100.0;
            if (savedSlider < 10.0 || savedSlider > 100.0) savedSlider = 100.0;
            CompressQualitySlider.Value = savedSlider;

            CompressModal.Visibility = Visibility.Visible;
            UpdateCompressionEstimate();
        }

        private void CloseCompressModal_Click(object sender, RoutedEventArgs e)
        {
            CloseCompressModal();
        }

        private void CloseCompressModal()
        {
            CompressModal.Visibility = Visibility.Collapsed;
        }

        private void CompressQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CompressQualityValueText == null || CompressQualitySlider == null || _currentImage == null) return;

            if (_appConfig != null)
            {
                _appConfig.LastCompressSliderValue = Math.Round(e.NewValue);
                _appConfig.Save();
            }

            UpdateCompressionEstimate();
        }

        private void CompressMinus_Click(object sender, RoutedEventArgs e)
        {
            if (CompressQualitySlider == null) return;
            if (CompressQualitySlider.Value > CompressQualitySlider.Minimum)
            {
                CompressQualitySlider.Value = Math.Max(CompressQualitySlider.Minimum, CompressQualitySlider.Value - 1);
            }
        }

        private void CompressPlus_Click(object sender, RoutedEventArgs e)
        {
            if (CompressQualitySlider == null) return;
            if (CompressQualitySlider.Value < CompressQualitySlider.Maximum)
            {
                CompressQualitySlider.Value = Math.Min(CompressQualitySlider.Maximum, CompressQualitySlider.Value + 1);
            }
        }

        private void UpdateCompressionEstimate()
        {
            if (_currentImage == null || CompressNewSizeText == null) return;

            int percent = (int)Math.Round(CompressQualitySlider.Value);
            CompressQualityValueText.Text = $"{percent}%";

            if (CompressMinusBtn != null) CompressMinusBtn.IsEnabled = percent > (int)CompressQualitySlider.Minimum;
            if (CompressPlusBtn != null) CompressPlusBtn.IsEnabled = percent < (int)CompressQualitySlider.Maximum;

            if (_originalFileSize <= 0)
            {
                _originalFileSize = GetOriginalFileSize();
            }

            if (percent >= 100)
            {
                _lastCompressedData = null;
                _lastCompressedQuality = 100;
                _lastCompressedFormat = _compressFormat;

                CompressNewSizeText.Text = FormatBytes(_originalFileSize);
                CompressReductionText.Text = " (Original)";
                CompressReductionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
                return;
            }

            try
            {
                BitmapSource baseSource = GetComposedBitmap();
                byte[] data = EncodeCompressedImage(baseSource, _compressFormat, percent);
                _lastCompressedData = data;
                _lastCompressedQuality = percent;
                _lastCompressedFormat = _compressFormat;

                long actualBytes = data.Length;
                CompressNewSizeText.Text = FormatBytes(actualBytes);

                if (_originalFileSize > 0)
                {
                    double diff = (1.0 - ((double)actualBytes / _originalFileSize)) * 100.0;
                    if (diff > 0.5)
                    {
                        CompressReductionText.Text = $" (-{diff:F0}%)";
                        CompressReductionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    }
                    else if (diff < -0.5)
                    {
                        CompressReductionText.Text = $" (+{-diff:F0}%)";
                        CompressReductionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                    }
                    else
                    {
                        CompressReductionText.Text = " (0%)";
                        CompressReductionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
                    }
                }
            }
            catch { }
        }

        private void SaveCompressed_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;

            string ext = _compressFormat == "jpg"
                ? (!string.IsNullOrEmpty(_currentPath) && System.IO.Path.GetExtension(_currentPath).Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpeg" : ".jpg")
                : ".png";
            string filter = _compressFormat == "jpg" ? "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg" : "PNG Image (*.png)|*.png";
            string baseName = string.IsNullOrEmpty(_currentPath)
                ? "compressed_image"
                : System.IO.Path.GetFileNameWithoutExtension(_currentPath) + "_compressed";

            var dlg = new SaveFileDialog
            {
                Title = "Save Compressed Image",
                Filter = filter,
                DefaultExt = ext,
                FileName = baseName + ext
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    int percent = (int)Math.Round(CompressQualitySlider.Value);
                    byte[] data;

                    if (percent >= 100 && MainInkCanvas.Strokes.Count == 0 && !_historyManager.CanUndo && !string.IsNullOrEmpty(_currentPath) && File.Exists(_currentPath))
                    {
                        File.Copy(_currentPath, dlg.FileName, true);
                        CloseCompressModal();
                        System.Windows.MessageBox.Show(
                            "Image compressed and saved successfully.",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    if (_lastCompressedData != null && _lastCompressedQuality == percent && _lastCompressedFormat == _compressFormat)
                    {
                        data = _lastCompressedData;
                    }
                    else
                    {
                        BitmapSource baseSource = GetComposedBitmap();
                        data = EncodeCompressedImage(baseSource, _compressFormat, percent);
                    }

                    File.WriteAllBytes(dlg.FileName, data);
                    CloseCompressModal();

                    System.Windows.MessageBox.Show(
                        "Image compressed and saved successfully.",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save compressed image: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            CompressCurrentItem.IsEnabled = _currentImage != null;
            CompressMenuPopup.IsOpen = true;
        }

        private void CompressCurrentItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null) return;
            CompressMenuPopup.IsOpen = false;
            Compress_Click(sender, e);
        }

        private void BatchCompressItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CompressMenuPopup.IsOpen = false;
            OpenBatchCompressModal();
        }

        private static byte[] EncodeCompressedImage(BitmapSource source, string format, int percent)
        {
            return ImageCompressor.CompressByQuality(source, format, percent);
        }

        private long GetOriginalFileSize()
        {
            if (!string.IsNullOrEmpty(_currentPath) && File.Exists(_currentPath))
            {
                try
                {
                    return new FileInfo(_currentPath).Length;
                }
                catch { }
            }

            if (_currentImage != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(_currentImage));
                    enc.Save(ms);
                    return ms.Length;
                }
                catch { }
            }

            return 0;
        }

        private static string FormatBytes(long bytes)
        {
            return ImageCompressor.FormatBytes(bytes);
        }

        #endregion

        #region Batch Compression Logic

        private void OpenBatchCompressModal()
        {
            if (_isCropping) ExitCropMode();
            BatchCompressModal.Visibility = Visibility.Visible;
            UpdateBatchSummary();
        }

        private void CloseBatchCompressModal_Click(object sender, RoutedEventArgs e)
        {
            CloseBatchCompressModal();
        }

        private void CloseBatchCompressModal()
        {
            if (_isBatchProcessing)
            {
                var result = System.Windows.MessageBox.Show(
                    "Batch compression is currently in progress. Do you want to cancel and close?",
                    "Compression In Progress",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _batchCts?.Cancel();
                    BatchCompressModal.Visibility = Visibility.Collapsed;
                }
                return;
            }

            BatchCompressModal.Visibility = Visibility.Collapsed;
        }

        private void BatchAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Add Images to Batch",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                AddFilesToBatch(dlg.FileNames);
            }
        }

        private void BatchAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Folder Containing Images",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.FolderName))
            {
                try
                {
                    var files = Directory.EnumerateFiles(dlg.FolderName, "*.*", SearchOption.AllDirectories)
                                         .Where(f => SupportedBatchExtensions.Contains(System.IO.Path.GetExtension(f)));
                    AddFilesToBatch(files);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to scan folder: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void BatchClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchProcessing) return;
            _batchItems.Clear();
            UpdateBatchSummary();
            BatchProgressBar.Value = 0;
            BatchProgressStatusText.Text = "Queue cleared";
            BatchProgressPercentText.Text = "";
            BatchOpenFolderBtn.Visibility = Visibility.Collapsed;
        }

        private void BatchRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchProcessing) return;
            if (sender is FrameworkElement el && el.Tag is BatchItem item)
            {
                _batchItems.Remove(item);
                UpdateBatchSummary();
            }
        }

        private void BatchQueue_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void BatchQueue_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths != null && paths.Length > 0)
                {
                    var allFiles = new List<string>();
                    foreach (var p in paths)
                    {
                        if (File.Exists(p))
                        {
                            if (SupportedBatchExtensions.Contains(System.IO.Path.GetExtension(p)))
                                allFiles.Add(p);
                        }
                        else if (Directory.Exists(p))
                        {
                            try
                            {
                                var dirFiles = Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                                                        .Where(f => SupportedBatchExtensions.Contains(System.IO.Path.GetExtension(f)));
                                allFiles.AddRange(dirFiles);
                            }
                            catch { }
                        }
                    }
                    AddFilesToBatch(allFiles);
                }
            }
        }

        private void AddFilesToBatch(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(_batchItems.Select(x => x.FilePath), StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (var path in paths)
            {
                if (existing.Contains(path)) continue;

                try
                {
                    var fi = new FileInfo(path);
                    if (!fi.Exists) continue;

                    int w = 0, h = 0;
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        if (decoder.Frames.Count > 0)
                        {
                            w = decoder.Frames[0].PixelWidth;
                            h = decoder.Frames[0].PixelHeight;
                        }
                    }
                    catch { }

                    var item = new BatchItem
                    {
                        FilePath = path,
                        FileName = fi.Name,
                        Extension = fi.Extension.ToLowerInvariant(),
                        OriginalSize = fi.Length,
                        Width = w,
                        Height = h,
                        Status = BatchItemStatus.Waiting
                    };

                    _batchItems.Add(item);
                    existing.Add(path);
                    added++;
                }
                catch { }
            }

            UpdateBatchSummary();
            if (added > 0)
            {
                BatchProgressStatusText.Text = $"Added {added} image(s). Ready to compress.";
                BatchOpenFolderBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateBatchSummary()
        {
            int count = _batchItems.Count;
            long totalBytes = _batchItems.Sum(x => x.OriginalSize);

            BatchEmptyPlaceholder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BatchListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            BatchQueueSummaryText.Text = $"{count} file{(count == 1 ? "" : "s")} ({ImageCompressor.FormatBytes(totalBytes)})";
            BatchStartBtn.IsEnabled = count > 0 && !_isBatchProcessing;
            BatchClearBtn.IsEnabled = count > 0 && !_isBatchProcessing;
        }

        private void BatchMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateBatchModeVisuals();
        }

        private void UpdateBatchModeVisuals()
        {
            if (BatchSliderControls == null || BatchTargetSizeControls == null || BatchPercentageControls == null) return;

            bool isSlider = BatchModeSliderRadio?.IsChecked == true;
            bool isTarget = BatchModeTargetSizeRadio?.IsChecked == true;
            bool isPercent = BatchModePercentageRadio?.IsChecked == true;

            BatchSliderControls.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
            BatchTargetSizeControls.Visibility = isTarget ? Visibility.Visible : Visibility.Collapsed;
            BatchPercentageControls.Visibility = isPercent ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BatchQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BatchSliderValueText == null) return;
            int val = (int)Math.Round(e.NewValue);
            BatchSliderValueText.Text = $"{val}%";

            if (BatchSliderMinusBtn != null) BatchSliderMinusBtn.IsEnabled = val > (int)BatchQualitySlider.Minimum;
            if (BatchSliderPlusBtn != null) BatchSliderPlusBtn.IsEnabled = val < (int)BatchQualitySlider.Maximum;
        }

        private void BatchSliderMinus_Click(object sender, RoutedEventArgs e)
        {
            if (BatchQualitySlider == null) return;
            if (BatchQualitySlider.Value > BatchQualitySlider.Minimum)
                BatchQualitySlider.Value = Math.Max(BatchQualitySlider.Minimum, BatchQualitySlider.Value - 1);
        }

        private void BatchSliderPlus_Click(object sender, RoutedEventArgs e)
        {
            if (BatchQualitySlider == null) return;
            if (BatchQualitySlider.Value < BatchQualitySlider.Maximum)
                BatchQualitySlider.Value = Math.Min(BatchQualitySlider.Maximum, BatchQualitySlider.Value + 1);
        }

        private void BatchPercentageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BatchPercentageValueText == null) return;
            int val = (int)Math.Round(e.NewValue);
            BatchPercentageValueText.Text = $"{val}%";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void BatchDest_Changed(object sender, RoutedEventArgs e)
        {
        }

        private void BatchBrowseCustomFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Output Directory",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.FolderName))
            {
                BatchCustomFolderInput.Text = dlg.FolderName;
                BatchDestCustomRadio.IsChecked = true;
            }
        }

        private async void BatchStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchProcessing) return;
            if (_batchItems.Count == 0) return;

            // Validate Destination
            bool isCustom = BatchDestCustomRadio.IsChecked == true;
            bool isOverwrite = BatchDestOverwriteRadio.IsChecked == true;
            string customFolder = BatchCustomFolderInput.Text.Trim();
            string subfolderName = BatchSubfolderNameInput.Text.Trim();
            if (string.IsNullOrEmpty(subfolderName)) subfolderName = "_compressed";

            if (isCustom)
            {
                if (string.IsNullOrEmpty(customFolder))
                {
                    System.Windows.MessageBox.Show("Please select or enter a valid custom output folder.", "Folder Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    if (!Directory.Exists(customFolder))
                        Directory.CreateDirectory(customFolder);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cannot use custom folder: {ex.Message}", "Invalid Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            if (isOverwrite)
            {
                var confirm = System.Windows.MessageBox.Show(
                    "Warning: Overwrite original files is selected!\n\nThis will permanently replace your original image files with the compressed versions. Are you sure you want to proceed?",
                    "Confirm Overwrite",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;
            }

            // Determine Compression Mode & Parameters
            bool isSliderMode = BatchModeSliderRadio.IsChecked == true;
            bool isTargetSizeMode = BatchModeTargetSizeRadio.IsChecked == true;
            bool isPercentageMode = BatchModePercentageRadio.IsChecked == true;

            int sliderQuality = (int)Math.Round(BatchQualitySlider.Value);

            long targetBytes = 500 * 1024;
            bool skipSmaller = BatchSkipSmallerCheck.IsChecked == true;
            if (isTargetSizeMode)
            {
                if (!double.TryParse(BatchTargetSizeInput.Text, out double sizeVal) || sizeVal <= 0)
                {
                    System.Windows.MessageBox.Show("Please enter a valid positive target size number.", "Invalid Target Size", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                string unit = (BatchTargetSizeUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KB";
                targetBytes = (long)(sizeVal * (unit == "MB" ? 1024 * 1024 : 1024));
            }

            double percentageRatio = BatchPercentageSlider.Value;

            // Setup UI state for running batch
            _isBatchProcessing = true;
            _batchCts = new CancellationTokenSource();
            var token = _batchCts.Token;

            BatchStartBtn.Visibility = Visibility.Collapsed;
            BatchCancelBtn.Visibility = Visibility.Visible;
            BatchCancelBtn.IsEnabled = true;
            BatchOpenFolderBtn.Visibility = Visibility.Collapsed;
            BatchClearBtn.IsEnabled = false;

            BatchProgressBar.Minimum = 0;
            BatchProgressBar.Maximum = _batchItems.Count;
            BatchProgressBar.Value = 0;

            // Reset items status
            foreach (var item in _batchItems)
            {
                item.Status = BatchItemStatus.Waiting;
                item.NewSize = 0;
                item.ErrorMessage = "";
            }

            int total = _batchItems.Count;
            int processed = 0;
            int skippedCount = 0;
            int successCount = 0;
            int errorCount = 0;
            string? firstOutputDir = null;

            try
            {
                for (int i = 0; i < _batchItems.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var item = _batchItems[i];
                    item.Status = BatchItemStatus.Processing;

                    BatchProgressStatusText.Text = $"Compressing ({i + 1}/{total}): {item.FileName}...";
                    BatchProgressPercentText.Text = $"{((i * 100) / total)}%";
                    BatchProgressBar.Value = i;

                    // Determine output file path
                    string outDir = "";
                    string outPath = "";

                    if (isOverwrite)
                    {
                        outPath = item.FilePath;
                        outDir = System.IO.Path.GetDirectoryName(item.FilePath)!;
                    }
                    else if (isCustom)
                    {
                        outDir = customFolder;
                        outPath = System.IO.Path.Combine(outDir, item.FileName);
                    }
                    else
                    {
                        string originalDir = System.IO.Path.GetDirectoryName(item.FilePath)!;
                        outDir = System.IO.Path.Combine(originalDir, subfolderName);
                        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                        outPath = System.IO.Path.Combine(outDir, item.FileName);
                    }

                    if (string.IsNullOrEmpty(firstOutputDir)) firstOutputDir = outDir;

                    // Execute compression in background thread
                    await Task.Run(() =>
                    {
                        try
                        {
                            // Check if skip smaller applies
                            if (isTargetSizeMode && skipSmaller && item.OriginalSize <= targetBytes)
                            {
                                if (!isOverwrite)
                                {
                                    File.Copy(item.FilePath, outPath, true);
                                }
                                item.NewSize = item.OriginalSize;
                                item.Status = BatchItemStatus.Skipped;
                                skippedCount++;
                                return;
                            }

                            // Load bitmap
                            BitmapSource bmp = ImageCompressor.LoadBitmapFromFile(item.FilePath);
                            string format = item.Extension.TrimStart('.');

                            byte[] compressedData;
                            if (isSliderMode)
                            {
                                compressedData = ImageCompressor.CompressByQuality(bmp, format, sliderQuality);
                            }
                            else if (isTargetSizeMode)
                            {
                                compressedData = ImageCompressor.CompressToTargetSize(bmp, format, targetBytes, item.OriginalSize);
                            }
                            else // isPercentageMode
                            {
                                compressedData = ImageCompressor.CompressToPercentageOfSize(bmp, format, percentageRatio, item.OriginalSize);
                            }

                            // Write to temp file then move to avoid partial writes
                            string tempOut = outPath + ".tmp";
                            File.WriteAllBytes(tempOut, compressedData);
                            File.Move(tempOut, outPath, true);

                            item.NewSize = compressedData.Length;
                            item.Status = BatchItemStatus.Completed;
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            item.Status = BatchItemStatus.Error;
                            item.ErrorMessage = ex.Message;
                            errorCount++;
                        }
                    }, token);

                    processed++;
                    BatchProgressBar.Value = processed;
                }

                _lastBatchOutputFolder = firstOutputDir ?? "";

                // Final summary calculation
                long originalSum = _batchItems.Sum(x => x.OriginalSize);
                long newSum = _batchItems.Where(x => x.NewSize > 0).Sum(x => x.NewSize);
                long savedBytes = originalSum - newSum;

                if (token.IsCancellationRequested)
                {
                    BatchProgressStatusText.Text = $"Batch compression canceled ({processed}/{total} processed).";
                    BatchProgressPercentText.Text = "";
                }
                else
                {
                    string savedStr = savedBytes > 0
                        ? $"Saved {ImageCompressor.FormatBytes(savedBytes)} (-{((double)savedBytes / originalSum * 100):F0}%)"
                        : "No reduction";
                    BatchProgressStatusText.Text = $"Finished {total} files! {savedStr}. Success: {successCount}, Skipped: {skippedCount}, Errors: {errorCount}";
                    BatchProgressPercentText.Text = "100%";
                }
            }
            catch (Exception ex)
            {
                BatchProgressStatusText.Text = $"Batch error: {ex.Message}";
            }
            finally
            {
                _isBatchProcessing = false;
                BatchStartBtn.Visibility = Visibility.Visible;
                BatchCancelBtn.Visibility = Visibility.Collapsed;
                BatchClearBtn.IsEnabled = true;
                if (!string.IsNullOrEmpty(_lastBatchOutputFolder) && Directory.Exists(_lastBatchOutputFolder))
                {
                    BatchOpenFolderBtn.Visibility = Visibility.Visible;
                }
            }
        }

        private void BatchCancel_Click(object sender, RoutedEventArgs e)
        {
            _batchCts?.Cancel();
            BatchCancelBtn.IsEnabled = false;
            BatchProgressStatusText.Text = "Canceling...";
        }

        private void BatchOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastBatchOutputFolder) && Directory.Exists(_lastBatchOutputFolder))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _lastBatchOutputFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open folder: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
