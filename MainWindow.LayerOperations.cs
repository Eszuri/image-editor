using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public partial class MainWindow
    {
        public void DuplicateLayer(LayerItem item)
        {
            if (item is OverlayImageItem img)
            {
                string copyName = GetUniqueLayerName($"{img.Name} (Copy)");
                AddOverlayImage(img.Source, new Point(img.X + 25, img.Y + 25), img.Width, img.Height, name: copyName);
                if (_selectedLayerItem != null)
                {
                    _selectedLayerItem.Opacity = img.Opacity;
                }
            }
            else if (item is StrokeLayerItem strokeItem)
            {
                var clonedStroke = strokeItem.StrokeData.Clone();
                var mat = new Matrix();
                mat.Translate(15, 15);
                clonedStroke.Transform(mat, false);
                string copyName = GetUniqueLayerName($"{strokeItem.Name} (Copy)");
                var newStroke = AddStrokeLayer(clonedStroke, strokeItem.Shape, copyName, autoSelect: true);
                if (newStroke != null)
                {
                    newStroke.Opacity = strokeItem.Opacity;
                }
            }
        }

        public HashSet<string> GetAllKnownLayerNames(LayerItem? exclude = null)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in _layers)
            {
                if (layer == exclude) continue;
                if (!string.IsNullOrWhiteSpace(layer.Name))
                {
                    names.Add(layer.Name.Trim());
                }
                if (layer is StrokeLayerItem stroke && stroke.IsMerged && stroke.OriginalLayers != null)
                {
                    foreach (var orig in stroke.OriginalLayers)
                    {
                        if (orig == exclude) continue;
                        if (!string.IsNullOrWhiteSpace(orig.Name))
                        {
                            names.Add(orig.Name.Trim());
                        }
                    }
                }
            }
            return names;
        }

        public string GetUniqueLayerName(string desiredName, LayerItem? exclude = null)
        {
            var existingNames = GetAllKnownLayerNames(exclude);
            string trimmed = string.IsNullOrWhiteSpace(desiredName) ? "Layer" : desiredName.Trim();

            if (!existingNames.Contains(trimmed))
            {
                return trimmed;
            }

            var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(.*?)(?:\s+(\d+)|\s*\((\d+)\))$");
            if (match.Success)
            {
                string basePrefix = match.Groups[1].Value.Trim();
                bool hasParens = match.Groups[3].Success;
                int num = 1;
                while (true)
                {
                    string candidate = hasParens ? $"{basePrefix} ({num})" : $"{basePrefix} {num}";
                    if (!existingNames.Contains(candidate))
                    {
                        return candidate;
                    }
                    num++;
                }
            }
            else
            {
                int num = 2;
                while (true)
                {
                    string candidate = $"{trimmed} ({num})";
                    if (!existingNames.Contains(candidate))
                    {
                        return candidate;
                    }
                    num++;
                }
            }
        }

        public string GetNextStrokeName(StrokeShape shape)
        {
            string shapeName = shape == StrokeShape.Freehand ? "Stroke" : shape.ToString();
            var existingNames = GetAllKnownLayerNames();
            int num = 1;
            while (existingNames.Contains($"{shapeName} {num}"))
            {
                num++;
            }
            return $"{shapeName} {num}";
        }

        public void DuplicateOverlayItem(OverlayImageItem item) => DuplicateLayer(item);

        public void DuplicateSelectedLayer()
        {
            if (_selectedLayers.Count > 1)
            {
                var toDup = _selectedLayers.ToList();
                foreach (var item in toDup)
                {
                    DuplicateLayer(item);
                }
            }
            else if (_selectedLayerItem != null)
            {
                DuplicateLayer(_selectedLayerItem);
            }
        }

        public void DuplicateSelectedOverlay() => DuplicateSelectedLayer();

        public void DeleteLayer(LayerItem item)
        {
            if (item == null || item.IsLocked)
            {
                return;
            }
            if (_selectedLayerItem == item || _selectedLayers.Contains(item))
            {
                _selectedLayers.Remove(item);
                if (item is StrokeLayerItem strk)
                {
                    _selectedStrokeLayers.Remove(strk);
                }
                if (_selectedLayerItem == item)
                {
                    _selectedLayerItem = _selectedLayers.LastOrDefault();
                }
            }
            _layers.Remove(item);
            OverlayCanvas.Children.Remove(item.VisualElement);
            _historyManager.Record(new DeleteLayerAction(this, item));
            UpdateMergeButtonVisibility();
            UpdateLayerListUI();
        }

        public void DeleteOverlayItem(OverlayImageItem item) => DeleteLayer(item);

        public void DeleteSelectedLayer()
        {
            if (_selectedLayers.Count > 1)
            {
                var toDelete = _selectedLayers.Where(x => !x.IsLocked).ToList();
                foreach (var item in toDelete)
                {
                    DeleteLayer(item);
                }
            }
            else if (_selectedLayerItem != null && !_selectedLayerItem.IsLocked)
            {
                DeleteLayer(_selectedLayerItem);
            }
        }

        public void DeleteSelectedOverlay() => DeleteSelectedLayer();

        public void ClearLayers()
        {
            DeselectLayer();
            _layers.Clear();
            OverlayCanvas?.Children.Clear();
            UpdateLayerListUI();
        }

        public void ClearOverlayItems() => ClearLayers();

        #region Internal Methods for History Actions

        public void InternalAddLayer(LayerItem item)
        {
            if (!_layers.Contains(item))
            {
                _layers.Add(item);
            }
            if (OverlayCanvas != null && !OverlayCanvas.Children.Contains(item.VisualElement))
            {
                OverlayCanvas.Children.Add(item.VisualElement);
                Panel.SetZIndex(item.VisualElement, item.ZIndex);
            }
            if (!_isPenActive)
            {
                SelectLayerItem(item);
            }
            else
            {
                DeselectLayer();
            }
            UpdateLayerListUI();
        }

        public void InternalRemoveLayer(LayerItem item)
        {
            if (_selectedLayerItem == item)
            {
                DeselectLayer();
            }
            _layers.Remove(item);
            OverlayCanvas?.Children.Remove(item.VisualElement);
            UpdateLayerListUI();
        }

        public void InternalTransformLayer(OverlayImageItem item, Rect rect)
        {
            item.X = rect.X;
            item.Y = rect.Y;
            item.Width = rect.Width;
            item.Height = rect.Height;

            if (item.ImageVisualElement != null)
            {
                item.ImageVisualElement.Width = rect.Width;
                item.ImageVisualElement.Height = rect.Height;
                Canvas.SetLeft(item.ImageVisualElement, rect.X);
                Canvas.SetTop(item.ImageVisualElement, rect.Y);
            }
            UpdateLayerListUI();
        }

        public void InternalReorderLayers(Dictionary<LayerItem, int> zIndices)
        {
            foreach (var kvp in zIndices)
            {
                kvp.Key.ZIndex = kvp.Value;
                if (kvp.Key.VisualElement != null)
                {
                    Panel.SetZIndex(kvp.Key.VisualElement, kvp.Value);
                }
            }
            UpdateLayerListUI();
        }

        public void InternalSetLayerOpacity(LayerItem item, double opacity)
        {
            item.Opacity = opacity;
            UpdateLayerSelectionVisuals();
        }

        public void InternalSetLayerVisibility(LayerItem item, bool isVisible)
        {
            item.IsVisible = isVisible;
            UpdateLayerListUI();
        }

        public void ToggleLayerLock(LayerItem item)
        {
            if (item == null) return;
            bool oldLocked = item.IsLocked;
            bool newLocked = !oldLocked;
            InternalSetLayerLock(item, newLocked);
            _historyManager.Record(new LayerLockAction(this, item, oldLocked, newLocked));
        }

        public void InternalSetLayerLock(LayerItem item, bool isLocked)
        {
            item.IsLocked = isLocked;
            if (item is OverlayImageItem img && img.ImageVisualElement != null)
            {
                img.ImageVisualElement.Cursor = isLocked ? Cursors.Arrow : Cursors.SizeAll;
            }
            UpdateLayerSelectionVisuals();
            UpdateLayerListUI();
        }

        public void InternalCropLayer(OverlayImageItem item, BitmapSource newImage, Rect newRect)
        {
            item.Source = newImage;
            item.X = newRect.X;
            item.Y = newRect.Y;
            item.Width = newRect.Width;
            item.Height = newRect.Height;

            if (item.ImageControl != null)
            {
                item.ImageControl.Source = newImage;
            }
            if (item.ImageVisualElement != null)
            {
                item.ImageVisualElement.Width = newRect.Width;
                item.ImageVisualElement.Height = newRect.Height;
                Canvas.SetLeft(item.ImageVisualElement, newRect.X);
                Canvas.SetTop(item.ImageVisualElement, newRect.Y);
            }
            UpdateLayerSelectionVisuals();
            UpdateLayerListUI();
        }

        #region Toolbar Handlers

        private void LayerDuplicate_Click(object sender, RoutedEventArgs e)
        {
            DuplicateSelectedLayer();
        }

        private void LayerDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedLayer();
        }

        #endregion

        #endregion
    }
}
