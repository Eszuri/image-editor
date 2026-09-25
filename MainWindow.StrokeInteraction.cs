using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private bool _isDraggingStroke;
        private Point _strokeDragStartCanvasPos;
        private Point _strokeDragLastCanvasPos;
        private double _strokeTotalDx;
        private double _strokeTotalDy;
        private StrokeLayerItem? _draggingStrokeItem;

        public void StartDraggingStroke(StrokeLayerItem item, Point canvasPos, IInputElement captureElement)
        {
            if (item == null || item.IsLocked || _isPenActive)
            {
                return;
            }

            _isDraggingStroke = true;
            _draggingStrokeItem = item;
            _strokeDragStartCanvasPos = canvasPos;
            _strokeDragLastCanvasPos = canvasPos;
            _strokeTotalDx = 0;
            _strokeTotalDy = 0;

            captureElement?.CaptureMouse();
        }

        public void ProcessDraggingStroke(Point canvasPos)
        {
            if (!_isDraggingStroke || _draggingStrokeItem == null)
            {
                return;
            }

            double dx = canvasPos.X - _strokeDragLastCanvasPos.X;
            double dy = canvasPos.Y - _strokeDragLastCanvasPos.Y;

            if (Math.Abs(dx) > 0.001 || Math.Abs(dy) > 0.001)
            {
                _draggingStrokeItem.TranslateStrokes(dx, dy);
                _strokeTotalDx += dx;
                _strokeTotalDy += dy;
                _strokeDragLastCanvasPos = canvasPos;
            }
        }

        public void FinishDraggingStroke(IInputElement captureElement)
        {
            if (!_isDraggingStroke)
            {
                return;
            }

            _isDraggingStroke = false;
            captureElement?.ReleaseMouseCapture();

            if (_draggingStrokeItem != null)
            {
                if (Math.Abs(_strokeTotalDx) > 0.5 || Math.Abs(_strokeTotalDy) > 0.5)
                {
                    _historyManager.Record(new MoveStrokeLayerAction(this, _draggingStrokeItem, _strokeTotalDx, _strokeTotalDy));
                }
                _draggingStrokeItem.UpdateSelectionBounds();
                UpdateLayerListUI();
            }

            _draggingStrokeItem = null;
        }

        public void ShowStrokeContextMenu(StrokeLayerItem item)
        {
            if (item == null) return;
            SelectLayerItem(item);
            var menu = CreateStrokeContextMenu(item);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        public void InternalMoveStrokeLayer(StrokeLayerItem item, double dx, double dy)
        {
            if (item == null) return;
            item.TranslateStrokes(dx, dy);
            UpdateLayerListUI();
        }

        public StrokeLayerItem? HitTestStrokeAtPoint(Point pt)
        {
            // Check currently selected stroke's selection box or strokes first
            if (_selectedLayerItem is StrokeLayerItem selStroke && selStroke.IsVisible)
            {
                if (selStroke.HitTestSelectionBox(pt) || selStroke.HitTestPoint(pt))
                {
                    return selStroke;
                }
            }

            // Top-to-bottom by ZIndex
            return _layers.OfType<StrokeLayerItem>()
                .Where(s => s.IsVisible)
                .OrderByDescending(s => s.ZIndex)
                .FirstOrDefault(s => s.HitTestPoint(pt));
        }
    }
}
