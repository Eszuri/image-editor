using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace ImageEditor
{
    public partial class MainWindow : FluentWindow
    {
        // Batch Compression state
        private readonly ObservableCollection<BatchItem> _batchItems = new();
        private CancellationTokenSource? _batchCts;
        private bool _isBatchProcessing;
        private string? _lastBatchOutputFolder;
        private static readonly HashSet<string> SupportedBatchExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".webp"
        };
        #region Batch Compression Logic

        private void OpenBatchCompressModal()
        {
            if (_isCropping)
            {
                ExitCropMode();
            }
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
                                         .Where(f => SupportedBatchExtensions.Contains(
                                             System.IO.Path.GetExtension(f)));
                    AddFilesToBatch(files);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to scan folder: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void BatchClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchProcessing)
            {
                return;
            }
            _batchItems.Clear();
            UpdateBatchSummary();
            BatchProgressBar.Value = 0;
            BatchProgressStatusText.Text = "Queue cleared";
            BatchProgressPercentText.Text = "";
            BatchOpenFolderBtn.Visibility = Visibility.Collapsed;
        }

        private void BatchRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchProcessing)
            {
                return;
            }
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
                            {
                                allFiles.Add(p);
                            }
                        }
                        else if (Directory.Exists(p))
                        {
                            try
                            {
                                var dirFiles = Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                                                        .Where(f => SupportedBatchExtensions.Contains(
                                                            System.IO.Path.GetExtension(f)));
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
                if (existing.Contains(path))
                {
                    continue;
                }

                try
                {
                    var fi = new FileInfo(path);
                    if (!fi.Exists)
                    {
                        continue;
                    }

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
            if (BatchSliderControls == null || BatchTargetSizeControls == null || BatchPercentageControls == null)
            {
                return;
            }

            bool isSlider = BatchModeSliderRadio?.IsChecked == true;
            bool isTarget = BatchModeTargetSizeRadio?.IsChecked == true;
            bool isPercent = BatchModePercentageRadio?.IsChecked == true;

            BatchSliderControls.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
            BatchTargetSizeControls.Visibility = isTarget ? Visibility.Visible : Visibility.Collapsed;
            BatchPercentageControls.Visibility = isPercent ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BatchQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BatchSliderValueText == null)
            {
                return;
            }
            int val = (int)Math.Round(e.NewValue);
            BatchSliderValueText.Text = $"{val}%";

            if (BatchSliderMinusBtn != null && BatchQualitySlider != null)
            {
                BatchSliderMinusBtn.IsEnabled = val > (int)BatchQualitySlider.Minimum;
            }
            if (BatchSliderPlusBtn != null && BatchQualitySlider != null)
            {
                BatchSliderPlusBtn.IsEnabled = val < (int)BatchQualitySlider.Maximum;
            }
        }

        private void BatchSliderMinus_Click(object sender, RoutedEventArgs e)
        {
            if (BatchQualitySlider == null)
            {
                return;
            }
            if (BatchQualitySlider.Value > BatchQualitySlider.Minimum)
            {
                BatchQualitySlider.Value = Math.Max(BatchQualitySlider.Minimum, BatchQualitySlider.Value - 1);
            }
        }

        private void BatchSliderPlus_Click(object sender, RoutedEventArgs e)
        {
            if (BatchQualitySlider == null)
            {
                return;
            }
            if (BatchQualitySlider.Value < BatchQualitySlider.Maximum)
            {
                BatchQualitySlider.Value = Math.Min(BatchQualitySlider.Maximum, BatchQualitySlider.Value + 1);
            }
        }

        private void BatchPercentageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BatchPercentageValueText == null)
            {
                return;
            }
            int val = (int)Math.Round(e.NewValue);
            BatchPercentageValueText.Text = $"{val}%";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
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
            if (_isBatchProcessing || _batchItems.Count == 0)
            {
                return;
            }

            // Validate Destination
            bool isCustom = BatchDestCustomRadio.IsChecked == true;
            bool isOverwrite = BatchDestOverwriteRadio.IsChecked == true;
            string customFolder = BatchCustomFolderInput.Text.Trim();
            string subfolderName = BatchSubfolderNameInput.Text.Trim();
            if (string.IsNullOrEmpty(subfolderName))
            {
                subfolderName = "_compressed";
            }

            if (isCustom)
            {
                if (string.IsNullOrEmpty(customFolder))
                {
                    System.Windows.MessageBox.Show(
                        "Please select or enter a valid custom output folder.",
                        "Folder Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    if (!Directory.Exists(customFolder))
                    {
                        Directory.CreateDirectory(customFolder);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Cannot use custom folder: {ex.Message}",
                        "Invalid Folder",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            if (isOverwrite)
            {
                var confirm = System.Windows.MessageBox.Show(
                    "Warning: Overwrite original files is selected!\n\n" +
                    "This will permanently replace your original image files " +
                    "with the compressed versions. Are you sure you want to proceed?",
                    "Confirm Overwrite",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }
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
                    System.Windows.MessageBox.Show(
                        "Please enter a valid positive target size number.",
                        "Invalid Target Size",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
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
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

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
                        if (!Directory.Exists(outDir))
                        {
                            Directory.CreateDirectory(outDir);
                        }
                        outPath = System.IO.Path.Combine(outDir, item.FileName);
                    }

                    if (string.IsNullOrEmpty(firstOutputDir))
                    {
                        firstOutputDir = outDir;
                    }

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
                                compressedData = ImageCompressor.CompressToTargetSize(
                                    bmp, format, targetBytes, item.OriginalSize);
                            }
                            else // isPercentageMode
                            {
                                compressedData = ImageCompressor.CompressToPercentageOfSize(
                                    bmp, format, percentageRatio, item.OriginalSize);
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
                    BatchProgressStatusText.Text =
                        $"Finished {total} files! {savedStr}. " +
                        $"Success: {successCount}, Skipped: {skippedCount}, Errors: {errorCount}";
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
                    System.Windows.MessageBox.Show(
                        $"Could not open folder: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}