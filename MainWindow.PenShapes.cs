using System;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace ImageEditor
{
    public partial class MainWindow
    {
        public void UpdateCanvasClips(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            var rectGeo = new RectangleGeometry(new Rect(0, 0, w, h));
            if (ImageContainer != null) ImageContainer.Clip = rectGeo;
            if (OverlayCanvas != null) OverlayCanvas.Clip = rectGeo;
            if (MainInkCanvas != null) MainInkCanvas.Clip = rectGeo;
            if (ShapePreviewCanvas != null) ShapePreviewCanvas.Clip = rectGeo;
        }

        private void RenderShapePreview(Point start, Point end)
        {
            ShapePreviewCanvas.Children.Clear();
            double thickness = GetEffectiveCanvasThickness();
            var stroke = CreateShapeStroke(start, end, _strokeShape, _currentColor, thickness);
            var geom = stroke.GetGeometry();
            var path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = new SolidColorBrush(_currentColor)
            };
            ShapePreviewCanvas.Children.Add(path);
        }

        public static Stroke CreateShapeStroke(Point start, Point end, StrokeShape shape, Color color, double thickness)
        {
            var pts = new StylusPointCollection();
            var attr = new DrawingAttributes
            {
                Color = color,
                Width = thickness,
                Height = thickness,
                FitToCurve = false,
                StylusTip = StylusTip.Ellipse,
                IgnorePressure = true
            };

            if (shape == StrokeShape.Line)
            {
                pts.Add(new StylusPoint(start.X, start.Y));
                pts.Add(new StylusPoint(end.X, end.Y));
            }
            else if (shape == StrokeShape.Arrow)
            {
                Vector dir = end - start;
                double len = dir.Length;
                if (len < 2)
                {
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                }
                else
                {
                    double angle = Math.Atan2(dir.Y, dir.X);
                    double headLen = Math.Min(thickness * 3.5, len * 0.4);
                    headLen = Math.Max(headLen, thickness * 2.0);
                    double barbAngle = Math.PI * 0.82;

                    Point w1 = new Point(end.X + Math.Cos(angle + barbAngle) * headLen,
                                         end.Y + Math.Sin(angle + barbAngle) * headLen);
                    Point w2 = new Point(end.X + Math.Cos(angle - barbAngle) * headLen,
                                         end.Y + Math.Sin(angle - barbAngle) * headLen);

                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w1.X, w1.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w2.X, w2.Y));
                }
            }
            else if (shape == StrokeShape.DoubleArrow)
            {
                Vector dir = end - start;
                double len = dir.Length;
                if (len < 2)
                {
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                }
                else
                {
                    double angle = Math.Atan2(dir.Y, dir.X);
                    double headLen = Math.Min(thickness * 3.5, len * 0.4);
                    headLen = Math.Max(headLen, thickness * 2.0);
                    double barbAngle = Math.PI * 0.82;

                    Point w1 = new Point(end.X + Math.Cos(angle + barbAngle) * headLen,
                                         end.Y + Math.Sin(angle + barbAngle) * headLen);
                    Point w2 = new Point(end.X + Math.Cos(angle - barbAngle) * headLen,
                                         end.Y + Math.Sin(angle - barbAngle) * headLen);

                    Point aw1 = new Point(start.X + Math.Cos(angle + Math.PI - barbAngle) * headLen,
                                          start.Y + Math.Sin(angle + Math.PI - barbAngle) * headLen);
                    Point aw2 = new Point(start.X + Math.Cos(angle + Math.PI + barbAngle) * headLen,
                                          start.Y + Math.Sin(angle + Math.PI + barbAngle) * headLen);

                    pts.Add(new StylusPoint(aw1.X, aw1.Y));
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(aw2.X, aw2.Y));
                    pts.Add(new StylusPoint(start.X, start.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w1.X, w1.Y));
                    pts.Add(new StylusPoint(end.X, end.Y));
                    pts.Add(new StylusPoint(w2.X, w2.Y));
                }
            }

            return new Stroke(pts, attr);
        }
    }
}
