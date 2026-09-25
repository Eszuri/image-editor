using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        private Color _currentCanvasBgColor = Colors.White;

        #region Base Layer Context Menu

        public ContextMenu CreateBaseLayerContextMenu()
        {
            var menu = new ContextMenu();

            var itemResize = new System.Windows.Controls.MenuItem
            {
                Header = "Edit Dimensions (Shrink / Expand)...",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowExpand24, FontSize = 16 }
            };
            itemResize.Click += (s, e) => OpenCanvasDimensionsModal();
            menu.Items.Add(itemResize);

            var itemBgColor = new System.Windows.Controls.MenuItem
            {
                Header = "Change Background Color...",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Color24, FontSize = 16 }
            };
            itemBgColor.Click += (s, e) => OpenChangeBgColorModal();
            menu.Items.Add(itemBgColor);

            return menu;
        }

        #endregion

        #region Canvas Dimensions Modal (Edit Base Layer Dimensions)

        private int _origCanvasDimW;
        private int _origCanvasDimH;
        private bool _isUpdatingCanvasDimInputs;

        public void OpenCanvasDimensionsModal()
        {
            if (_currentImage == null) return;
            if (_isCropping) ExitCropMode();

            _origCanvasDimW = _currentImage.PixelWidth;
            _origCanvasDimH = _currentImage.PixelHeight;

            CanvasDimensionsCurrentText.Text = $"{_origCanvasDimW} × {_origCanvasDimH} px";

            _isUpdatingCanvasDimInputs = true;
            CanvasDimensionsWidthInput.Text = _origCanvasDimW.ToString();
            CanvasDimensionsHeightInput.Text = _origCanvasDimH.ToString();
            CanvasDimensionsAspectCheck.IsChecked = true;
            CanvasDimensionsModeCanvasRadio.IsChecked = true;
            _isUpdatingCanvasDimInputs = false;

            CanvasDimensionsModal.Visibility = Visibility.Visible;
            CanvasDimensionsWidthInput.Focus();
            CanvasDimensionsWidthInput.SelectAll();
        }

        public void CloseCanvasDimensionsModal() => CanvasDimensionsModal.Visibility = Visibility.Collapsed;

        private void CloseCanvasDimensionsModal_Click(object sender, RoutedEventArgs e) => CloseCanvasDimensionsModal();

        private void CanvasDimensionsPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string tag)
            {
                _isUpdatingCanvasDimInputs = true;
                switch (tag)
                {
                    case "1920x1080":
                        CanvasDimensionsWidthInput.Text = "1920";
                        CanvasDimensionsHeightInput.Text = "1080";
                        break;
                    case "1280x720":
                        CanvasDimensionsWidthInput.Text = "1280";
                        CanvasDimensionsHeightInput.Text = "720";
                        break;
                    case "1080x1080":
                        CanvasDimensionsWidthInput.Text = "1080";
                        CanvasDimensionsHeightInput.Text = "1080";
                        break;
                    case "FitWindow":
                        double vw = ViewportGrid.ActualWidth > 20 ? ViewportGrid.ActualWidth : 1280;
                        double vh = ViewportGrid.ActualHeight > 20 ? ViewportGrid.ActualHeight : 720;
                        int fitW = Math.Max(100, (int)Math.Round(vw - 60));
                        int fitH = Math.Max(100, (int)Math.Round(vh - 60));
                        CanvasDimensionsWidthInput.Text = fitW.ToString();
                        CanvasDimensionsHeightInput.Text = fitH.ToString();
                        break;
                }
                _isUpdatingCanvasDimInputs = false;
            }
        }

        private void CanvasDimensionsWidthInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCanvasDimInputs || _origCanvasDimW <= 0 || _origCanvasDimH <= 0) return;
            if (CanvasDimensionsAspectCheck?.IsChecked == true && int.TryParse(CanvasDimensionsWidthInput.Text.Trim(), out int val) && val > 0)
            {
                _isUpdatingCanvasDimInputs = true;
                int newH = (int)Math.Max(1, Math.Round((double)val * _origCanvasDimH / _origCanvasDimW));
                CanvasDimensionsHeightInput.Text = newH.ToString();
                _isUpdatingCanvasDimInputs = false;
            }
        }

        private void CanvasDimensionsHeightInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCanvasDimInputs || _origCanvasDimW <= 0 || _origCanvasDimH <= 0) return;
            if (CanvasDimensionsAspectCheck?.IsChecked == true && int.TryParse(CanvasDimensionsHeightInput.Text.Trim(), out int val) && val > 0)
            {
                _isUpdatingCanvasDimInputs = true;
                int newW = (int)Math.Max(1, Math.Round((double)val * _origCanvasDimW / _origCanvasDimH));
                CanvasDimensionsWidthInput.Text = newW.ToString();
                _isUpdatingCanvasDimInputs = false;
            }
        }

        private void CanvasDimensionsAspectCheck_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasDimensionsAspectCheck.IsChecked == true && _origCanvasDimW > 0 && _origCanvasDimH > 0)
            {
                if (int.TryParse(CanvasDimensionsWidthInput.Text.Trim(), out int val) && val > 0)
                {
                    _isUpdatingCanvasDimInputs = true;
                    int newH = (int)Math.Max(1, Math.Round((double)val * _origCanvasDimH / _origCanvasDimW));
                    CanvasDimensionsHeightInput.Text = newH.ToString();
                    _isUpdatingCanvasDimInputs = false;
                }
            }
        }

        private void ApplyCanvasDimensions_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;

            if (!int.TryParse(CanvasDimensionsWidthInput.Text.Trim(), out int targetW) ||
                !int.TryParse(CanvasDimensionsHeightInput.Text.Trim(), out int targetH) ||
                targetW < 10 || targetW > 32768 ||
                targetH < 10 || targetH > 32768)
            {
                System.Windows.MessageBox.Show("Please enter valid width and height between 10 and 32,768 pixels.",
                    "Invalid Dimensions", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int oldW = _currentImage.PixelWidth;
            int oldH = _currentImage.PixelHeight;

            if (targetW == oldW && targetH == oldH)
            {
                CloseCanvasDimensionsModal();
                return;
            }

            bool isScaleContent = CanvasDimensionsModeScaleRadio?.IsChecked == true;
            BitmapSource newBaseImage;
            Stroke[] oldStrokes = MainInkCanvas.Strokes.ToArray();
            LayerItem[] oldLayers = _layers.ToArray();

            if (isScaleContent)
            {
                double scaleX = (double)targetW / oldW;
                double scaleY = (double)targetH / oldH;

                var scaled = new TransformedBitmap(_currentImage, new ScaleTransform(scaleX, scaleY));
                scaled.Freeze();
                newBaseImage = scaled;

                foreach (var s in MainInkCanvas.Strokes)
                {
                    s.Transform(new Matrix(scaleX, 0, 0, scaleY, 0, 0), false);
                }

                foreach (var l in _layers)
                {
                    if (l is StrokeLayerItem sli)
                    {
                        sli.HostCanvas.Width = targetW;
                        sli.HostCanvas.Height = targetH;
                        if (sli.Presenter != null)
                        {
                            sli.Presenter.Width = targetW;
                            sli.Presenter.Height = targetH;
                            sli.Presenter.Clip = new RectangleGeometry(new Rect(0, 0, targetW, targetH));
                        }
                        sli.HostCanvas.Clip = new RectangleGeometry(new Rect(0, 0, targetW, targetH));
                        foreach (var s in sli.Strokes)
                        {
                            s.Transform(new Matrix(scaleX, 0, 0, scaleY, 0, 0), false);
                        }
                        sli.UpdateSelectionBounds();
                    }
                    else if (l is OverlayImageItem oii)
                    {
                        oii.X *= scaleX;
                        oii.Y *= scaleY;
                        oii.Width *= scaleX;
                        oii.Height *= scaleY;
                        if (oii.ImageVisualElement != null)
                        {
                            Canvas.SetLeft(oii.ImageVisualElement, oii.X);
                            Canvas.SetTop(oii.ImageVisualElement, oii.Y);
                            oii.ImageVisualElement.Width = oii.Width;
                            oii.ImageVisualElement.Height = oii.Height;
                        }
                    }
                }
            }
            else
            {
                // Canvas Size: Expand or crop bounds without scaling layers
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    if (_currentCanvasBgColor != Colors.Transparent)
                    {
                        dc.DrawRectangle(new SolidColorBrush(_currentCanvasBgColor), null, new Rect(0, 0, targetW, targetH));
                    }
                    dc.DrawImage(_currentImage, new Rect(0, 0, oldW, oldH));
                }
                var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);
                rtb.Freeze();
                newBaseImage = rtb;

                foreach (var l in _layers)
                {
                    if (l is StrokeLayerItem sli)
                    {
                        sli.HostCanvas.Width = targetW;
                        sli.HostCanvas.Height = targetH;
                        if (sli.Presenter != null)
                        {
                            sli.Presenter.Width = targetW;
                            sli.Presenter.Height = targetH;
                            sli.Presenter.Clip = new RectangleGeometry(new Rect(0, 0, targetW, targetH));
                        }
                        sli.HostCanvas.Clip = new RectangleGeometry(new Rect(0, 0, targetW, targetH));
                        sli.UpdateSelectionBounds();
                    }
                }
            }

            _currentImage = newBaseImage;
            DisplayImage.Source = newBaseImage;
            DisplayImage.Width = targetW;
            DisplayImage.Height = targetH;
            ImageContainer.Width = targetW;
            ImageContainer.Height = targetH;
            MainInkCanvas.Width = targetW;
            MainInkCanvas.Height = targetH;
            ShapePreviewCanvas.Width = targetW;
            ShapePreviewCanvas.Height = targetH;
            CropCanvas.Width = targetW;
            CropCanvas.Height = targetH;

            UpdateCanvasClips(targetW, targetH);
            UpdateImageInfoText();
            FitImageToViewport();
            UpdateLayerListUI();

            _historyManager.Record(new ImageTransformAction(this, _currentImage, oldStrokes, oldLayers,
                                                               newBaseImage, MainInkCanvas.Strokes.ToArray(), _layers.ToArray()));

            CloseCanvasDimensionsModal();
        }

        #endregion

        #region Change Background Color Modal

        private bool _isUpdatingChangeBgColor;
        private double _changeBgSaturation = 1.0;

        public void OpenChangeBgColorModal()
        {
            if (_currentImage == null) return;
            if (_isCropping) ExitCropMode();

            if (_currentCanvasBgColor == Colors.Transparent)
            {
                ChangeBgCurrentColorTile.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                ChangeBgCurrentColorText.Text = "Transparent";
                ChangeBgTransparentRadio.IsChecked = true;
            }
            else if (_currentCanvasBgColor == (Color)ColorConverter.ConvertFromString("#1E1E1E"))
            {
                ChangeBgCurrentColorTile.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                ChangeBgCurrentColorText.Text = "Dark (#1E1E1E)";
                ChangeBgDarkRadio.IsChecked = true;
            }
            else if (_currentCanvasBgColor == Colors.White)
            {
                ChangeBgCurrentColorTile.Background = Brushes.White;
                ChangeBgCurrentColorText.Text = "White (#FFFFFF)";
                ChangeBgWhiteRadio.IsChecked = true;
            }
            else
            {
                string hex = $"#{_currentCanvasBgColor.R:X2}{_currentCanvasBgColor.G:X2}{_currentCanvasBgColor.B:X2}";
                ChangeBgCurrentColorTile.Background = new SolidColorBrush(_currentCanvasBgColor);
                ChangeBgCurrentColorText.Text = hex;
                ChangeBgCustomRadio.IsChecked = true;
                SetChangeBgCustomColor(hex);
            }

            UpdateChangeBgCustomPanelVisibility();
            ChangeBgColorModal.Visibility = Visibility.Visible;
        }

        public void CloseChangeBgColorModal() => ChangeBgColorModal.Visibility = Visibility.Collapsed;

        private void CloseChangeBgColorModal_Click(object sender, RoutedEventArgs e) => CloseChangeBgColorModal();

        private void ChangeBgRadio_Checked(object sender, RoutedEventArgs e) => UpdateChangeBgCustomPanelVisibility();

        private void UpdateChangeBgCustomPanelVisibility()
        {
            if (ChangeBgCustomColorPanel != null)
            {
                ChangeBgCustomColorPanel.Visibility = (ChangeBgCustomRadio?.IsChecked == true)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void SetChangeBgCustomColor(string hex)
        {
            if (TryParseHexColor(hex, out Color color))
            {
                UpdateChangeBgControls(color, updateHexText: true, updateRgbText: true, updateSliders: true);
            }
        }

        private void UpdateChangeBgControls(Color color, bool updateHexText, bool updateRgbText, bool updateSliders)
        {
            if (_isUpdatingChangeBgColor) return;
            _isUpdatingChangeBgColor = true;
            try
            {
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                if (updateHexText && ChangeBgCustomHexInput != null)
                {
                    ChangeBgCustomHexInput.Text = hex;
                }

                if (updateRgbText)
                {
                    if (ChangeBgCustomRInput != null) ChangeBgCustomRInput.Text = color.R.ToString();
                    if (ChangeBgCustomGInput != null) ChangeBgCustomGInput.Text = color.G.ToString();
                    if (ChangeBgCustomBInput != null) ChangeBgCustomBInput.Text = color.B.ToString();
                }

                RgbToHsl(color, out double h, out double s, out double l);

                bool isAchromatic = (s < 0.01 || l <= 0.001 || l >= 0.999);
                if (isAchromatic)
                {
                    if (ChangeBgHueSlider != null)
                    {
                        h = ChangeBgHueSlider.Value;
                    }
                }
                else
                {
                    _changeBgSaturation = s;
                }

                if (updateSliders)
                {
                    if (!isAchromatic && ChangeBgHueSlider != null)
                    {
                        ChangeBgHueSlider.Value = Math.Round(h);
                    }
                    if (ChangeBgLightnessSlider != null)
                    {
                        ChangeBgLightnessSlider.Value = Math.Round(l * 100);
                    }
                }

                if (ChangeBgHueValueText != null)
                {
                    ChangeBgHueValueText.Text = $"{(int)Math.Round(h)}°";
                }
                if (ChangeBgLightnessValueText != null)
                {
                    ChangeBgLightnessValueText.Text = $"{(int)Math.Round(l * 100)}%";
                }

                double satForPreview = _changeBgSaturation > 0.05 ? _changeBgSaturation : 1.0;
                Color pureHue = HslToRgb(h, satForPreview, 0.5);
                if (ChangeBgLightnessMidStop != null)
                {
                    ChangeBgLightnessMidStop.Color = pureHue;
                }

                if (ChangeBgCustomColorPreview != null)
                {
                    ChangeBgCustomColorPreview.Background = new SolidColorBrush(color);
                }
            }
            finally
            {
                _isUpdatingChangeBgColor = false;
            }
        }

        private void ChangeBgHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingChangeBgColor || ChangeBgHueSlider == null) return;
            double h = ChangeBgHueSlider.Value;
            double l = (ChangeBgLightnessSlider?.Value ?? 42) / 100.0;
            double s = _changeBgSaturation > 0.05 ? _changeBgSaturation : 1.0;
            Color color = HslToRgb(h, s, l);
            UpdateChangeBgControls(color, updateHexText: true, updateRgbText: true, updateSliders: false);
        }

        private void ChangeBgLightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingChangeBgColor || ChangeBgLightnessSlider == null) return;
            double h = ChangeBgHueSlider?.Value ?? 206;
            double l = ChangeBgLightnessSlider.Value / 100.0;
            double s = _changeBgSaturation > 0.05 ? _changeBgSaturation : 1.0;
            Color color = HslToRgb(h, s, l);
            UpdateChangeBgControls(color, updateHexText: true, updateRgbText: true, updateSliders: false);
        }

        private void ChangeBgCustomHexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingChangeBgColor || ChangeBgCustomHexInput == null) return;
            string text = ChangeBgCustomHexInput.Text.Trim();
            if (TryParseHexColor(text, out Color color))
            {
                UpdateChangeBgControls(color, updateHexText: false, updateRgbText: true, updateSliders: true);
            }
        }

        private void ChangeBgCustomRgbInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingChangeBgColor) return;
            if (ChangeBgCustomRInput == null || ChangeBgCustomGInput == null || ChangeBgCustomBInput == null) return;

            if (byte.TryParse(ChangeBgCustomRInput.Text.Trim(), out byte r) &&
                byte.TryParse(ChangeBgCustomGInput.Text.Trim(), out byte g) &&
                byte.TryParse(ChangeBgCustomBInput.Text.Trim(), out byte b))
            {
                Color color = Color.FromRgb(r, g, b);
                UpdateChangeBgControls(color, updateHexText: true, updateRgbText: false, updateSliders: true);
            }
        }

        private void OpenChangeBgColorPickerDlg_Click(object sender, RoutedEventArgs e)
        {
            string hex = ChangeBgCustomHexInput?.Text.Trim() ?? "";
            TryParseHexColor(hex, out Color current);
            if (ShowNativeColorPicker(current, out Color chosen))
            {
                SetChangeBgCustomColor($"#{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}");
            }
        }

        private void ApplyChangeBgColor_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null) return;

            Color targetColor;
            if (ChangeBgTransparentRadio.IsChecked == true)
            {
                targetColor = Colors.Transparent;
            }
            else if (ChangeBgDarkRadio.IsChecked == true)
            {
                targetColor = (Color)ColorConverter.ConvertFromString("#1E1E1E");
            }
            else if (ChangeBgCustomRadio.IsChecked == true)
            {
                string hex = ChangeBgCustomHexInput.Text.Trim();
                if (!TryParseHexColor(hex, out targetColor))
                {
                    System.Windows.MessageBox.Show("Please enter a valid hexadecimal color (e.g. #0078D4 or #FFF).",
                        "Invalid Color", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                targetColor = Colors.White;
            }

            int w = _currentImage.PixelWidth;
            int h = _currentImage.PixelHeight;

            BitmapSource newBgImage;
            if (_isCreatedCanvasMode)
            {
                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                if (targetColor == Colors.White)
                {
                    Array.Fill(pixels, (byte)255);
                }
                else if (targetColor != Colors.Transparent)
                {
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        pixels[i] = targetColor.B;
                        pixels[i + 1] = targetColor.G;
                        pixels[i + 2] = targetColor.R;
                        pixels[i + 3] = targetColor.A;
                    }
                }
                var blank = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                blank.Freeze();
                newBgImage = blank;
            }
            else
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    if (targetColor != Colors.Transparent)
                    {
                        dc.DrawRectangle(new SolidColorBrush(targetColor), null, new Rect(0, 0, w, h));
                    }
                    dc.DrawImage(_currentImage, new Rect(0, 0, w, h));
                }
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);
                rtb.Freeze();
                newBgImage = rtb;
            }

            Color oldBgColor = _currentCanvasBgColor;
            BitmapSource oldImage = _currentImage;

            _currentCanvasBgColor = targetColor;
            _currentImage = newBgImage;
            DisplayImage.Source = newBgImage;

            _historyManager.Record(new ChangeBgColorAction(this, oldImage, oldBgColor, newBgImage, targetColor));

            UpdateLayerListUI();
            CloseChangeBgColorModal();
        }

        public void InternalSetBaseImageAndBgColor(BitmapSource img, Color bgColor)
        {
            _currentImage = img;
            DisplayImage.Source = img;
            _currentCanvasBgColor = bgColor;
            UpdateLayerListUI();
        }

        #endregion
    }
}
