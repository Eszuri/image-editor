using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private bool _isReorderingLayer;
        private Border? _dragCandidateCard;
        private LayerItem? _dragCandidateLayer;
        private Point _dragStartMousePos;

        private readonly Border _dropIndicator = new Border
        {
            Height = 3,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(6, 1, 6, 1),
            IsHitTestVisible = false
        };

        public void AttachCardDragDropHandlers(Border card, LayerItem item)
        {
            if (card == null || item == null) return;

            card.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (IsInsideActionButton(e.OriginalSource as DependencyObject))
                {
                    _dragCandidateCard = null;
                    _dragCandidateLayer = null;
                    return;
                }

                _dragCandidateCard = card;
                _dragCandidateLayer = item;
                _dragStartMousePos = e.GetPosition(LayersListPanel);
                _isReorderingLayer = false;
            };

            card.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragCandidateCard != card || item.IsLocked)
                {
                    return;
                }

                Point curPos = e.GetPosition(LayersListPanel);

                if (!_isReorderingLayer)
                {
                    double dy = Math.Abs(curPos.Y - _dragStartMousePos.Y);
                    double dx = Math.Abs(curPos.X - _dragStartMousePos.X);

                    if (dy > SystemParameters.MinimumVerticalDragDistance || dx > SystemParameters.MinimumHorizontalDragDistance)
                    {
                        _isReorderingLayer = true;
                        card.Opacity = 0.45;
                        card.CaptureMouse();
                        UpdateDropIndicator(curPos);
                    }
                }
                else
                {
                    UpdateDropIndicator(curPos);
                }
            };

            card.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_isReorderingLayer && _dragCandidateCard == card)
                {
                    _isReorderingLayer = false;
                    card.ReleaseMouseCapture();
                    card.Opacity = 1.0;

                    if (LayersListPanel != null && LayersListPanel.Children.Contains(_dropIndicator))
                    {
                        LayersListPanel.Children.Remove(_dropIndicator);
                    }

                    Point curPos = e.GetPosition(LayersListPanel);
                    ExecuteLayerDrop(item, curPos);
                    e.Handled = true;
                }

                _dragCandidateCard = null;
                _dragCandidateLayer = null;
            };

            card.LostMouseCapture += (s, e) =>
            {
                if (_isReorderingLayer)
                {
                    _isReorderingLayer = false;
                    card.Opacity = 1.0;
                    if (LayersListPanel != null && LayersListPanel.Children.Contains(_dropIndicator))
                    {
                        LayersListPanel.Children.Remove(_dropIndicator);
                    }
                }
                _dragCandidateCard = null;
                _dragCandidateLayer = null;
            };
        }

        private static bool IsInsideActionButton(DependencyObject? obj)
        {
            while (obj != null && !(obj is Border b && b.Tag is LayerItem))
            {
                if (obj is TextBox || (obj is FrameworkElement fe && (string?)fe.Tag == "ActionBtn"))
                {
                    return true;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private int CalculateDropSlot(Point curPos)
        {
            if (LayersListPanel == null) return 0;
            var layerCards = LayersListPanel.Children.OfType<Border>().Where(b => b.Tag is LayerItem).ToList();
            for (int i = 0; i < layerCards.Count; i++)
            {
                var c = layerCards[i];
                Point p = c.TranslatePoint(new Point(0, 0), LayersListPanel);
                double midY = p.Y + c.ActualHeight / 2.0;
                if (curPos.Y < midY)
                {
                    return i;
                }
            }
            return layerCards.Count;
        }

        private void UpdateDropIndicator(Point curPos)
        {
            if (LayersListPanel == null) return;
            var layerCards = LayersListPanel.Children.OfType<Border>().Where(b => b.Tag is LayerItem).ToList();
            if (layerCards.Count == 0) return;

            int targetSlot = CalculateDropSlot(curPos);

            if (LayersListPanel.Children.Contains(_dropIndicator))
            {
                LayersListPanel.Children.Remove(_dropIndicator);
            }

            if (targetSlot < layerCards.Count)
            {
                int visualIdx = LayersListPanel.Children.IndexOf(layerCards[targetSlot]);
                if (visualIdx >= 0)
                {
                    LayersListPanel.Children.Insert(visualIdx, _dropIndicator);
                }
                else
                {
                    LayersListPanel.Children.Add(_dropIndicator);
                }
            }
            else
            {
                int count = LayersListPanel.Children.Count;
                int insertIdx = (_currentImage != null && count > 0) ? count - 1 : count;
                LayersListPanel.Children.Insert(Math.Max(0, insertIdx), _dropIndicator);
            }
        }

        private void ExecuteLayerDrop(LayerItem item, Point dropPos)
        {
            int targetSlot = CalculateDropSlot(dropPos);
            var ordered = _layers.OrderByDescending(x => x.ZIndex).ToList();
            int fromIndex = ordered.IndexOf(item);

            if (fromIndex < 0)
            {
                return;
            }

            int targetIndex = targetSlot > fromIndex ? targetSlot - 1 : targetSlot;
            targetIndex = Math.Clamp(targetIndex, 0, ordered.Count - 1);

            if (targetIndex != fromIndex)
            {
                ordered.RemoveAt(fromIndex);
                ordered.Insert(targetIndex, item);

                int total = ordered.Count;
                var oldZ = _layers.ToDictionary(x => x, x => x.ZIndex);
                for (int i = 0; i < total; i++)
                {
                    ordered[i].ZIndex = total - i;
                    if (ordered[i].VisualElement != null)
                    {
                        Panel.SetZIndex(ordered[i].VisualElement, ordered[i].ZIndex);
                    }
                }
                var newZ = _layers.ToDictionary(x => x, x => x.ZIndex);

                _historyManager.Record(new ReorderLayersAction(this, oldZ, newZ));
                UpdateLayerListUI();
            }
        }

        #region Layer Z-Index Order Methods

        public void BringLayerToFront(LayerItem item)
        {
            if (_layers.Count == 0 || item == null) return;
            var oldZ = _layers.ToDictionary(x => x, x => x.ZIndex);
            int maxZ = _layers.Max(x => x.ZIndex);
            item.ZIndex = maxZ + 1;
            if (item.VisualElement != null)
            {
                Panel.SetZIndex(item.VisualElement, item.ZIndex);
            }
            var newZ = _layers.ToDictionary(x => x, x => x.ZIndex);

            _historyManager.Record(new ReorderLayersAction(this, oldZ, newZ));
            UpdateLayerListUI();
        }

        public void SendLayerToBack(LayerItem item)
        {
            if (_layers.Count == 0 || item == null) return;
            var oldZ = _layers.ToDictionary(x => x, x => x.ZIndex);
            int minZ = _layers.Min(x => x.ZIndex);
            item.ZIndex = minZ - 1;
            if (item.VisualElement != null)
            {
                Panel.SetZIndex(item.VisualElement, item.ZIndex);
            }
            var newZ = _layers.ToDictionary(x => x, x => x.ZIndex);

            _historyManager.Record(new ReorderLayersAction(this, oldZ, newZ));
            UpdateLayerListUI();
        }

        public void BringLayerForward(LayerItem item)
        {
            if (item == null) return;
            var nextItem = _layers.Where(x => x.ZIndex > item.ZIndex).OrderBy(x => x.ZIndex).FirstOrDefault();
            if (nextItem != null)
            {
                var oldZ = _layers.ToDictionary(x => x, x => x.ZIndex);
                int temp = item.ZIndex;
                item.ZIndex = nextItem.ZIndex;
                nextItem.ZIndex = temp;
                if (item.VisualElement != null) Panel.SetZIndex(item.VisualElement, item.ZIndex);
                if (nextItem.VisualElement != null) Panel.SetZIndex(nextItem.VisualElement, nextItem.ZIndex);
                var newZ = _layers.ToDictionary(x => x, x => x.ZIndex);

                _historyManager.Record(new ReorderLayersAction(this, oldZ, newZ));
                UpdateLayerListUI();
            }
        }

        public void SendLayerBackward(LayerItem item)
        {
            if (item == null) return;
            var prevItem = _layers.Where(x => x.ZIndex < item.ZIndex).OrderByDescending(x => x.ZIndex).FirstOrDefault();
            if (prevItem != null)
            {
                var oldZ = _layers.ToDictionary(x => x, x => x.ZIndex);
                int temp = item.ZIndex;
                item.ZIndex = prevItem.ZIndex;
                prevItem.ZIndex = temp;
                if (item.VisualElement != null) Panel.SetZIndex(item.VisualElement, item.ZIndex);
                if (prevItem.VisualElement != null) Panel.SetZIndex(prevItem.VisualElement, prevItem.ZIndex);
                var newZ = _layers.ToDictionary(x => x, x => x.ZIndex);

                _historyManager.Record(new ReorderLayersAction(this, oldZ, newZ));
                UpdateLayerListUI();
            }
        }

        #region Toolbar Handlers

        private void LayerFront_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                BringLayerToFront(_selectedLayerItem);
            }
        }

        private void LayerMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                BringLayerForward(_selectedLayerItem);
            }
        }

        private void LayerMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                SendLayerBackward(_selectedLayerItem);
            }
        }

        private void LayerBack_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                SendLayerToBack(_selectedLayerItem);
            }
        }

        #endregion

        #endregion
    }
}
