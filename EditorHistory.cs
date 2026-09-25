using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public interface IEditorAction
    {
        void Undo();
        void Redo();
    }

    public class AddLayerAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;

        public AddLayerAction(MainWindow window, LayerItem layer)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }

        public void Undo()
        {
            _window.InternalRemoveLayer(_layer);
        }

        public void Redo()
        {
            _window.InternalAddLayer(_layer);
        }
    }

    public class DeleteLayerAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;

        public DeleteLayerAction(MainWindow window, LayerItem layer)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }

        public void Undo()
        {
            _window.InternalAddLayer(_layer);
        }

        public void Redo()
        {
            _window.InternalRemoveLayer(_layer);
        }
    }

    public class TransformLayerAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly OverlayImageItem _item;
        private readonly Rect _oldRect;
        private readonly Rect _newRect;

        public TransformLayerAction(MainWindow window, OverlayImageItem item, Rect oldRect, Rect newRect)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _oldRect = oldRect;
            _newRect = newRect;
        }

        public void Undo()
        {
            _window.InternalTransformLayer(_item, _oldRect);
        }

        public void Redo()
        {
            _window.InternalTransformLayer(_item, _newRect);
        }
    }

    public class MoveStrokeLayerAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly StrokeLayerItem _item;
        private readonly double _dx;
        private readonly double _dy;

        public MoveStrokeLayerAction(MainWindow window, StrokeLayerItem item, double dx, double dy)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _dx = dx;
            _dy = dy;
        }

        public void Undo()
        {
            _window.InternalMoveStrokeLayer(_item, -_dx, -_dy);
        }

        public void Redo()
        {
            _window.InternalMoveStrokeLayer(_item, _dx, _dy);
        }
    }

    public class ReorderLayersAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly Dictionary<LayerItem, int> _oldZIndices;
        private readonly Dictionary<LayerItem, int> _newZIndices;

        public ReorderLayersAction(MainWindow window, Dictionary<LayerItem, int> oldZIndices, Dictionary<LayerItem, int> newZIndices)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _oldZIndices = new Dictionary<LayerItem, int>(oldZIndices);
            _newZIndices = new Dictionary<LayerItem, int>(newZIndices);
        }

        public void Undo()
        {
            _window.InternalReorderLayers(_oldZIndices);
        }

        public void Redo()
        {
            _window.InternalReorderLayers(_newZIndices);
        }
    }

    public class LayerOpacityAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;
        private readonly double _oldOpacity;
        private readonly double _newOpacity;

        public LayerOpacityAction(MainWindow window, LayerItem layer, double oldOpacity, double newOpacity)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldOpacity = oldOpacity;
            _newOpacity = newOpacity;
        }

        public void Undo()
        {
            _window.InternalSetLayerOpacity(_layer, _oldOpacity);
        }

        public void Redo()
        {
            _window.InternalSetLayerOpacity(_layer, _newOpacity);
        }
    }

    public class LayerVisibilityAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;
        private readonly bool _oldVis;
        private readonly bool _newVis;

        public LayerVisibilityAction(MainWindow window, LayerItem layer, bool oldVis, bool newVis)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldVis = oldVis;
            _newVis = newVis;
        }

        public void Undo()
        {
            _window.InternalSetLayerVisibility(_layer, _oldVis);
        }

        public void Redo()
        {
            _window.InternalSetLayerVisibility(_layer, _newVis);
        }
    }

    public class LayerLockAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;
        private readonly bool _oldLocked;
        private readonly bool _newLocked;

        public LayerLockAction(MainWindow window, LayerItem layer, bool oldLocked, bool newLocked)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldLocked = oldLocked;
            _newLocked = newLocked;
        }

        public void Undo()
        {
            _window.InternalSetLayerLock(_layer, _oldLocked);
        }

        public void Redo()
        {
            _window.InternalSetLayerLock(_layer, _newLocked);
        }
    }

    public class CropLayerImageAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly OverlayImageItem _item;
        private readonly BitmapSource _oldBmp;
        private readonly Rect _oldRect;
        private readonly BitmapSource _newBmp;
        private readonly Rect _newRect;

        public CropLayerImageAction(
            MainWindow window,
            OverlayImageItem item,
            BitmapSource oldBmp,
            Rect oldRect,
            BitmapSource newBmp,
            Rect newRect)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _oldBmp = oldBmp ?? throw new ArgumentNullException(nameof(oldBmp));
            _oldRect = oldRect;
            _newBmp = newBmp ?? throw new ArgumentNullException(nameof(newBmp));
            _newRect = newRect;
        }

        public void Undo()
        {
            _window.InternalCropLayer(_item, _oldBmp, _oldRect);
        }

        public void Redo()
        {
            _window.InternalCropLayer(_item, _newBmp, _newRect);
        }
    }

    public class MergeLayersAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly List<StrokeLayerItem> _originalItems;
        private readonly StrokeLayerItem _mergedItem;

        public MergeLayersAction(MainWindow window, List<StrokeLayerItem> originalItems, StrokeLayerItem mergedItem)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _originalItems = new List<StrokeLayerItem>(originalItems);
            _mergedItem = mergedItem ?? throw new ArgumentNullException(nameof(mergedItem));
        }

        public void Undo()
        {
            _window.InternalUnmergeLayers(_mergedItem, _originalItems);
        }

        public void Redo()
        {
            _window.InternalMergeLayers(_originalItems, _mergedItem);
        }
    }

    public class SplitLayersAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly StrokeLayerItem _mergedItem;
        private readonly List<StrokeLayerItem> _originalItems;

        public SplitLayersAction(MainWindow window, StrokeLayerItem mergedItem, List<StrokeLayerItem> originalItems)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _mergedItem = mergedItem ?? throw new ArgumentNullException(nameof(mergedItem));
            _originalItems = new List<StrokeLayerItem>(originalItems);
        }

        public void Undo()
        {
            _window.InternalMergeLayers(_originalItems, _mergedItem);
        }

        public void Redo()
        {
            _window.InternalUnmergeLayers(_mergedItem, _originalItems);
        }
    }

    public class ImageTransformAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly BitmapSource _oldImage;
        private readonly Stroke[] _oldStrokes;
        private readonly LayerItem[] _oldLayers;
        private readonly BitmapSource _newImage;
        private readonly Stroke[] _newStrokes;
        private readonly LayerItem[] _newLayers;

        public ImageTransformAction(
            MainWindow window,
            BitmapSource oldImage,
            Stroke[] oldStrokes,
            LayerItem[] oldLayers,
            BitmapSource newImage,
            Stroke[] newStrokes,
            LayerItem[] newLayers)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _oldImage = oldImage;
            _oldStrokes = oldStrokes ?? Array.Empty<Stroke>();
            _oldLayers = oldLayers ?? Array.Empty<LayerItem>();
            _newImage = newImage;
            _newStrokes = newStrokes ?? Array.Empty<Stroke>();
            _newLayers = newLayers ?? Array.Empty<LayerItem>();
        }

        public void Undo()
        {
            _window.SetImageAndLayers(_oldImage, _oldStrokes, _oldLayers);
        }

        public void Redo()
        {
            _window.SetImageAndLayers(_newImage, _newStrokes, _newLayers);
        }
    }

    public class RenameLayerAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly LayerItem _layer;
        private readonly string _oldName;
        private readonly string _newName;

        public RenameLayerAction(MainWindow window, LayerItem layer, string oldName, string newName)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldName = oldName ?? string.Empty;
            _newName = newName ?? string.Empty;
        }

        public void Undo()
        {
            _window.InternalSetLayerName(_layer, _oldName);
        }

        public void Redo()
        {
            _window.InternalSetLayerName(_layer, _newName);
        }
    }

    public class ChangeBgColorAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly BitmapSource _oldImage;
        private readonly Color _oldBgColor;
        private readonly BitmapSource _newImage;
        private readonly Color _newBgColor;

        public ChangeBgColorAction(MainWindow window, BitmapSource oldImage, Color oldBgColor, BitmapSource newImage, Color newBgColor)
        {
            _window = window;
            _oldImage = oldImage;
            _oldBgColor = oldBgColor;
            _newImage = newImage;
            _newBgColor = newBgColor;
        }

        public void Undo()
        {
            _window.InternalSetBaseImageAndBgColor(_oldImage, _oldBgColor);
        }

        public void Redo()
        {
            _window.InternalSetBaseImageAndBgColor(_newImage, _newBgColor);
        }
    }

    public class HistoryManager
    {
        private readonly List<IEditorAction> _undoStack = new();
        private readonly List<IEditorAction> _redoStack = new();
        public const int MaxHistoryCount = 50;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event EventHandler? HistoryChanged;

        public void Record(IEditorAction action)
        {
            _undoStack.Add(action);
            if (_undoStack.Count > MaxHistoryCount)
            {
                _undoStack.RemoveAt(0);
            }
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            int lastIdx = _undoStack.Count - 1;
            var action = _undoStack[lastIdx];
            _undoStack.RemoveAt(lastIdx);

            action.Undo();
            _redoStack.Add(action);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo)
            {
                return;
            }
            int lastIdx = _redoStack.Count - 1;
            var action = _redoStack[lastIdx];
            _redoStack.RemoveAt(lastIdx);

            action.Redo();
            _undoStack.Add(action);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
