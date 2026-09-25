using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private void NewCanvas_Click(object sender, RoutedEventArgs e)
        {
            OpenNewCanvasModal();
        }

        public void OpenNewCanvasModal()
        {
            if (_isCropping)
            {
                ExitCropMode();
            }

            int w = _appConfig.LastNewCanvasWidth > 0 ? _appConfig.LastNewCanvasWidth : 1920;
            int h = _appConfig.LastNewCanvasHeight > 0 ? _appConfig.LastNewCanvasHeight : 1080;
            NewCanvasWidthInput.Text = w.ToString();
            NewCanvasHeightInput.Text = h.ToString();

            string customColor = !string.IsNullOrWhiteSpace(_appConfig.LastNewCanvasCustomColor)
                ? _appConfig.LastNewCanvasCustomColor
                : "#0078D4";
            SetNewCanvasCustomColor(customColor);

            string bg = _appConfig.LastNewCanvasBg;
            if (bg == "Transparent")
            {
                NewCanvasBgTransparentRadio.IsChecked = true;
            }
            else if (bg == "Dark")
            {
                NewCanvasBgDarkRadio.IsChecked = true;
            }
            else if (bg == "Custom")
            {
                NewCanvasBgCustomRadio.IsChecked = true;
            }
            else
            {
                NewCanvasBgWhiteRadio.IsChecked = true;
            }
            UpdateNewCanvasCustomPanelVisibility();

            NewCanvasModal.Visibility = Visibility.Visible;
            NewCanvasWidthInput.Focus();
            NewCanvasWidthInput.SelectAll();
        }

        private void CloseNewCanvasModal_Click(object sender, RoutedEventArgs e)
        {
            CloseNewCanvasModal();
        }

        public void CloseNewCanvasModal()
        {
            NewCanvasModal.Visibility = Visibility.Collapsed;
        }

        private void NewCanvasPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string tag)
            {
                switch (tag)
                {
                    case "1920x1080":
                        NewCanvasWidthInput.Text = "1920";
                        NewCanvasHeightInput.Text = "1080";
                        break;
                    case "1280x720":
                        NewCanvasWidthInput.Text = "1280";
                        NewCanvasHeightInput.Text = "720";
                        break;
                    case "1080x1080":
                        NewCanvasWidthInput.Text = "1080";
                        NewCanvasHeightInput.Text = "1080";
                        break;
                    case "FitWindow":
                        double vw = ViewportGrid.ActualWidth > 20 ? ViewportGrid.ActualWidth : 1280;
                        double vh = ViewportGrid.ActualHeight > 20 ? ViewportGrid.ActualHeight : 720;
                        int fitW = Math.Max(100, (int)Math.Round(vw - 60));
                        int fitH = Math.Max(100, (int)Math.Round(vh - 60));
                        NewCanvasWidthInput.Text = fitW.ToString();
                        NewCanvasHeightInput.Text = fitH.ToString();
                        break;
                }
            }
        }

        private void ApplyNewCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(NewCanvasWidthInput.Text.Trim(), out int width) ||
                !int.TryParse(NewCanvasHeightInput.Text.Trim(), out int height) ||
                width < 10 || width > 16384 ||
                height < 10 || height > 16384)
            {
                MessageBox.Show(
                    "Please enter valid width and height between 10 and 16,384 pixels.",
                    "Invalid Dimensions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (HasEditingProgress())
            {
                var result = MessageBox.Show(
                    "You have unsaved edits on the current image. Creating a new canvas will discard your progress. Continue?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Color bgColor;
            string bgName;
            if (NewCanvasBgTransparentRadio.IsChecked == true)
            {
                bgColor = Colors.Transparent;
                bgName = "Transparent";
            }
            else if (NewCanvasBgDarkRadio.IsChecked == true)
            {
                bgColor = (Color)ColorConverter.ConvertFromString("#1E1E1E");
                bgName = "Dark";
            }
            else if (NewCanvasBgCustomRadio.IsChecked == true)
            {
                string hex = NewCanvasCustomHexInput.Text.Trim();
                if (!TryParseHexColor(hex, out bgColor))
                {
                    MessageBox.Show(
                        "Please enter a valid hexadecimal color (e.g., #0078D4, #FF5500, or #FFF).",
                        "Invalid Color",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                bgName = "Custom";
                _appConfig.LastNewCanvasCustomColor = hex.StartsWith("#") ? hex : "#" + hex;
            }
            else
            {
                bgColor = Colors.White;
                bgName = "White";
            }

            _appConfig.LastNewCanvasWidth = width;
            _appConfig.LastNewCanvasHeight = height;
            _appConfig.LastNewCanvasBg = bgName;
            _appConfig.Save();

            _currentCanvasBgColor = bgColor;

            // Create blank bitmap with BGRA32 pixel format
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            if (bgColor == Colors.White)
            {
                Array.Fill(pixels, (byte)255);
            }
            else if (bgColor != Colors.Transparent)
            {
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = bgColor.B;
                    pixels[i + 1] = bgColor.G;
                    pixels[i + 2] = bgColor.R;
                    pixels[i + 3] = bgColor.A;
                }
            }
            // For Transparent, byte[] is already 0 initialized

            var blankBitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            blankBitmap.Freeze();

            if (_isCropping)
            {
                ExitCropMode();
            }

            _currentImage = blankBitmap;
            _originalLoadedImage = blankBitmap;
            _currentPath = null;

            DisplayImage.Source = _currentImage;
            DisplayImage.Width = width;
            DisplayImage.Height = height;

            ImageContainer.Width = width;
            ImageContainer.Height = height;

            MainInkCanvas.Width = width;
            MainInkCanvas.Height = height;
            MainInkCanvas.Strokes.Clear();

            ShapePreviewCanvas.Width = width;
            ShapePreviewCanvas.Height = height;
            ShapePreviewCanvas.Children.Clear();

            UpdateCanvasClips(width, height);

            CropCanvas.Width = width;
            CropCanvas.Height = height;

            _savedUnappliedCropRect = null;
            _historyManager.Clear();
            UpdateHistoryButtonStates();
            ClearLayers();

            ImageContainer.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            UpdateImageInfoText();

            _isCreatedCanvasMode = true;
            SetControlsEnabled(true);
            SetLayerPanelVisible(true);
            UpdateLayerListUI();
            _isManualZoom = false;

            CloseNewCanvasModal();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitImageToViewport();
                ActivatePenMode();
            }), DispatcherPriority.Loaded);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct CHOOSECOLOR
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public int rgbResult;
            public IntPtr lpCustColors;
            public int Flags;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
        }

        private const int CC_RGBINIT = 0x00000001;
        private const int CC_FULLOPEN = 0x00000002;
        private const int CC_ANYCOLOR = 0x00000100;

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool ChooseColor(ref CHOOSECOLOR cc);

        private static readonly int[] s_customColors = new int[16];

        private bool _isUpdatingCustomColor;
        private double _customSaturation = 1.0;

        private void NewCanvasBgRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateNewCanvasCustomPanelVisibility();
        }

        private void UpdateNewCanvasCustomPanelVisibility()
        {
            if (NewCanvasCustomColorPanel != null)
            {
                NewCanvasCustomColorPanel.Visibility = (NewCanvasBgCustomRadio?.IsChecked == true)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void SetNewCanvasCustomColor(string hex)
        {
            if (TryParseHexColor(hex, out Color color))
            {
                UpdateCustomColorControls(color, updateHexText: true, updateRgbText: true, updateSliders: true);
            }
        }

        private void UpdateCustomColorControls(Color color, bool updateHexText, bool updateRgbText, bool updateSliders)
        {
            if (_isUpdatingCustomColor) return;
            _isUpdatingCustomColor = true;
            try
            {
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                if (updateHexText && NewCanvasCustomHexInput != null)
                {
                    NewCanvasCustomHexInput.Text = hex;
                }

                if (updateRgbText)
                {
                    if (NewCanvasCustomRInput != null) NewCanvasCustomRInput.Text = color.R.ToString();
                    if (NewCanvasCustomGInput != null) NewCanvasCustomGInput.Text = color.G.ToString();
                    if (NewCanvasCustomBInput != null) NewCanvasCustomBInput.Text = color.B.ToString();
                }

                RgbToHsl(color, out double h, out double s, out double l);

                bool isAchromatic = (s < 0.01 || l <= 0.001 || l >= 0.999);
                if (isAchromatic)
                {
                    if (NewCanvasHueSlider != null)
                    {
                        h = NewCanvasHueSlider.Value;
                    }
                }
                else
                {
                    _customSaturation = s;
                }

                if (updateSliders)
                {
                    if (!isAchromatic && NewCanvasHueSlider != null)
                    {
                        NewCanvasHueSlider.Value = Math.Round(h);
                    }
                    if (NewCanvasLightnessSlider != null)
                    {
                        NewCanvasLightnessSlider.Value = Math.Round(l * 100);
                    }
                }

                if (NewCanvasHueValueText != null)
                {
                    NewCanvasHueValueText.Text = $"{(int)Math.Round(h)}°";
                }
                if (NewCanvasLightnessValueText != null)
                {
                    NewCanvasLightnessValueText.Text = $"{(int)Math.Round(l * 100)}%";
                }

                double satForPreview = _customSaturation > 0.05 ? _customSaturation : 1.0;
                Color pureHue = HslToRgb(h, satForPreview, 0.5);
                if (NewCanvasLightnessMidStop != null)
                {
                    NewCanvasLightnessMidStop.Color = pureHue;
                }

                if (NewCanvasCustomColorPreview != null)
                {
                    NewCanvasCustomColorPreview.Background = new SolidColorBrush(color);
                }
            }
            finally
            {
                _isUpdatingCustomColor = false;
            }
        }

        private void NewCanvasHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingCustomColor || NewCanvasHueSlider == null) return;
            double h = NewCanvasHueSlider.Value;
            double l = (NewCanvasLightnessSlider?.Value ?? 42) / 100.0;
            double s = _customSaturation > 0.05 ? _customSaturation : 1.0;
            Color color = HslToRgb(h, s, l);
            UpdateCustomColorControls(color, updateHexText: true, updateRgbText: true, updateSliders: false);
        }

        private void NewCanvasLightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingCustomColor || NewCanvasLightnessSlider == null) return;
            double h = NewCanvasHueSlider?.Value ?? 206;
            double l = NewCanvasLightnessSlider.Value / 100.0;
            double s = _customSaturation > 0.05 ? _customSaturation : 1.0;
            Color color = HslToRgb(h, s, l);
            UpdateCustomColorControls(color, updateHexText: true, updateRgbText: true, updateSliders: false);
        }

        private void NewCanvasCustomHexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCustomColor || NewCanvasCustomHexInput == null) return;
            string text = NewCanvasCustomHexInput.Text.Trim();
            if (TryParseHexColor(text, out Color color))
            {
                UpdateCustomColorControls(color, updateHexText: false, updateRgbText: true, updateSliders: true);
            }
        }

        private void NewCanvasCustomRgbInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCustomColor) return;
            if (NewCanvasCustomRInput == null || NewCanvasCustomGInput == null || NewCanvasCustomBInput == null) return;

            if (byte.TryParse(NewCanvasCustomRInput.Text.Trim(), out byte r) &&
                byte.TryParse(NewCanvasCustomGInput.Text.Trim(), out byte g) &&
                byte.TryParse(NewCanvasCustomBInput.Text.Trim(), out byte b))
            {
                Color color = Color.FromRgb(r, g, b);
                UpdateCustomColorControls(color, updateHexText: true, updateRgbText: false, updateSliders: true);
            }
        }

        private bool ShowNativeColorPicker(Color initialColor, out Color chosenColor)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var cc = new CHOOSECOLOR();
            cc.lStructSize = Marshal.SizeOf(typeof(CHOOSECOLOR));
            cc.hwndOwner = helper.Handle;
            cc.rgbResult = initialColor.R | (initialColor.G << 8) | (initialColor.B << 16);

            GCHandle handle = GCHandle.Alloc(s_customColors, GCHandleType.Pinned);
            try
            {
                cc.lpCustColors = handle.AddrOfPinnedObject();
                cc.Flags = CC_RGBINIT | CC_FULLOPEN | CC_ANYCOLOR;

                if (ChooseColor(ref cc))
                {
                    byte r = (byte)(cc.rgbResult & 0xFF);
                    byte g = (byte)((cc.rgbResult >> 8) & 0xFF);
                    byte b = (byte)((cc.rgbResult >> 16) & 0xFF);
                    chosenColor = Color.FromRgb(r, g, b);
                    return true;
                }
            }
            finally
            {
                handle.Free();
            }

            chosenColor = initialColor;
            return false;
        }

        private void OpenColorPickerDlg_Click(object sender, RoutedEventArgs e)
        {
            string hex = NewCanvasCustomHexInput?.Text.Trim() ?? "";
            TryParseHexColor(hex, out Color current);
            if (ShowNativeColorPicker(current, out Color chosen))
            {
                SetNewCanvasCustomColor($"#{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}");
            }
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;
            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360.0);
                b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
            }
            return Color.FromRgb((byte)Math.Clamp(Math.Round(r * 255), 0, 255),
                                  (byte)Math.Clamp(Math.Round(g * 255), 0, 255),
                                  (byte)Math.Clamp(Math.Round(b * 255), 0, 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        private static void RgbToHsl(Color color, out double h, out double s, out double l)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = 0;
                s = 0;
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == r)
                    h = (g - b) / d + (g < b ? 6.0 : 0.0);
                else if (max == g)
                    h = (b - r) / d + 2.0;
                else
                    h = (r - g) / d + 4.0;

                h *= 60.0;
            }
        }

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            hex = hex.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;

            if (hex.Length == 4) // #RGB
            {
                hex = $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
            }
            else if (hex.Length == 5) // #RGBA
            {
                hex = $"#{hex[4]}{hex[4]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is Color c)
                {
                    color = c;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}

