using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public interface IEditorAction
    {
        void Undo();
        void Redo();
        string Description { get; }
    }

    public class AddStrokeAction : IEditorAction
    {
        public Stroke Stroke { get; }
        private readonly InkCanvas _inkCanvas;
        public string Description => "Add Pen Stroke";

        public AddStrokeAction(Stroke stroke, InkCanvas inkCanvas)
        {
            Stroke = stroke ?? throw new ArgumentNullException(nameof(stroke));
            _inkCanvas = inkCanvas ?? throw new ArgumentNullException(nameof(inkCanvas));
        }

        public void Undo()
        {
            _inkCanvas.Strokes.Remove(Stroke);
        }

        public void Redo()
        {
            if (!_inkCanvas.Strokes.Contains(Stroke))
            {
                _inkCanvas.Strokes.Add(Stroke);
            }
        }
    }

    public class ImageTransformAction : IEditorAction
    {
        private readonly MainWindow _window;
        public BitmapSource OldImage { get; }
        public Stroke[] OldStrokes { get; }
        public BitmapSource NewImage { get; }
        public Stroke[] NewStrokes { get; }
        public string Description { get; }

        public ImageTransformAction(MainWindow window, BitmapSource oldImage, Stroke[] oldStrokes, BitmapSource newImage, Stroke[] newStrokes, string description)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            OldImage = oldImage;
            OldStrokes = oldStrokes ?? Array.Empty<Stroke>();
            NewImage = newImage;
            NewStrokes = newStrokes ?? Array.Empty<Stroke>();
            Description = description;
        }

        public void Undo()
        {
            _window.SetImageAndStrokes(OldImage, OldStrokes);
        }

        public void Redo()
        {
            _window.SetImageAndStrokes(NewImage, NewStrokes);
        }
    }

    public class HistoryManager
    {
        private readonly List<IEditorAction> _undoStack = new();
        private readonly List<IEditorAction> _redoStack = new();
        public int MaxHistoryCount { get; set; } = 50;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

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
            if (!CanUndo) return;
            int lastIdx = _undoStack.Count - 1;
            var action = _undoStack[lastIdx];
            _undoStack.RemoveAt(lastIdx);

            action.Undo();
            _redoStack.Add(action);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo) return;
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
