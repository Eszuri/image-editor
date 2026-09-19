using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace ImageEditor
{
    public partial class MainWindow : FluentWindow
    {
        // Resize state
        private int _origResizeWidth;
        private int _origResizeHeight;
        private bool _isUpdatingResizeInputs;
        #region Image Resize Logic

        private void Resize_Click(object sender, RoutedEventArgs e)
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
            PenSettingsPopup.IsOpen = false;
            CompressMenuPopup.IsOpen = false;

            var (oldImage, oldStrokes, baseSource) = GetTransformBase();
            _origResizeWidth = baseSource.PixelWidth;
            _origResizeHeight = baseSource.PixelHeight;

            ResizeOriginalSizeText.Text = $"{_origResizeWidth} × {_origResizeHeight} px";

            _isUpdatingResizeInputs = true;
            ResizeLockAspectCheck.IsChecked = _appConfig.LastResizeMaintainAspectRatio;
            int initialW = _appConfig.LastResizeWidth > 0 ? _appConfig.LastResizeWidth : _origResizeWidth;
            int initialH = _appConfig.LastResizeHeight > 0 ? _appConfig.LastResizeHeight : _origResizeHeight;
            ResizeWidthInput.Text = initialW.ToString();
            ResizeHeightInput.Text = initialH.ToString();
            _isUpdatingResizeInputs = false;

            UpdateResizePreview();
            ResizeModal.Visibility = Visibility.Visible;
            ResizeWidthInput.Focus();
            ResizeWidthInput.SelectAll();
        }

        private void CloseResizeModal_Click(object sender, RoutedEventArgs e)
        {
            CloseResizeModal();
        }

        private void CloseResizeModal()
        {
            ResizeModal.Visibility = Visibility.Collapsed;
        }

        private void ResizeWidthInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingResizeInputs || _origResizeWidth <= 0 || _origResizeHeight <= 0)
            {
                return;
            }

            if (int.TryParse(ResizeWidthInput.Text, out int val) && val > 0)
            {
                if (ResizeLockAspectCheck.IsChecked == true)
                {
                    _isUpdatingResizeInputs = true;
                    int newH = (int)Math.Max(1, Math.Round((double)val * _origResizeHeight / _origResizeWidth));
                    ResizeHeightInput.Text = newH.ToString();
                    _isUpdatingResizeInputs = false;
                }
            }

            var (isValid, targetW, targetH) = GetTargetResizeDimensions();
            if (isValid)
            {
                _appConfig.LastResizeWidth = targetW;
                _appConfig.LastResizeHeight = targetH;
            }

            UpdateResizePreview();
        }

        private void ResizeHeightInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingResizeInputs || _origResizeWidth <= 0 || _origResizeHeight <= 0)
            {
                return;
            }

            if (int.TryParse(ResizeHeightInput.Text, out int val) && val > 0)
            {
                if (ResizeLockAspectCheck.IsChecked == true)
                {
                    _isUpdatingResizeInputs = true;
                    int newW = (int)Math.Max(1, Math.Round((double)val * _origResizeWidth / _origResizeHeight));
                    ResizeWidthInput.Text = newW.ToString();
                    _isUpdatingResizeInputs = false;
                }
            }

            var (isValid, targetW, targetH) = GetTargetResizeDimensions();
            if (isValid)
            {
                _appConfig.LastResizeWidth = targetW;
                _appConfig.LastResizeHeight = targetH;
            }

            UpdateResizePreview();
        }

        private void ResizeLockAspectCheck_Click(object sender, RoutedEventArgs e)
        {
            _appConfig.LastResizeMaintainAspectRatio = ResizeLockAspectCheck.IsChecked == true;
            _appConfig.Save();

            if (ResizeLockAspectCheck.IsChecked == true && _origResizeWidth > 0 && _origResizeHeight > 0)
            {
                if (int.TryParse(ResizeWidthInput.Text, out int val) && val > 0)
                {
                    _isUpdatingResizeInputs = true;
                    int newH = (int)Math.Max(1, Math.Round((double)val * _origResizeHeight / _origResizeWidth));
                    ResizeHeightInput.Text = newH.ToString();
                    _isUpdatingResizeInputs = false;
                }
                UpdateResizePreview();
            }
        }

        private (bool isValid, int targetW, int targetH) GetTargetResizeDimensions()
        {
            if (!int.TryParse(ResizeWidthInput?.Text, out int valW) || valW <= 0 ||
                !int.TryParse(ResizeHeightInput?.Text, out int valH) || valH <= 0)
            {
                return (false, 0, 0);
            }

            bool valid = valW > 0 && valH > 0 && valW <= 32768 && valH <= 32768;
            return (valid, valW, valH);
        }

        private void UpdateResizePreview()
        {
            if (ResizeNewSizeText == null || ApplyResizeBtn == null)
            {
                return;
            }

            var (isValid, targetW, targetH) = GetTargetResizeDimensions();
            if (!isValid || _origResizeWidth <= 0 || _origResizeHeight <= 0)
            {
                ResizeNewSizeText.Text = "Invalid dimensions";
                ApplyResizeBtn.IsEnabled = false;
                return;
            }

            ResizeNewSizeText.Text = $"{targetW} × {targetH} px";
            ApplyResizeBtn.IsEnabled = true;
        }

        private void ApplyResize_Click(object sender, RoutedEventArgs e)
        {
            var (isValid, targetW, targetH) = GetTargetResizeDimensions();
            if (!isValid || _currentImage == null)
            {
                return;
            }

            _appConfig.LastResizeWidth = targetW;
            _appConfig.LastResizeHeight = targetH;
            _appConfig.LastResizeMaintainAspectRatio = ResizeLockAspectCheck.IsChecked == true;
            _appConfig.Save();

            if (targetW == _origResizeWidth && targetH == _origResizeHeight)
            {
                CloseResizeModal();
                return;
            }

            var (oldImage, oldStrokes, baseSource) = GetTransformBase();
            double scaleX = (double)targetW / baseSource.PixelWidth;
            double scaleY = (double)targetH / baseSource.PixelHeight;

            var newImage = new TransformedBitmap(baseSource, new ScaleTransform(scaleX, scaleY));
            if (newImage.CanFreeze)
            {
                newImage.Freeze();
            }

            CloseResizeModal();
            ApplyImageTransform(oldImage, oldStrokes, newImage, Array.Empty<Stroke>());
        }

        #endregion
    }
}