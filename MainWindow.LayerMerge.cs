using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ImageEditor
{
    public partial class MainWindow
    {
        public void SplitSelectedStrokeLayer()
        {
            if (_selectedLayerItem is StrokeLayerItem strokeItem)
            {
                SplitStrokeLayer(strokeItem);
            }
        }

        public void SplitStrokeLayer(StrokeLayerItem strokeItem)
        {
            if (strokeItem == null || !strokeItem.IsMerged)
            {
                return;
            }

            if (strokeItem.IsLocked)
            {
                MessageBox.Show(
                    "This layer is locked. Please unlock it before splitting.",
                    "Layer Locked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var originals = strokeItem.OriginalLayers;
            if (originals == null || originals.Count == 0)
            {
                return;
            }

            InternalUnmergeLayers(strokeItem, originals);
            _historyManager.Record(new SplitLayersAction(this, strokeItem, originals));
        }

        public void MergeSelectedStrokeLayers()
        {
            if (_selectedStrokeLayers.Count < 2)
            {
                return;
            }

            if (_selectedStrokeLayers.Any(x => x.IsLocked))
            {
                MessageBox.Show(
                    "Some of the selected pen layers are locked. Please unlock them before merging.",
                    "Layer Locked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var originalList = _selectedStrokeLayers.OrderBy(x => x.ZIndex).ToList();
            double canvasW = _currentImage?.PixelWidth ?? 800;
            double canvasH = _currentImage?.PixelHeight ?? 600;
            int zIndex = originalList.Max(x => x.ZIndex);
            string defaultName = $"Merged Pen ({originalList.Count})";
            string name = GetUniqueLayerName(defaultName);

            var merged = StrokeLayerItem.CreateMerged(originalList, canvasW, canvasH, zIndex, name);

            InternalMergeLayers(originalList, merged);
            _historyManager.Record(new MergeLayersAction(this, originalList, merged));
        }

        public void UpdateMergeButtonVisibility()
        {
            if (LayerMergeBtn != null)
            {
                if (_selectedStrokeLayers.Count >= 2)
                {
                    LayerMergeBtn.Visibility = Visibility.Visible;
                    LayerMergeBtn.Content = $"Merge ({_selectedStrokeLayers.Count})";
                }
                else
                {
                    LayerMergeBtn.Visibility = Visibility.Collapsed;
                }
            }

            if (LayerSplitBtn != null)
            {
                if (_selectedStrokeLayers.Count < 2 &&
                    _selectedLayerItem is StrokeLayerItem stroke &&
                    stroke.IsMerged)
                {
                    LayerSplitBtn.Visibility = Visibility.Visible;
                    LayerSplitBtn.Content = $"Split Layer ({stroke.OriginalLayers.Count})";
                }
                else
                {
                    LayerSplitBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void InternalMergeLayers(List<StrokeLayerItem> originalItems, StrokeLayerItem mergedItem)
        {
            var ordered = _layers.OrderByDescending(x => x.ZIndex).ToList();
            int insertIndex = ordered.Count;

            foreach (var it in originalItems)
            {
                int idx = ordered.IndexOf(it);
                if (idx >= 0 && idx < insertIndex)
                {
                    insertIndex = idx;
                }
                ordered.Remove(it);
                _layers.Remove(it);
                OverlayCanvas.Children.Remove(it.VisualElement);
                _selectedStrokeLayers.Remove(it);
                _selectedLayers.Remove(it);
            }

            insertIndex = Math.Clamp(insertIndex, 0, ordered.Count);

            mergedItem.Name = GetUniqueLayerName(mergedItem.Name, mergedItem);

            ordered.Insert(insertIndex, mergedItem);
            if (!_layers.Contains(mergedItem))
            {
                _layers.Add(mergedItem);
            }
            if (!OverlayCanvas.Children.Contains(mergedItem.VisualElement))
            {
                OverlayCanvas.Children.Add(mergedItem.VisualElement);
            }

            int total = ordered.Count;
            for (int i = 0; i < total; i++)
            {
                var layer = ordered[i];
                layer.ZIndex = total - i;
                Panel.SetZIndex(layer.VisualElement, layer.ZIndex);
            }

            _selectedLayers.Clear();
            _selectedStrokeLayers.Clear();
            SelectSingleLayer(mergedItem);
            UpdateLayerListUI();
            UpdateMergeButtonVisibility();
        }

        public void InternalUnmergeLayers(StrokeLayerItem mergedItem, List<StrokeLayerItem> originalItems)
        {
            // 1. Find the current position of mergedItem in the layer stack (ordered by ZIndex descending)
            var ordered = _layers.OrderByDescending(x => x.ZIndex).ToList();
            int insertIndex = ordered.IndexOf(mergedItem);
            if (insertIndex < 0)
            {
                insertIndex = 0;
            }

            // Remove mergedItem from active list and canvas
            ordered.Remove(mergedItem);
            _layers.Remove(mergedItem);
            OverlayCanvas.Children.Remove(mergedItem.VisualElement);
            _selectedStrokeLayers.Remove(mergedItem);
            _selectedLayers.Remove(mergedItem);

            // 2. Order original items in their relative visual stacking order (top to bottom)
            var orderedOriginals = originalItems.OrderByDescending(x => x.ZIndex).ToList();

            // 3. Ensure no duplicate names among original layers
            foreach (var orig in orderedOriginals)
            {
                orig.Name = GetUniqueLayerName(orig.Name, orig);
            }

            // 4. Insert original items right where mergedItem was in the ordered stack
            ordered.InsertRange(insertIndex, orderedOriginals);

            // 5. Add to _layers and OverlayCanvas, and re-normalize ZIndex across all layers
            int total = ordered.Count;
            for (int i = 0; i < total; i++)
            {
                var layer = ordered[i];
                layer.ZIndex = total - i;
                if (!_layers.Contains(layer))
                {
                    _layers.Add(layer);
                }
                if (!OverlayCanvas.Children.Contains(layer.VisualElement))
                {
                    OverlayCanvas.Children.Add(layer.VisualElement);
                }
                Panel.SetZIndex(layer.VisualElement, layer.ZIndex);
            }

            // 6. Update selection bounds for all restored original layers
            foreach (var orig in orderedOriginals)
            {
                orig.UpdateSelectionBounds();
            }

            _selectedLayers.Clear();
            _selectedStrokeLayers.Clear();
            if (orderedOriginals.Count > 0)
            {
                SelectSingleLayer(orderedOriginals.First());
            }
            UpdateLayerListUI();
            UpdateMergeButtonVisibility();
        }

        #region Toolbar Handlers

        private void LayerMerge_Click(object sender, RoutedEventArgs e)
        {
            MergeSelectedStrokeLayers();
        }

        private void LayerSplit_Click(object sender, RoutedEventArgs e)
        {
            SplitSelectedStrokeLayer();
        }

        #endregion
    }
}
