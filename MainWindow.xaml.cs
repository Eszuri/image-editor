using System.IO;
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

        // App Config & Sidebar State
        private AppConfig _appConfig = new();
        private bool _isSidebarCollapsed;

        // Global History Manager
        private readonly HistoryManager _historyManager = new();

        // Pending Open Path
        private string? _pendingOpenFilePath;

        // True only when active canvas is created as Blank Paper / New Canvas
        private bool _isCreatedCanvasMode;

        private double CurrentScale => ImageMatrixTransform.Matrix.M11 > 0.0001 ? ImageMatrixTransform.Matrix.M11 : 1.0;

        public MainWindow() : this(GetCommandLineInitialFile())
        {
        }

        private static string? GetCommandLineInitialFile()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 1; i < args.Length; i++)
                {
                    string candidate = args[i].Trim('"', ' ');
                    if (!candidate.StartsWith('-') && File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch { }
            return null;
        }

        public MainWindow(string? initialFilePath)
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
                StylusTip = StylusTip.Ellipse,
                IgnorePressure = true
            };
            MainInkCanvas.StrokeCollected += MainInkCanvas_StrokeCollected;
            _historyManager.HistoryChanged += (s, ev) =>
            {
                UpdateHistoryButtonStates();
                UpdateLayerListUI();
            };
            Closing += MainWindow_Closing;

            BatchListView.ItemsSource = _batchItems;
            _batchItems.CollectionChanged += (s, ev) => UpdateBatchSummary();
            InitBatchConfig();

            if (!string.IsNullOrEmpty(initialFilePath) && File.Exists(initialFilePath))
            {
                Loaded += (s, ev) => LoadFile(initialFilePath);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (HasEditingProgress())
            {
                var result = System.Windows.MessageBox.Show(
                    "You have unsaved edits. Are you sure you want to close?",
                    "Unsaved Changes",
                    System.Windows.MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            _appConfig.IsSidebarCollapsed = _isSidebarCollapsed;
            _appConfig.PenColorHex = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
            _appConfig.PenThickness = _currentThickness;
            _appConfig.PenShape = _strokeShape.ToString();
            _appConfig.LastCompressSliderValue = Math.Round(CompressQualitySlider?.Value ?? 100.0);
            if (BatchModeSliderRadio?.IsChecked == true)
            {
                _appConfig.LastBatchMode = "Slider";
            }
            else if (BatchModeTargetSizeRadio?.IsChecked == true)
            {
                _appConfig.LastBatchMode = "TargetSize";
            }
            else
            {
                _appConfig.LastBatchMode = "Percentage";
            }

            if (double.TryParse(BatchTargetSizeInput?.Text, out double ts))
            {
                _appConfig.LastBatchTargetSize = ts;
            }

            _appConfig.LastBatchTargetUnit = (BatchTargetSizeUnit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KB";
            _appConfig.LastBatchSkipSmaller = BatchSkipSmallerCheck?.IsChecked == true;
            _appConfig.LastBatchPercentage = Math.Round(BatchPercentageSlider?.Value ?? 50.0);

            if (BatchDestCustomRadio?.IsChecked == true)
            {
                _appConfig.LastBatchOutputOption = "Custom";
            }
            else if (BatchDestOverwriteRadio?.IsChecked == true)
            {
                _appConfig.LastBatchOutputOption = "Overwrite";
            }
            else
            {
                _appConfig.LastBatchOutputOption = "Subfolder";
            }

            _appConfig.LastBatchCustomFolder = BatchCustomFolderInput?.Text ?? "";

            _appConfig.LastResizeMaintainAspectRatio = ResizeLockAspectCheck?.IsChecked == true;
            if (int.TryParse(ResizeWidthInput?.Text, out int rw) && rw > 0)
            {
                _appConfig.LastResizeWidth = rw;
            }
            if (int.TryParse(ResizeHeightInput?.Text, out int rh) && rh > 0)
            {
                _appConfig.LastResizeHeight = rh;
            }

            if (int.TryParse(NewCanvasWidthInput?.Text, out int ncw) && ncw > 0)
            {
                _appConfig.LastNewCanvasWidth = ncw;
            }
            if (int.TryParse(NewCanvasHeightInput?.Text, out int nch) && nch > 0)
            {
                _appConfig.LastNewCanvasHeight = nch;
            }

            _appConfig.Save();
            _batchCts?.Dispose();
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
            if (_appConfig == null)
            {
                return;
            }

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

            BatchTargetSizeInput.Text = _appConfig.LastBatchTargetSize > 0
                ? _appConfig.LastBatchTargetSize.ToString()
                : "500";
            BatchTargetSizeUnit.SelectedIndex = _appConfig.LastBatchTargetUnit == "MB" ? 1 : 0;
            BatchSkipSmallerCheck.IsChecked = _appConfig.LastBatchSkipSmaller;

            double pct = _appConfig.LastBatchPercentage;
            if (pct < 10 || pct > 90)
            {
                pct = 50;
            }
            BatchPercentageSlider.Value = pct;
            BatchPercentageValueText.Text = $"{pct}%";

            double slider = _appConfig.LastCompressSliderValue;
            if (slider < 10 || slider > 100)
            {
                slider = 80;
            }
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
            var vis = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            var align = _isSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;

            SidebarBorder.Width = _isSidebarCollapsed ? 52 : 145;
            SidebarToggleIcon.Symbol = _isSidebarCollapsed ? SymbolRegular.PanelLeftExpand20 : SymbolRegular.PanelLeftContract20;
            SidebarToggleBtn.ToolTip = _isSidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar";
            SidebarToggleText.Visibility = vis;

            CursorText.Visibility = vis;
            CropText.Visibility = vis;
            PenText.Visibility = vis;
            ImportImageText.Visibility = vis;
            ResizeText.Visibility = vis;
            RotateText.Visibility = vis;
            FlipText.Visibility = vis;

            CursorBtn.HorizontalContentAlignment = align;
            CropBtn.HorizontalContentAlignment = align;
            PenBtn.HorizontalContentAlignment = align;
            ImportImageBtn.HorizontalContentAlignment = align;
            ResizeBtn.HorizontalContentAlignment = align;
            RotateBtn.HorizontalContentAlignment = align;
            FlipBtn.HorizontalContentAlignment = align;
            SidebarToggleBtn.HorizontalContentAlignment = align;

            if (saveConfig)
            {
                _appConfig.IsSidebarCollapsed = _isSidebarCollapsed;
                _appConfig.Save();
            }
        }

        private bool HasEditingProgress()
        {
            if (_currentImage == null)
            {
                return false;
            }
            return _historyManager.CanUndo
                || MainInkCanvas.Strokes.Count > 0
                || _layers.Count > 0
                || _isCropping
                || _savedUnappliedCropRect != null
                || _currentImage != _originalLoadedImage;
        }

        private void RequestOpenImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            // Only prompt if the user has edited the image at least once.
            // If the image has not been edited at all (or no image is loaded), open directly.
            if (!HasEditingProgress())
            {
                LoadFile(path);
                return;
            }

            _pendingOpenFilePath = path;
            ReplaceConfirmNewFileNameText.Text = System.IO.Path.GetFileName(path);
            ReplaceConfirmNewFileNameText.ToolTip = path;
            ReplaceConfirmMessageText.Text =
                "You have unsaved edits on the current image. Opening a new image will discard your progress.";

            ReplaceConfirmModal.Visibility = Visibility.Visible;
        }

        private void ReplaceConfirmReplace_Click(object sender, RoutedEventArgs e)
        {
            string? path = _pendingOpenFilePath;
            CloseReplaceConfirmModal();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                LoadFile(path);
            }
        }

        private void ReplaceConfirmNewWindow_Click(object sender, RoutedEventArgs e)
        {
            string? path = _pendingOpenFilePath;
            CloseReplaceConfirmModal();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                OpenInNewWindow(path);
            }
        }

        private void CloseReplaceConfirmModal_Click(object sender, RoutedEventArgs e)
        {
            CloseReplaceConfirmModal();
        }

        private void CloseReplaceConfirmModal()
        {
            _pendingOpenFilePath = null;
            ReplaceConfirmModal.Visibility = Visibility.Collapsed;
        }

        private void OpenInNewWindow(string path)
        {
            try
            {
                var newWin = new MainWindow(path);

                var workArea = SystemParameters.WorkArea;
                double newLeft = this.Left + 30;
                double newTop = this.Top + 30;
                if (newLeft + this.Width <= workArea.Right && newTop + this.Height <= workArea.Bottom)
                {
                    newWin.WindowStartupLocation = WindowStartupLocation.Manual;
                    newWin.Left = newLeft;
                    newWin.Top = newTop;
                }
                else
                {
                    newWin.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                newWin.Show();
                newWin.Activate();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open new window: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_isCropping)
            {
                ExitCropMode();
            }

            var dlg = new OpenFileDialog
            {
                Title = "Select Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                RequestOpenImage(dlg.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                var bmp = ImageCompressor.LoadBitmapFromFile(path);
                _currentImage = bmp;
                _originalLoadedImage = bmp;
                _currentPath = path;

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
                UpdateCanvasClips(_currentImage.PixelWidth, _currentImage.PixelHeight);
                ClearLayers();
                ActivateCursorMode();
                _savedUnappliedCropRect = null;
                _historyManager.Clear();
                UpdateHistoryButtonStates();

                CropCanvas.Width = _currentImage.PixelWidth;
                CropCanvas.Height = _currentImage.PixelHeight;

                ImageContainer.Visibility = Visibility.Visible;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                UpdateImageInfoText();

                _isCreatedCanvasMode = false;
                SetControlsEnabled(true);
                _isManualZoom = false;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitImageToViewport();
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open image: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            if (_isCropping)
            {
                ApplyCrop_Click(sender, e);
            }

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
                    bool isJpeg = dlg.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                  dlg.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
                    BitmapEncoder encoder = isJpeg
                        ? new JpegBitmapEncoder { QualityLevel = 95 }
                        : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(src));
                    using (var fs = File.Create(dlg.FileName))
                    {
                        encoder.Save(fs);
                    }
                    _currentPath = dlg.FileName;
                    UpdateImageInfoText();
                    System.Windows.MessageBox.Show(
                        "Image saved successfully!",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
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
                FileNameText.Text = $"— {name} ({w} × {h}, {ImageCompressor.FormatBytes(size)})";
            }
            else
            {
                FileNameText.Text = $"— {name} ({w} × {h})";
            }
        }

        public void SetImageAndLayers(BitmapSource img, Stroke[] strokes, LayerItem[]? layers = null)
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

            UpdateCanvasClips(_currentImage.PixelWidth, _currentImage.PixelHeight);

            MainInkCanvas.Strokes.Clear();
            ClearLayers();
            if (strokes != null)
            {
                foreach (var s in strokes)
                {
                    MainInkCanvas.Strokes.Add(s);
                }
            }
            if (layers != null)
            {
                foreach (var l in layers)
                {
                    InternalAddLayer(l);
                }
            }

            UpdateImageInfoText();
            _isManualZoom = false;
            FitImageToViewport();
            UpdateLayerListUI();
        }

        public void SetImageAndStrokes(BitmapSource img, Stroke[] strokes) => SetImageAndLayers(img, strokes);

        private void ApplyImageTransform(BitmapSource oldImage, Stroke[] oldStrokes, BitmapSource newImage, Stroke[] newStrokes)
        {
            ApplyImageTransform(oldImage, oldStrokes, _layers.ToArray(), newImage, newStrokes, Array.Empty<LayerItem>());
        }

        private void ApplyImageTransform(BitmapSource oldImage, Stroke[] oldStrokes, LayerItem[] oldLayers, BitmapSource newImage, Stroke[] newStrokes, LayerItem[] newLayers)
        {
            SetImageAndLayers(newImage, newStrokes, newLayers);
            _historyManager.Record(new ImageTransformAction(this, oldImage, oldStrokes, oldLayers, newImage, newStrokes, newLayers));
        }

        private (BitmapSource oldImage, Stroke[] oldStrokes, BitmapSource baseSource) GetTransformBase()
        {
            BitmapSource oldImage = _currentImage!;
            Stroke[] oldStrokes = MainInkCanvas.Strokes.ToArray();
            BitmapSource baseSource = (oldStrokes.Length > 0 || _layers.Count > 0) ? GetComposedBitmap() : oldImage;
            return (oldImage, oldStrokes, baseSource);
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            if (_isCropping)
            {
                ExitCropMode();
            }
            _savedUnappliedCropRect = null;

            var (oldImage, oldStrokes, baseSource) = GetTransformBase();
            var newImage = new TransformedBitmap(baseSource, new RotateTransform(90));
            if (newImage.CanFreeze)
            {
                newImage.Freeze();
            }

            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            if (_isCropping)
            {
                ExitCropMode();
            }
            _savedUnappliedCropRect = null;

            var (oldImage, oldStrokes, baseSource) = GetTransformBase();
            var newImage = new TransformedBitmap(
                baseSource,
                new ScaleTransform(-1, 1, baseSource.PixelWidth / 2.0, 0));
            if (newImage.CanFreeze)
            {
                newImage.Freeze();
            }

            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        #region Zoom & Pan Logic (MatrixTransform)

        private void FitImageToViewport()
        {
            if (_currentImage == null)
            {
                return;
            }

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
            if (scale > 1.0)
            {
                scale = 1.0;
            }
            if (scale < 0.005)
            {
                scale = 0.005;
            }

            double offsetX = (viewW - imgW * scale) / 2.0;
            double offsetY = (viewH - imgH * scale) / 2.0;

            Matrix m = Matrix.Identity;
            m.Scale(scale, scale);
            m.Translate(offsetX, offsetY);
            ImageMatrixTransform.Matrix = m;

            _isManualZoom = false;
            UpdateZoomText();
            UpdatePenCanvasThickness();
            if (_isCropping)
            {
                UpdateCropVisuals();
            }
        }

        private void ResetZoom()
        {
            if (_currentImage == null)
            {
                return;
            }

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
            UpdatePenCanvasThickness();
            if (_isCropping)
            {
                UpdateCropVisuals();
            }
        }


        private void UpdateZoomText()
        {
            int pct = (int)Math.Round(CurrentScale * 100);
            ZoomLevelText.Text = $"Zoom: {pct}%";
        }

        private void ZoomAt(Point center, double factor)
        {
            if (_currentImage == null)
            {
                return;
            }

            Matrix m = ImageMatrixTransform.Matrix;
            double currentScale = m.M11 > 0.0001 ? m.M11 : 1.0;
            double newScale = Math.Clamp(currentScale * factor, 0.02, 50.0);
            double actualFactor = newScale / currentScale;
            if (Math.Abs(actualFactor - 1.0) < 0.0001)
            {
                return;
            }

            m.ScaleAt(actualFactor, actualFactor, center.X, center.Y);
            ImageMatrixTransform.Matrix = m;
            _isManualZoom = true;

            UpdateZoomText();
            UpdatePenCanvasThickness();
            if (_isCropping)
            {
                UpdateCropVisuals();
            }
        }

        private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            Point mousePos = e.GetPosition(ViewportGrid);
            double zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);

            ZoomAt(mousePos, zoomFactor);
            e.Handled = true;
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

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
                HidePenCursor();
                Point current = e.GetPosition(ViewportGrid);
                double dx = current.X - _panStart.X;
                double dy = current.Y - _panStart.Y;
                _panStart = current;

                Matrix m = ImageMatrixTransform.Matrix;
                m.Translate(dx, dy);
                ImageMatrixTransform.Matrix = m;
                e.Handled = true;
                return;
            }

            if (_isPenActive && _currentImage != null)
            {
                UpdatePenCursor(e.GetPosition(ViewportGrid));
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ViewportGrid.ReleaseMouseCapture();
                ViewportGrid.Cursor = Cursors.Arrow;
                if (_isPenActive && _currentImage != null)
                {
                    UpdatePenCursor(e.GetPosition(ViewportGrid));
                }
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
            HidePenCursor();
        }

        private void ViewportGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentImage != null && !_isCropping && !_isManualZoom)
            {
                FitImageToViewport();
            }
        }

        #endregion


        private void SetControlsEnabled(bool enabled)
        {
            CursorBtn.IsEnabled = enabled;
            CropBtn.IsEnabled = enabled;
            PenBtn.IsEnabled = enabled;
            ImportImageBtn.Visibility = (enabled && _isCreatedCanvasMode) ? Visibility.Visible : Visibility.Collapsed;
            ImportImageBtn.IsEnabled = enabled && _isCreatedCanvasMode;
            LayersToggleBtn.Visibility = (enabled && _isCreatedCanvasMode) ? Visibility.Visible : Visibility.Collapsed;
            LayersToggleBtn.IsEnabled = enabled && _isCreatedCanvasMode;
            RightSidebarBorder.Visibility = (enabled && _isCreatedCanvasMode && _isLayerPanelOpen) ? Visibility.Visible : Visibility.Collapsed;
            ResizeBtn.IsEnabled = enabled;
            RotateBtn.IsEnabled = enabled;
            FlipBtn.IsEnabled = enabled;
            SaveBtn.IsEnabled = enabled;
            CompressBtn.IsEnabled = true;
            UpdateHistoryButtonStates();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (ResizeModal.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseResizeModal();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    ApplyResize_Click(sender, e);
                    e.Handled = true;
                    return;
                }
            }

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

            if (ReplaceConfirmModal.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseReplaceConfirmModal();
                    e.Handled = true;
                    return;
                }
            }

            if (NewCanvasModal.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseNewCanvasModal();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    ApplyNewCanvas_Click(sender, e);
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

            // New Canvas shortcut: Ctrl+N
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                NewCanvas_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (_currentImage != null)
            {
                // Rename layer shortcut: F2
                if (e.Key == Key.F2)
                {
                    if (_selectedLayerItem != null && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
                    {
                        StartRenameLayer(_selectedLayerItem);
                        e.Handled = true;
                        return;
                    }
                }

                // Delete or Duplicate selected layer (Image or Stroke)
                if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    if (_selectedLayerItem != null && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
                    {
                        DeleteSelectedLayer();
                        e.Handled = true;
                        return;
                    }
                }

                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
                {
                    if (_selectedLayerItem != null && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
                    {
                        DuplicateSelectedLayer();
                        e.Handled = true;
                        return;
                    }
                }

                // Import Image shortcut: Ctrl+I (only in Blank Canvas mode)
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
                {
                    if (_isCreatedCanvasMode && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
                    {
                        ImportImage_Click(sender, e);
                        e.Handled = true;
                        return;
                    }
                }

                // Toggle Layer Panel shortcut: Ctrl+L (only in Blank Canvas mode)
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L)
                {
                    if (_isCreatedCanvasMode && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
                    {
                        LayersToggle_Click(sender, e);
                        e.Handled = true;
                        return;
                    }
                }

                // Mode shortcuts (no modifiers): V (Cursor / Pan), P (Pen), C (Crop)
                if (Keyboard.Modifiers == ModifierKeys.None && !(FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase))
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
                    if (FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase)
                    {
                        return;
                    }
                    Redo_Click(sender, e);
                    e.Handled = true;
                    return;
                }

                // Undo shortcut: Ctrl+Z
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
                {
                    if (FocusManager.GetFocusedElement(this) is System.Windows.Controls.Primitives.TextBoxBase)
                    {
                        return;
                    }
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
                    if (_isCreatedCanvasMode && _currentImage != null)
                    {
                        Point dropPos = e.GetPosition(OverlayCanvas);
                        foreach (var file in files)
                        {
                            if (File.Exists(file) && SupportedBatchExtensions.Contains(System.IO.Path.GetExtension(file)))
                            {
                                AddOverlayImageFromFile(file, dropPos);
                            }
                        }
                    }
                    else if (File.Exists(files[0]))
                    {
                        string ext = System.IO.Path.GetExtension(files[0]);
                        if (SupportedBatchExtensions.Contains(ext))
                        {
                            RequestOpenImage(files[0]);
                        }
                    }
                }
            }
        }

    }
}