using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public interface IEditorAction
    {
        void Undo();
        void Redo();
    }

    public class AddStrokeAction : IEditorAction
    {
        private readonly Stroke _stroke;
        private readonly InkCanvas _inkCanvas;

        public AddStrokeAction(Stroke stroke, InkCanvas inkCanvas)
        {
            _stroke = stroke ?? throw new ArgumentNullException(nameof(stroke));
            _inkCanvas = inkCanvas ?? throw new ArgumentNullException(nameof(inkCanvas));
        }

        public void Undo()
        {
            _inkCanvas.Strokes.Remove(_stroke);
        }

        public void Redo()
        {
            if (!_inkCanvas.Strokes.Contains(_stroke))
            {
                _inkCanvas.Strokes.Add(_stroke);
            }
        }
    }

    public class ImageTransformAction : IEditorAction
    {
        private readonly MainWindow _window;
        private readonly BitmapSource _oldImage;
        private readonly Stroke[] _oldStrokes;
        private readonly BitmapSource _newImage;
        private readonly Stroke[] _newStrokes;

        public ImageTransformAction(
            MainWindow window,
            BitmapSource oldImage,
            Stroke[] oldStrokes,
            BitmapSource newImage,
            Stroke[] newStrokes)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _oldImage = oldImage;
            _oldStrokes = oldStrokes ?? Array.Empty<Stroke>();
            _newImage = newImage;
            _newStrokes = newStrokes ?? Array.Empty<Stroke>();
        }

        public void Undo()
        {
            _window.SetImageAndStrokes(_oldImage, _oldStrokes);
        }

        public void Redo()
        {
            _window.SetImageAndStrokes(_newImage, _newStrokes);
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
