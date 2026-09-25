using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private readonly List<LayerItem> _layers = new();
        private LayerItem? _selectedLayerItem;

        // Compatibility accessors
        public IEnumerable<OverlayImageItem> _overlayItems => _layers.OfType<OverlayImageItem>();
        private OverlayImageItem? _selectedOverlayItem => _selectedLayerItem as OverlayImageItem;

        // Dragging overlay state
        private bool _isDraggingOverlay;
        private Point _dragStartCanvasPos;
        private Point _dragItemStartPos;

        // Resizing overlay state
        private bool _isResizingOverlay;
        private string _activeResizeHandle = "";
        private Point _resizeStartCanvasPos;
        private Rect _resizeStartRect;

        private void ImportImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null || !_isCreatedCanvasMode)
            {
                return;
            }

            if (_isCropping)
            {
                ExitCropMode();
            }

            var dlg = new OpenFileDialog
            {
                Title = "Select Image to Import",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    AddOverlayImageFromFile(file);
                }
            }
        }

        public void AddOverlayImageFromFile(string path, Point? dropPos = null)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                var bmp = ImageCompressor.LoadBitmapFromFile(path);
                AddOverlayImage(bmp, dropPos, name: Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to import image '{Path.GetFileName(path)}': {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void AddOverlayImage(BitmapSource bmp, Point? initialPos = null, double? customW = null, double? customH = null, string? name = null)
        {
            if (_currentImage == null || bmp == null)
            {
                return;
            }

            double canvasW = _currentImage.PixelWidth;
            double canvasH = _currentImage.PixelHeight;

            double targetW;
            double targetH;

            if (customW.HasValue && customH.HasValue && customW.Value > 0 && customH.Value > 0)
            {
                targetW = customW.Value;
                targetH = customH.Value;
            }
            else
            {
                targetW = bmp.PixelWidth;
                targetH = bmp.PixelHeight;

                // Scale down if larger than 60% of canvas dimensions
                double maxAllowedW = canvasW * 0.6;
                double maxAllowedH = canvasH * 0.6;

                if (targetW > maxAllowedW || targetH > maxAllowedH)
                {
                    double scale = Math.Min(maxAllowedW / targetW, maxAllowedH / targetH);
                    targetW = Math.Round(targetW * scale);
                    targetH = Math.Round(targetH * scale);
                }

                targetW = Math.Max(40, targetW);
                targetH = Math.Max(40, targetH);
            }

            double posX = initialPos?.X ?? Math.Max(0, (canvasW - targetW) / 2.0);
            double posY = initialPos?.Y ?? Math.Max(0, (canvasH - targetH) / 2.0);

            // Stagger if adding multiple items at center
            if (!initialPos.HasValue && _layers.Count > 0)
            {
                int offset = (_layers.Count % 8) * 20;
                posX = Math.Min(canvasW - targetW, posX + offset);
                posY = Math.Min(canvasH - targetH, posY + offset);
            }

            var item = new OverlayImageItem
            {
                Name = !string.IsNullOrWhiteSpace(name) ? GetUniqueLayerName(name) : GetUniqueLayerName("Layer 1"),
                Source = bmp,
                X = posX,
                Y = posY,
                Width = targetW,
                Height = targetH
            };

            // Build Visual Elements
            var visual = new Grid
            {
                Width = targetW,
                Height = targetH,
                Cursor = Cursors.SizeAll,
                Background = Brushes.Transparent
            };

            var imgControl = new Image
            {
                Source = bmp,
                Stretch = Stretch.Fill
            };
            RenderOptions.SetBitmapScalingMode(imgControl, BitmapScalingMode.HighQuality);
            visual.Children.Add(imgControl);

            // Selection outline border
            var selBorder = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                BorderThickness = new Thickness(1.5),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            visual.Children.Add(selBorder);

            // 4 Corner Resize Handles
            var handleTL = CreateCornerHandle("TL", Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-5, -5, 0, 0), item);
            var handleTR = CreateCornerHandle("TR", Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -5, -5, 0), item);
            var handleBL = CreateCornerHandle("BL", Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(-5, 0, 0, -5), item);
            var handleBR = CreateCornerHandle("BR", Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -5, -5), item);

            visual.Children.Add(handleTL);
            visual.Children.Add(handleTR);
            visual.Children.Add(handleBL);
            visual.Children.Add(handleBR);

            item.ImageVisualElement = visual;
            item.ImageControl = imgControl;
            item.SelectionBorder = selBorder;
            item.HandleTL = handleTL;
            item.HandleTR = handleTR;
            item.HandleBL = handleBL;
            item.HandleBR = handleBR;

            // Context Menu
            visual.ContextMenu = CreateOverlayContextMenu(item);
            visual.ContextMenuOpening += (s, e) =>
            {
                visual.ContextMenu = CreateOverlayContextMenu(item);
            };

            // Drag Mouse Handlers
            visual.MouseDown += (s, e) =>
            {
                if (_isPenActive)
                {
                    return;
                }

                // Check if any visible stroke layer with higher ZIndex was hit at this position
                Point clickPos = e.GetPosition(OverlayCanvas);
                var hitStrokeAbove = _layers.OfType<StrokeLayerItem>()
                    .Where(st => st.IsVisible && st.ZIndex > item.ZIndex)
                    .OrderByDescending(st => st.ZIndex)
                    .FirstOrDefault(st => st.HitTestPoint(clickPos));

                if (hitStrokeAbove != null)
                {
                    SelectLayerItem(hitStrokeAbove);
                    if (e.LeftButton == MouseButtonState.Pressed && !hitStrokeAbove.IsLocked)
                    {
                        StartDraggingStroke(hitStrokeAbove, clickPos, OverlayCanvas);
                    }
                    else if (e.RightButton == MouseButtonState.Pressed)
                    {
                        ShowStrokeContextMenu(hitStrokeAbove);
                    }
                    e.Handled = true;
                    return;
                }

                if (item.IsLocked)
                {
                    if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
                    {
                        SelectLayerItem(item);
                    }
                    return;
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    SelectLayerItem(item);
                    _isDraggingOverlay = true;
                    _dragStartCanvasPos = clickPos;
                    _dragItemStartPos = new Point(item.X, item.Y);
                    visual.CaptureMouse();
                    e.Handled = true;
                }
                else if (e.RightButton == MouseButtonState.Pressed)
                {
                    SelectLayerItem(item);
                }
            };

            visual.MouseMove += (s, e) =>
            {
                if (_isDraggingOverlay && _selectedLayerItem == item)
                {
                    Point cur = e.GetPosition(OverlayCanvas);
                    double dx = cur.X - _dragStartCanvasPos.X;
                    double dy = cur.Y - _dragStartCanvasPos.Y;

                    item.X = _dragItemStartPos.X + dx;
                    item.Y = _dragItemStartPos.Y + dy;

                    Canvas.SetLeft(visual, item.X);
                    Canvas.SetTop(visual, item.Y);
                    e.Handled = true;
                }
            };

            visual.MouseUp += (s, e) =>
            {
                if (_isDraggingOverlay && _selectedLayerItem == item)
                {
                    _isDraggingOverlay = false;
                    visual.ReleaseMouseCapture();

                    var oldRect = new Rect(_dragItemStartPos.X, _dragItemStartPos.Y, item.Width, item.Height);
                    var newRect = new Rect(item.X, item.Y, item.Width, item.Height);
                    if (Math.Abs(newRect.X - oldRect.X) > 0.5 || Math.Abs(newRect.Y - oldRect.Y) > 0.5)
                    {
                        _historyManager.Record(new TransformLayerAction(this, item, oldRect, newRect));
                    }
                    e.Handled = true;
                }
            };

            // Calculate initial Z-Index
            int maxZ = _layers.Count > 0 ? _layers.Max(x => x.ZIndex) : 0;
            item.ZIndex = maxZ + 1;
            Panel.SetZIndex(visual, item.ZIndex);

            Canvas.SetLeft(visual, posX);
            Canvas.SetTop(visual, posY);

            OverlayCanvas.Children.Add(visual);
            _layers.Add(item);

            // Record history action
            _historyManager.Record(new AddLayerAction(this, item));

            // Switch to Cursor tool to interact with newly imported image
            ActivateCursorMode();
            SelectLayerItem(item);
            UpdateLayerListUI();
        }

        private Border CreateCornerHandle(string name, Cursor cursor, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin, OverlayImageItem item)
        {
            var handle = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = margin,
                Cursor = cursor,
                Visibility = Visibility.Collapsed,
                Tag = name
            };

            handle.MouseDown += (s, e) =>
            {
                if (_isPenActive || item.IsLocked)
                {
                    return;
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    SelectLayerItem(item);
                    _isResizingOverlay = true;
                    _activeResizeHandle = name;
                    _resizeStartCanvasPos = e.GetPosition(OverlayCanvas);
                    _resizeStartRect = new Rect(item.X, item.Y, item.Width, item.Height);
                    handle.CaptureMouse();
                    e.Handled = true;
                }
            };

            handle.MouseMove += (s, e) =>
            {
                if (_isResizingOverlay && _selectedLayerItem == item)
                {
                    Point cur = e.GetPosition(OverlayCanvas);
                    double dx = cur.X - _resizeStartCanvasPos.X;
                    double dy = cur.Y - _resizeStartCanvasPos.Y;
                    double ar = item.AspectRatio > 0.001 ? item.AspectRatio : 1.0;

                    double newW = item.Width;
                    double newH = item.Height;
                    double newX = item.X;
                    double newY = item.Y;

                    switch (_activeResizeHandle)
                    {
                        case "BR":
                            newW = Math.Max(20, _resizeStartRect.Width + dx);
                            newH = Math.Max(20, Math.Round(newW / ar));
                            break;

                        case "BL":
                            newW = Math.Max(20, _resizeStartRect.Width - dx);
                            newH = Math.Max(20, Math.Round(newW / ar));
                            newX = _resizeStartRect.Right - newW;
                            break;

                        case "TR":
                            newW = Math.Max(20, _resizeStartRect.Width + dx);
                            newH = Math.Max(20, Math.Round(newW / ar));
                            newY = _resizeStartRect.Bottom - newH;
                            break;

                        case "TL":
                            newW = Math.Max(20, _resizeStartRect.Width - dx);
                            newH = Math.Max(20, Math.Round(newW / ar));
                            newX = _resizeStartRect.Right - newW;
                            newY = _resizeStartRect.Bottom - newH;
                            break;
                    }

                    item.Width = newW;
                    item.Height = newH;
                    item.X = newX;
                    item.Y = newY;

                    item.ImageVisualElement.Width = newW;
                    item.ImageVisualElement.Height = newH;
                    Canvas.SetLeft(item.ImageVisualElement, newX);
                    Canvas.SetTop(item.ImageVisualElement, newY);

                    e.Handled = true;
                }
            };

            handle.MouseUp += (s, e) =>
            {
                if (_isResizingOverlay && _selectedLayerItem == item)
                {
                    _isResizingOverlay = false;
                    handle.ReleaseMouseCapture();

                    var newRect = new Rect(item.X, item.Y, item.Width, item.Height);
                    if (Math.Abs(newRect.Width - _resizeStartRect.Width) > 0.5 ||
                        Math.Abs(newRect.Height - _resizeStartRect.Height) > 0.5 ||
                        Math.Abs(newRect.X - _resizeStartRect.X) > 0.5 ||
                        Math.Abs(newRect.Y - _resizeStartRect.Y) > 0.5)
                    {
                        _historyManager.Record(new TransformLayerAction(this, item, _resizeStartRect, newRect));
                    }
                    e.Handled = true;
                }
            };

            return handle;
        }

        private ContextMenu CreateOverlayContextMenu(OverlayImageItem item)
        {
            var menu = new ContextMenu();

            var itemCrop = new MenuItem
            {
                Header = "Crop Image",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Crop16, FontSize = 14 },
                IsEnabled = !item.IsLocked
            };
            itemCrop.Click += (s, e) => StartCropOverlayImage(item);
            menu.Items.Add(itemCrop);

            var itemRename = new MenuItem
            {
                Header = "Rename (F2)",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Rename20, FontSize = 14 }
            };
            itemRename.Click += (s, e) => StartRenameLayer(item);
            menu.Items.Add(itemRename);

            var itemClone = new MenuItem
            {
                Header = "Clone (Ctrl+D)",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Copy16, FontSize = 14 }
            };
            itemClone.Click += (s, e) => DuplicateLayer(item);
            menu.Items.Add(itemClone);

            var itemDel = new MenuItem
            {
                Header = "Delete (Del)",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Delete16, FontSize = 14 },
                IsEnabled = !item.IsLocked
            };
            itemDel.Click += (s, e) => DeleteLayer(item);
            menu.Items.Add(itemDel);

            menu.Items.Add(new Separator());

            var itemLock = new MenuItem
            {
                Header = item.IsLocked ? "Unlock Layer" : "Lock Layer",
                Icon = new SymbolIcon
                {
                    Symbol = item.IsLocked ? SymbolRegular.LockOpen16 : SymbolRegular.LockClosed16,
                    FontSize = 14
                }
            };
            itemLock.Click += (s, e) => ToggleLayerLock(item);
            menu.Items.Add(itemLock);

            return menu;
        }

        private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPenActive || _currentImage == null)
            {
                return;
            }

            Point clickPos = e.GetPosition(OverlayCanvas);

            // Check hit on strokes or selection box
            var hitStroke = HitTestStrokeAtPoint(clickPos);
            if (hitStroke != null)
            {
                SelectLayerItem(hitStroke);
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (!hitStroke.IsLocked)
                    {
                        StartDraggingStroke(hitStroke, clickPos, OverlayCanvas);
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.RightButton == MouseButtonState.Pressed)
                {
                    ShowStrokeContextMenu(hitStroke);
                    e.Handled = true;
                    return;
                }
            }

            if (e.OriginalSource == OverlayCanvas)
            {
                DeselectLayer();
            }
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingStroke)
            {
                Point cur = e.GetPosition(OverlayCanvas);
                ProcessDraggingStroke(cur);
                e.Handled = true;
                return;
            }

            if (!_isPenActive && _currentImage != null)
            {
                Point cur = e.GetPosition(OverlayCanvas);
                var hit = HitTestStrokeAtPoint(cur);
                if (hit != null && !hit.IsLocked)
                {
                    OverlayCanvas.Cursor = Cursors.SizeAll;
                }
                else
                {
                    OverlayCanvas.Cursor = Cursors.Arrow;
                }
            }
        }

        private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStroke)
            {
                FinishDraggingStroke(OverlayCanvas);
                e.Handled = true;
            }
        }

        private void OverlayCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDraggingStroke)
            {
                FinishDraggingStroke(OverlayCanvas);
            }
        }
    }
}
