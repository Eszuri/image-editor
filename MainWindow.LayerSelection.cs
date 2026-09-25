using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private readonly HashSet<LayerItem> _selectedLayers = new();
        private readonly HashSet<StrokeLayerItem> _selectedStrokeLayers = new();
        private LayerItem? _selectionAnchorLayer;

        public void HandleLayerCardClick(LayerItem item, MouseButtonEventArgs e)
        {
            if (item == null) return;

            bool isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (isShift)
            {
                // Shift + Click: Range Selection from anchor to clicked item
                var ordered = _layers.OrderByDescending(x => x.ZIndex).ToList();
                var anchor = _selectionAnchorLayer;
                if (anchor == null || !ordered.Contains(anchor))
                {
                    anchor = _selectedLayerItem ?? ordered.FirstOrDefault();
                }

                int anchorIdx = anchor != null ? ordered.IndexOf(anchor) : -1;
                int targetIdx = ordered.IndexOf(item);

                if (anchorIdx >= 0 && targetIdx >= 0)
                {
                    int start = Math.Min(anchorIdx, targetIdx);
                    int end = Math.Max(anchorIdx, targetIdx);

                    if (!isCtrl)
                    {
                        foreach (var l in _selectedLayers)
                        {
                            l.IsSelected = false;
                        }
                        _selectedLayers.Clear();
                        _selectedStrokeLayers.Clear();
                    }

                    for (int i = start; i <= end; i++)
                    {
                        var layer = ordered[i];
                        _selectedLayers.Add(layer);
                        layer.IsSelected = true;
                        if (layer is StrokeLayerItem strk)
                        {
                            _selectedStrokeLayers.Add(strk);
                        }
                    }

                    _selectedLayerItem = item;
                    if (_selectionAnchorLayer == null)
                    {
                        _selectionAnchorLayer = anchor;
                    }
                }
                else
                {
                    SelectSingleLayer(item);
                }

                ActivateCursorMode();
                UpdateMergeButtonVisibility();
                UpdateLayerSelectionVisuals();
                e.Handled = true;
            }
            else if (isCtrl)
            {
                // Ctrl + Click: Toggle single layer in multi-selection
                if (_selectedLayerItem != null && !_selectedLayers.Contains(_selectedLayerItem))
                {
                    _selectedLayers.Add(_selectedLayerItem);
                    _selectedLayerItem.IsSelected = true;
                    if (_selectedLayerItem is StrokeLayerItem curStroke)
                    {
                        _selectedStrokeLayers.Add(curStroke);
                    }
                }

                if (_selectedLayers.Contains(item))
                {
                    _selectedLayers.Remove(item);
                    item.IsSelected = false;
                    if (item is StrokeLayerItem strk)
                    {
                        _selectedStrokeLayers.Remove(strk);
                    }

                    if (_selectedLayerItem == item)
                    {
                        _selectedLayerItem = _selectedLayers.LastOrDefault();
                    }
                }
                else
                {
                    _selectedLayers.Add(item);
                    item.IsSelected = true;
                    if (item is StrokeLayerItem strk)
                    {
                        _selectedStrokeLayers.Add(strk);
                    }
                    _selectedLayerItem = item;
                }

                _selectionAnchorLayer = item;
                ActivateCursorMode();
                UpdateMergeButtonVisibility();
                UpdateLayerSelectionVisuals();
                e.Handled = true;
            }
            else
            {
                // Normal Click: Single selection
                SelectSingleLayer(item);
                ActivateCursorMode();
                UpdateLayerSelectionVisuals();
                e.Handled = true;
            }
        }

        public void SelectSingleLayer(LayerItem item)
        {
            if (item == null) return;

            foreach (var l in _selectedLayers)
            {
                if (l != item)
                {
                    l.IsSelected = false;
                }
            }

            _selectedLayers.Clear();
            _selectedStrokeLayers.Clear();

            _selectedLayers.Add(item);
            if (item is StrokeLayerItem strk)
            {
                _selectedStrokeLayers.Add(strk);
            }

            _selectionAnchorLayer = item;
            SelectLayerItem(item);
            UpdateMergeButtonVisibility();
        }

        public void SelectLayerItem(LayerItem item)
        {
            if (item == null) return;

            if (_selectedLayerItem != null && _selectedLayerItem != item && !_selectedLayers.Contains(_selectedLayerItem))
            {
                _selectedLayerItem.IsSelected = false;
            }

            _selectedLayerItem = item;
            if (!_selectedLayers.Contains(item))
            {
                _selectedLayers.Add(item);
                if (item is StrokeLayerItem strk)
                {
                    _selectedStrokeLayers.Add(strk);
                }
            }
            item.IsSelected = true;
            UpdateLayerSelectionVisuals();
        }

        public void SelectOverlayItem(OverlayImageItem item) => SelectLayerItem(item);

        public void DeselectOverlay() => DeselectLayer();

        public void DeselectLayer()
        {
            foreach (var l in _selectedLayers)
            {
                l.IsSelected = false;
            }
            _selectedLayers.Clear();
            _selectedStrokeLayers.Clear();
            _selectionAnchorLayer = null;

            if (_selectedLayerItem != null)
            {
                _selectedLayerItem.IsSelected = false;
                _selectedLayerItem = null;
            }
            UpdateMergeButtonVisibility();
            UpdateLayerSelectionVisuals();
        }
    }
}
