using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace ImageEditor
{
    public class StrokeLayerItem : LayerItem
    {
        public override LayerType Type => LayerType.Stroke;

        public StrokeCollection Strokes { get; set; } = new();

        public Stroke StrokeData
        {
            get => Strokes.Count > 0 ? Strokes[0] : null!;
            set
            {
                Strokes.Clear();
                if (value != null)
                {
                    Strokes.Add(value);
                }
            }
        }

        public Color StrokeColor { get; set; }
        public double StrokeThickness { get; set; }
        public MainWindow.StrokeShape Shape { get; set; } = MainWindow.StrokeShape.Freehand;

        public Canvas HostCanvas { get; set; } = null!;
        public InkPresenter Presenter { get; set; } = null!;
        public Border SelectionBorder { get; set; } = null!;

        public override FrameworkElement VisualElement => HostCanvas;

        public List<StrokeLayerItem> OriginalLayers { get; set; } = new();
        public bool IsMerged => OriginalLayers != null && OriginalLayers.Count > 0;

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (SelectionBorder != null)
                {
                    if (value && Strokes.Count > 0)
                    {
                        UpdateSelectionBounds();
                        SelectionBorder.BorderBrush = IsLocked
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5A93C"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
                        SelectionBorder.Cursor = IsLocked ? Cursors.Arrow : Cursors.SizeAll;
                        SelectionBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SelectionBorder.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        public void UpdateSelectionBounds()
        {
            if (SelectionBorder != null && Strokes.Count > 0)
            {
                Rect bounds = Strokes.GetBounds();
                double pad = 6;
                SelectionBorder.Width = Math.Max(16, bounds.Width + pad * 2);
                SelectionBorder.Height = Math.Max(16, bounds.Height + pad * 2);
                Canvas.SetLeft(SelectionBorder, bounds.X - pad);
                Canvas.SetTop(SelectionBorder, bounds.Y - pad);
            }
        }

        public bool HitTestPoint(Point pt, double tolerance = 8.0)
        {
            if (Strokes == null || Strokes.Count == 0)
            {
                return false;
            }
            Rect bounds = Strokes.GetBounds();
            bounds.Inflate(tolerance, tolerance);
            if (!bounds.Contains(pt))
            {
                return false;
            }
            return Strokes.HitTest(pt, tolerance).Count > 0;
        }

        public bool HitTestSelectionBox(Point pt, double padding = 8.0)
        {
            if (Strokes == null || Strokes.Count == 0)
            {
                return false;
            }
            Rect bounds = Strokes.GetBounds();
            bounds.Inflate(padding, padding);
            return bounds.Contains(pt);
        }

        public void TranslateStrokes(double dx, double dy)
        {
            if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            {
                return;
            }
            var mat = new Matrix();
            mat.Translate(dx, dy);
            Strokes.Transform(mat, false);

            if (IsMerged && OriginalLayers != null)
            {
                foreach (var orig in OriginalLayers)
                {
                    orig.Strokes.Transform(mat, false);
                    orig.UpdateSelectionBounds();
                }
            }

            UpdateSelectionBounds();
        }

        public override void RenderTo(DrawingContext dc)
        {
            Strokes?.Draw(dc);
        }

        public override string DimensionsText
        {
            get
            {
                if (Strokes == null || Strokes.Count == 0) return "0 × 0 px";
                Rect b = Strokes.GetBounds();
                int bw = Math.Max(1, (int)Math.Round(b.Width));
                int bh = Math.Max(1, (int)Math.Round(b.Height));
                return IsMerged
                    ? $"{bw} × {bh} px • Merged ({OriginalLayers.Count})"
                    : $"{bw} × {bh} px • {(int)Math.Round(StrokeThickness)} px";
            }
        }

        public override FrameworkElement CreateThumbnailElement()
        {
            var thumbBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141414")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4, 0, 4, 0)
            };

            double h = Math.Clamp(Math.Round(StrokeThickness), 3.0, 14.0);
            var dot = new Border
            {
                Width = 16,
                Height = h,
                CornerRadius = new CornerRadius(Math.Min(2.0, h / 2.0)),
                Background = new SolidColorBrush(StrokeColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            thumbBorder.Child = dot;
            return thumbBorder;
        }

        public static StrokeLayerItem Create(Stroke stroke, MainWindow.StrokeShape shape, double canvasW, double canvasH, int zIndex, string name)
        {
            var item = new StrokeLayerItem
            {
                Name = name,
                StrokeColor = stroke.DrawingAttributes.Color,
                StrokeThickness = stroke.DrawingAttributes.Width,
                Shape = shape,
                ZIndex = zIndex
            };
            item.Strokes.Add(stroke);

            var rectGeo = new RectangleGeometry(new Rect(0, 0, canvasW, canvasH));

            var host = new Canvas
            {
                Width = canvasW,
                Height = canvasH,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                ClipToBounds = true,
                Clip = rectGeo
            };

            var presenter = new InkPresenter
            {
                Width = canvasW,
                Height = canvasH,
                IsHitTestVisible = false,
                ClipToBounds = true,
                Clip = rectGeo
            };
            presenter.Strokes = item.Strokes;
            host.Children.Add(presenter);

            var selBorder = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            host.Children.Add(selBorder);

            item.HostCanvas = host;
            item.Presenter = presenter;
            item.SelectionBorder = selBorder;
            item.UpdateSelectionBounds();

            Panel.SetZIndex(host, zIndex);
            return item;
        }

        public static StrokeLayerItem CreateMerged(IEnumerable<StrokeLayerItem> items, double canvasW, double canvasH, int zIndex, string name)
        {
            var itemList = items.ToList();
            var first = itemList.First();
            var item = new StrokeLayerItem
            {
                Name = name,
                StrokeColor = first.StrokeColor,
                StrokeThickness = first.StrokeThickness,
                Shape = first.Shape,
                ZIndex = zIndex,
                Opacity = itemList.Average(x => x.Opacity),
                OriginalLayers = new List<StrokeLayerItem>(itemList)
            };

            foreach (var it in itemList)
            {
                foreach (var s in it.Strokes)
                {
                    item.Strokes.Add(s.Clone());
                }
            }

            var rectGeo = new RectangleGeometry(new Rect(0, 0, canvasW, canvasH));

            var host = new Canvas
            {
                Width = canvasW,
                Height = canvasH,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                ClipToBounds = true,
                Clip = rectGeo
            };

            var presenter = new InkPresenter
            {
                Width = canvasW,
                Height = canvasH,
                IsHitTestVisible = false,
                ClipToBounds = true,
                Clip = rectGeo
            };
            presenter.Strokes = item.Strokes;
            host.Children.Add(presenter);

            var selBorder = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            host.Children.Add(selBorder);

            item.HostCanvas = host;
            item.Presenter = presenter;
            item.SelectionBorder = selBorder;
            item.UpdateSelectionBounds();

            Panel.SetZIndex(host, zIndex);
            return item;
        }
    }
}
