using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private bool _isLayerPanelOpen = true;
        private double _sliderStartOpacity = 1.0;

        private void LayersToggle_Click(object sender, RoutedEventArgs e)
        {
            SetLayerPanelVisible(!_isLayerPanelOpen);
        }

        private void CloseLayerPanel_Click(object sender, RoutedEventArgs e)
        {
            SetLayerPanelVisible(false);
        }

        public void SetLayerPanelVisible(bool visible)
        {
            _isLayerPanelOpen = visible;
            RightSidebarBorder.Visibility = (visible && _isCreatedCanvasMode) ? Visibility.Visible : Visibility.Collapsed;
            LayersToggleBtn.Appearance = visible ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }

        public void UpdateLayerListUI()
        {
            if (LayersListPanel == null)
            {
                return;
            }

            if (_isRenamingActive && _selectedLayerItem != null && _layerRenameControls.TryGetValue(_selectedLayerItem, out var activeRenameCtrl))
            {
                CommitRenameLayer(_selectedLayerItem, activeRenameCtrl.nameBox.Text);
            }
            _layerRenameControls.Clear();
            LayersListPanel.Children.Clear();
            LayersCountBadge.Text = _layers.Count.ToString();

            // Sync Opacity Slider with currently selected item
            if (_selectedLayerItem != null)
            {
                LayerOpacitySlider.IsEnabled = !_selectedLayerItem.IsLocked;
                LayerOpacitySlider.Value = Math.Round(_selectedLayerItem.Opacity * 100);
                LayerOpacityText.Text = $"{(int)Math.Round(_selectedLayerItem.Opacity * 100)}%";
                _sliderStartOpacity = _selectedLayerItem.Opacity;
            }
            else
            {
                LayerOpacitySlider.IsEnabled = false;
                LayerOpacitySlider.Value = 100;
                LayerOpacityText.Text = "—";
            }

            // Display in visual stacking order (topmost layer on top)
            foreach (var item in _layers.OrderByDescending(x => x.ZIndex))
            {
                var card = CreateLayerCard(item);
                LayersListPanel.Children.Add(card);
            }

            // Canvas Background Base Card at the bottom of the stack
            if (_currentImage != null)
            {
                var baseCard = CreateBaseCanvasCard();
                LayersListPanel.Children.Add(baseCard);
            }

            UpdateMergeButtonVisibility();
        }

        private Border CreateLayerCard(LayerItem item)
        {
            bool isMultiSelected = _selectedLayers.Count > 1 && _selectedLayers.Contains(item);
            bool isSelected = _selectedLayers.Contains(item) || item == _selectedLayerItem;

            var card = new Border
            {
                Background = isMultiSelected
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3B53"))
                    : isSelected
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#253545"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1C1C")),
                BorderBrush = isMultiSelected
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0098FF"))
                    : isSelected
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2C2C")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 5),
                Cursor = Cursors.Hand,
                Tag = item
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // Col 0: Visibility Eye
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // Col 1: Lock Toggle
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Col 2: Thumbnail
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Col 3: Name & Dimensions

            // 1. Visibility Button (Eye)
            var eyeIcon = new SymbolIcon
            {
                Symbol = item.IsVisible ? SymbolRegular.Eye16 : SymbolRegular.EyeOff16,
                FontSize = 14,
                Foreground = item.IsVisible ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777777")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = item.IsVisible ? "Hide Layer" : "Show Layer"
            };

            var eyeBtn = new Border
            {
                Background = Brushes.Transparent,
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                Tag = "ActionBtn",
                Child = eyeIcon
            };

            eyeBtn.MouseDown += (s, e) =>
            {
                bool oldVis = item.IsVisible;
                item.IsVisible = !oldVis;
                _historyManager.Record(new LayerVisibilityAction(this, item, oldVis, item.IsVisible));
                UpdateLayerListUI();
                e.Handled = true;
            };

            Grid.SetColumn(eyeBtn, 0);
            grid.Children.Add(eyeBtn);

            // 2. Lock Button
            var lockIcon = new SymbolIcon
            {
                Symbol = item.IsLocked ? SymbolRegular.LockClosed16 : SymbolRegular.LockOpen16,
                FontSize = 13,
                Foreground = item.IsLocked
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5A93C"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = item.IsLocked ? "Locked (Click to Unlock)" : "Unlocked (Click to Lock)"
            };

            var lockBtn = new Border
            {
                Background = Brushes.Transparent,
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                Tag = "ActionBtn",
                Child = lockIcon
            };

            lockBtn.MouseDown += (s, e) =>
            {
                ToggleLayerLock(item);
                e.Handled = true;
            };

            Grid.SetColumn(lockBtn, 1);
            grid.Children.Add(lockBtn);

            // 3. Thumbnail
            var thumb = item.CreateThumbnailElement();
            Grid.SetColumn(thumb, 2);
            grid.Children.Add(thumb);

            // 4. Name & Dimensions
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            var nameText = new TextBlock
            {
                Text = item.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = item.IsVisible ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = "Double-click to rename"
            };

            var nameBox = new System.Windows.Controls.TextBox
            {
                Text = item.Name,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141414")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3, 1, 3, 1),
                Height = 22,
                Margin = new Thickness(0, 0, 4, 2),
                Visibility = Visibility.Collapsed,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "ActionBtn"
            };

            nameBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitRenameLayer(item, nameBox.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelRenameLayer(item);
                    e.Handled = true;
                }
            };

            nameBox.LostFocus += (s, e) =>
            {
                if (_isRenamingActive)
                {
                    CommitRenameLayer(item, nameBox.Text);
                }
            };

            nameBox.MouseDown += (s, e) => e.Handled = true;

            RegisterLayerRenameControls(item, nameText, nameBox);

            textStack.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                {
                    StartRenameLayer(item);
                    e.Handled = true;
                }
            };

            var dimText = new TextBlock
            {
                Text = item.DimensionsText,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"))
            };

            textStack.Children.Add(nameText);
            textStack.Children.Add(nameBox);
            textStack.Children.Add(dimText);

            Grid.SetColumn(textStack, 3);
            grid.Children.Add(textStack);

            card.Child = grid;

            // Context Menu on layer card
            if (item is OverlayImageItem overlayImg)
            {
                card.ContextMenu = CreateOverlayContextMenu(overlayImg);
                card.ContextMenuOpening += (s, e) =>
                {
                    card.ContextMenu = CreateOverlayContextMenu(overlayImg);
                };
            }
            else if (item is StrokeLayerItem strokeItem)
            {
                card.ContextMenuOpening += (s, e) =>
                {
                    card.ContextMenu = CreateStrokeContextMenu(strokeItem);
                };
            }

            // Click card to select on canvas or multi-select
            card.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    HandleLayerCardClick(item, e);
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    if (!_selectedLayers.Contains(item))
                    {
                        SelectSingleLayer(item);
                    }
                    UpdateLayerSelectionVisuals();
                }
            };

            AttachCardDragDropHandlers(card, item);

            return card;
        }

        private ContextMenu CreateStrokeContextMenu(StrokeLayerItem item)
        {
            var menu = new ContextMenu();

            if (item.IsMerged)
            {
                var itemSplit = new MenuItem
                {
                    Header = $"Split Layer ({item.OriginalLayers.Count} Layers)",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowSplit20, FontSize = 14 },
                    IsEnabled = !item.IsLocked
                };
                itemSplit.Click += (s, e) => SplitStrokeLayer(item);
                menu.Items.Add(itemSplit);

                menu.Items.Add(new Separator());
            }

            if (_selectedStrokeLayers.Count >= 2 && _selectedStrokeLayers.Contains(item))
            {
                var itemMerge = new MenuItem
                {
                    Header = $"Merge ({_selectedStrokeLayers.Count} Layers)",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Merge20, FontSize = 14 }
                };
                itemMerge.Click += (s, e) => MergeSelectedStrokeLayers();
                menu.Items.Add(itemMerge);

                menu.Items.Add(new Separator());
            }

            var itemRename = new MenuItem
            {
                Header = "Rename (F2)",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Rename20, FontSize = 14 }
            };
            itemRename.Click += (s, e) => StartRenameLayer(item);
            menu.Items.Add(itemRename);

            var itemDup = new MenuItem
            {
                Header = "Duplicate (Ctrl+D)",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Copy16, FontSize = 14 }
            };
            itemDup.Click += (s, e) => DuplicateLayer(item);
            menu.Items.Add(itemDup);

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

        private Border CreateBaseCanvasCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141414")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#282828")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 4, 0, 0),
                Opacity = 0.8
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lockIcon = new SymbolIcon
            {
                Symbol = SymbolRegular.LockClosed16,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Base Background Layer (Locked)"
            };
            Grid.SetColumn(lockIcon, 1);
            grid.Children.Add(lockIcon);

            var thumbBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(4),
                Background = _currentCanvasBgColor == Colors.Transparent
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"))
                    : new SolidColorBrush(_currentCanvasBgColor),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2, 0, 2, 0)
            };
            if (_currentCanvasBgColor == Colors.Transparent)
            {
                thumbBorder.Child = new TextBlock
                {
                    Text = "⛶",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"))
                };
            }
            Grid.SetColumn(thumbBorder, 2);
            grid.Children.Add(thumbBorder);

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            var nameText = new TextBlock
            {
                Text = "Background",
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"))
            };

            var dimText = new TextBlock
            {
                Text = $"{_currentImage!.PixelWidth} × {_currentImage.PixelHeight} px",
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))
            };

            textStack.Children.Add(nameText);
            textStack.Children.Add(dimText);

            Grid.SetColumn(textStack, 3);
            grid.Children.Add(textStack);

            card.Child = grid;
            card.ContextMenu = CreateBaseLayerContextMenu();
            card.MouseRightButtonUp += (s, e) =>
            {
                card.ContextMenu.IsOpen = true;
                e.Handled = true;
            };
            card.Cursor = Cursors.Hand;
            card.ToolTip = "Base Background Layer (Right-click for options: Edit Dimensions, Change Background Color)";
            return card;
        }


        public void UpdateLayerSelectionVisuals()
        {
            if (LayersListPanel == null)
            {
                return;
            }

            foreach (var child in LayersListPanel.Children)
            {
                if (child is Border card && card.Tag is LayerItem item)
                {
                    bool isMultiSel = _selectedLayers.Count > 1 && _selectedLayers.Contains(item);
                    bool isSel = _selectedLayers.Contains(item) || item == _selectedLayerItem;

                    if (isMultiSel)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3B53"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0098FF"));
                    }
                    else if (isSel)
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#253545"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
                    }
                    else
                    {
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1C1C"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2C2C"));
                    }
                }
            }

            if (_selectedLayerItem != null)
            {
                LayerOpacitySlider.IsEnabled = !_selectedLayerItem.IsLocked;
                LayerOpacitySlider.Value = Math.Round(_selectedLayerItem.Opacity * 100);
                LayerOpacityText.Text = $"{(int)Math.Round(_selectedLayerItem.Opacity * 100)}%";
                _sliderStartOpacity = _selectedLayerItem.Opacity;
            }
            else
            {
                LayerOpacitySlider.IsEnabled = false;
                LayerOpacitySlider.Value = 100;
                LayerOpacityText.Text = "—";
            }

            UpdateMergeButtonVisibility();
        }

        private void LayerOpacitySlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_selectedLayerItem != null)
            {
                _sliderStartOpacity = _selectedLayerItem.Opacity;
            }
        }

        private void LayerOpacitySlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                double currentOp = LayerOpacitySlider.Value / 100.0;
                if (Math.Abs(currentOp - _sliderStartOpacity) > 0.01)
                {
                    _historyManager.Record(new LayerOpacityAction(this, _selectedLayerItem, _sliderStartOpacity, currentOp));
                    _sliderStartOpacity = currentOp;
                }
            }
        }

        private void LayerOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                _selectedLayerItem.Opacity = e.NewValue / 100.0;
                if (LayerOpacityText != null)
                {
                    LayerOpacityText.Text = $"{(int)Math.Round(e.NewValue)}%";
                }
            }
        }
    }
}
