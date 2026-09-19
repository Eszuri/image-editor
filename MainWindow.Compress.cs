using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace ImageEditor
{
    public partial class MainWindow : FluentWindow
    {
        // Single Image Compression state
        private BitmapSource? _originalLoadedImage;
        private enum CompressTarget { Edited, Original }
        private CompressTarget _compressTarget = CompressTarget.Edited;
        private string _compressFormat = "jpg";
        private long _originalFileSize;
        private byte[]? _lastCompressedData;
        private int _lastCompressedQuality = -1;
        private string _lastCompressedFormat = "";
        #region Image Compression Logic

        private void OpenCompressModal(CompressTarget target)
        {
            if (_currentImage == null)
            {
                return;
            }
            if (_isCropping)
            {
                ExitCropMode();
            }

            _compressTarget = target;
            UpdateCompressionTargetUI();

            // Automatically match output format to the loaded image
            string ext = !string.IsNullOrEmpty(_currentPath)
                ? System.IO.Path.GetExtension(_currentPath).ToLowerInvariant()
                : ".png";

            _compressFormat = (ext == ".jpg" || ext == ".jpeg") ? "jpg" : "png";

            // Restore last session slider value, default to 100% (original resolution/size)
            double savedSlider = _appConfig?.LastCompressSliderValue ?? 100.0;
            if (savedSlider < 10.0 || savedSlider > 100.0)
            {
                savedSlider = 100.0;
            }
            CompressQualitySlider.Value = savedSlider;

            CompressModal.Visibility = Visibility.Visible;
            UpdateCompressionEstimate();
        }

        private void CompressTargetEdited_Click(object sender, RoutedEventArgs e)
        {
            if (_compressTarget == CompressTarget.Edited)
            {
                return;
            }
            _compressTarget = CompressTarget.Edited;
            UpdateCompressionTargetUI();
            UpdateCompressionEstimate();
        }

        private void CompressTargetOriginal_Click(object sender, RoutedEventArgs e)
        {
            if (_compressTarget == CompressTarget.Original)
            {
                return;
            }
            _compressTarget = CompressTarget.Original;
            UpdateCompressionTargetUI();
            UpdateCompressionEstimate();
        }

        private void UpdateCompressionTargetUI()
        {
            if (CompressTargetEditedBtn == null || CompressTargetOriginalBtn == null)
            {
                return;
            }

            if (_compressTarget == CompressTarget.Edited)
            {
                CompressTargetEditedBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                CompressTargetOriginalBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                CompressResolutionLabel.Text = "Edited Canvas Resolution";
                CompressOriginalSizeLabel.Text = "Edited Baseline Size";

                BitmapSource activeSource = GetComposedBitmap();
                CompressResolutionText.Text = $"{activeSource.PixelWidth} × {activeSource.PixelHeight} px";
                _originalFileSize = GetSourceBaselineSize(activeSource, _compressFormat);
                CompressOriginalSizeText.Text = ImageCompressor.FormatBytes(_originalFileSize);
            }
            else
            {
                CompressTargetEditedBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                CompressTargetOriginalBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                CompressResolutionLabel.Text = "Original File Resolution";
                CompressOriginalSizeLabel.Text = "Original File Size";

                BitmapSource origSource = _originalLoadedImage ?? _currentImage!;
                CompressResolutionText.Text = $"{origSource.PixelWidth} × {origSource.PixelHeight} px";
                _originalFileSize = GetOriginalFileSize();
                CompressOriginalSizeText.Text = ImageCompressor.FormatBytes(_originalFileSize);
            }
            _lastCompressedData = null;
        }

        private BitmapSource GetCurrentCompressSource()
        {
            if (_compressTarget == CompressTarget.Original)
            {
                return _originalLoadedImage ?? _currentImage!;
            }
            return GetComposedBitmap();
        }

        private long GetSourceBaselineSize(BitmapSource source, string format)
        {
            if (_compressTarget == CompressTarget.Original ||
                (source == _originalLoadedImage && MainInkCanvas.Strokes.Count == 0 && !_historyManager.CanUndo))
            {
                return GetOriginalFileSize();
            }

            try
            {
                return format == "jpg"
                    ? ImageCompressor.EncodeJpeg(source, 100).Length
                    : ImageCompressor.EncodePng(source).Length;
            }
            catch
            {
                return GetOriginalFileSize();
            }
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
            if (CompressQualityValueText == null || CompressQualitySlider == null || _currentImage == null)
            {
                return;
            }

            if (_appConfig != null)
            {
                _appConfig.LastCompressSliderValue = Math.Round(e.NewValue);
                _appConfig.Save();
            }

            UpdateCompressionEstimate();
        }

        private void CompressMinus_Click(object sender, RoutedEventArgs e)
        {
            if (CompressQualitySlider == null)
            {
                return;
            }
            if (CompressQualitySlider.Value > CompressQualitySlider.Minimum)
            {
                CompressQualitySlider.Value = Math.Max(CompressQualitySlider.Minimum, CompressQualitySlider.Value - 1);
            }
        }

        private void CompressPlus_Click(object sender, RoutedEventArgs e)
        {
            if (CompressQualitySlider == null)
            {
                return;
            }
            if (CompressQualitySlider.Value < CompressQualitySlider.Maximum)
            {
                CompressQualitySlider.Value = Math.Min(CompressQualitySlider.Maximum, CompressQualitySlider.Value + 1);
            }
        }

        private void UpdateCompressionEstimate()
        {
            if (_currentImage == null || CompressNewSizeText == null)
            {
                return;
            }

            int percent = (int)Math.Round(CompressQualitySlider.Value);
            CompressQualityValueText.Text = $"{percent}%";

            if (CompressMinusBtn != null && CompressQualitySlider != null)
            {
                CompressMinusBtn.IsEnabled = percent > (int)CompressQualitySlider.Minimum;
            }
            if (CompressPlusBtn != null && CompressQualitySlider != null)
            {
                CompressPlusBtn.IsEnabled = percent < (int)CompressQualitySlider.Maximum;
            }

            BitmapSource baseSource = GetCurrentCompressSource();

            if (_originalFileSize <= 0)
            {
                _originalFileSize = (_compressTarget == CompressTarget.Original)
                    ? GetOriginalFileSize()
                    : GetSourceBaselineSize(baseSource, _compressFormat);
            }

            if (percent >= 100)
            {
                _lastCompressedData = null;
                _lastCompressedQuality = 100;
                _lastCompressedFormat = _compressFormat;

                CompressNewSizeText.Text = ImageCompressor.FormatBytes(_originalFileSize);
                CompressReductionText.Text = " (Original)";
                CompressReductionText.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
                return;
            }

            try
            {
                byte[] data = ImageCompressor.CompressByQuality(baseSource, _compressFormat, percent);
                _lastCompressedData = data;
                _lastCompressedQuality = percent;
                _lastCompressedFormat = _compressFormat;

                long actualBytes = data.Length;
                CompressNewSizeText.Text = ImageCompressor.FormatBytes(actualBytes);

                if (_originalFileSize > 0)
                {
                    double diff = (1.0 - ((double)actualBytes / _originalFileSize)) * 100.0;
                    if (diff > 0.5)
                    {
                        CompressReductionText.Text = $" (-{diff:F0}%)";
                        CompressReductionText.Foreground =
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    }
                    else if (diff < -0.5)
                    {
                        CompressReductionText.Text = $" (+{-diff:F0}%)";
                        CompressReductionText.Foreground =
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                    }
                    else
                    {
                        CompressReductionText.Text = " (0%)";
                        CompressReductionText.Foreground =
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
                    }
                }
            }
            catch { }
        }

        private void SaveCompressed_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            string ext = ".png";
            if (_compressFormat == "jpg")
            {
                bool isJpeg = !string.IsNullOrEmpty(_currentPath) &&
                              System.IO.Path.GetExtension(_currentPath).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
                ext = isJpeg ? ".jpeg" : ".jpg";
            }

            string filter = _compressFormat == "jpg"
                ? "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg"
                : "PNG Image (*.png)|*.png";

            string suffix = (_compressTarget == CompressTarget.Original) ? "_original_compressed" : "_compressed";
            string baseName = string.IsNullOrEmpty(_currentPath)
                ? (_compressTarget == CompressTarget.Original ? "original_compressed" : "compressed_image")
                : System.IO.Path.GetFileNameWithoutExtension(_currentPath) + suffix;

            var dlg = new SaveFileDialog
            {
                Title = _compressTarget == CompressTarget.Original
                    ? "Save Compressed Original Image"
                    : "Save Compressed Edited Image",
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

                    bool canDirectCopyOriginal = percent >= 100 &&
                                                 _compressTarget == CompressTarget.Original &&
                                                 !string.IsNullOrEmpty(_currentPath) &&
                                                 File.Exists(_currentPath);
                    if (canDirectCopyOriginal)
                    {
                        File.Copy(_currentPath!, dlg.FileName, true);
                        CloseCompressModal();
                        System.Windows.MessageBox.Show(
                            "Image compressed and saved successfully.",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    bool canDirectCopyEdited = percent >= 100 &&
                                               _compressTarget == CompressTarget.Edited &&
                                               MainInkCanvas.Strokes.Count == 0 &&
                                               !_historyManager.CanUndo &&
                                               !string.IsNullOrEmpty(_currentPath) &&
                                               File.Exists(_currentPath);
                    if (canDirectCopyEdited)
                    {
                        File.Copy(_currentPath!, dlg.FileName, true);
                        CloseCompressModal();
                        System.Windows.MessageBox.Show(
                            "Image compressed and saved successfully.",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    bool isCached = _lastCompressedData != null &&
                                    _lastCompressedQuality == percent &&
                                    _lastCompressedFormat == _compressFormat;
                    if (isCached)
                    {
                        data = _lastCompressedData!;
                    }
                    else
                    {
                        BitmapSource baseSource = GetCurrentCompressSource();
                        data = ImageCompressor.CompressByQuality(baseSource, _compressFormat, percent);
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
            CompressSubMenuBorder.Visibility = Visibility.Collapsed;
            CompressCurrentItem.Background = Brushes.Transparent;
            CompressMenuPopup.IsOpen = true;
        }

        private void CompressCurrentItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            e.Handled = true;

            if (CompressSubMenuBorder.Visibility == Visibility.Visible)
            {
                CompressSubMenuBorder.Visibility = Visibility.Collapsed;
                CompressCurrentItem.Background = Brushes.Transparent;
            }
            else
            {
                CompressSubMenuBorder.Visibility = Visibility.Visible;
                CompressCurrentItem.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#303030"));
            }
        }

        private void CompressEditedItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            CompressCurrentItem.Background = Brushes.Transparent;
            CompressSubMenuBorder.Visibility = Visibility.Collapsed;
            CompressMenuPopup.IsOpen = false;
            OpenCompressModal(CompressTarget.Edited);
        }

        private void CompressOriginalItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }
            CompressCurrentItem.Background = Brushes.Transparent;
            CompressSubMenuBorder.Visibility = Visibility.Collapsed;
            CompressMenuPopup.IsOpen = false;
            OpenCompressModal(CompressTarget.Original);
        }

        private void BatchCompressItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CompressCurrentItem.Background = Brushes.Transparent;
            CompressSubMenuBorder.Visibility = Visibility.Collapsed;
            CompressMenuPopup.IsOpen = false;
            OpenBatchCompressModal();
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

        #endregion
    }
}