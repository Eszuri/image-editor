using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public class OverlayImageItem : LayerItem
    {
        public override LayerType Type => LayerType.Image;

        public BitmapSource Source { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double AspectRatio => Height > 0 ? Width / Height : 1.0;

        public Grid ImageVisualElement { get; set; } = null!;
        public override FrameworkElement VisualElement => ImageVisualElement;

        public Image ImageControl { get; set; } = null!;
        public Border SelectionBorder { get; set; } = null!;
        public Border HandleTL { get; set; } = null!;
        public Border HandleTR { get; set; } = null!;
        public Border HandleBL { get; set; } = null!;
        public Border HandleBR { get; set; } = null!;

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                var vis = value ? Visibility.Visible : Visibility.Collapsed;
                if (SelectionBorder != null)
                {
                    SelectionBorder.Visibility = vis;
                    SelectionBorder.BorderBrush = IsLocked
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5A93C"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
                }
                var handleVis = (value && !IsLocked) ? Visibility.Visible : Visibility.Collapsed;
                if (HandleTL != null) HandleTL.Visibility = handleVis;
                if (HandleTR != null) HandleTR.Visibility = handleVis;
                if (HandleBL != null) HandleBL.Visibility = handleVis;
                if (HandleBR != null) HandleBR.Visibility = handleVis;
            }
        }

        public override void RenderTo(DrawingContext dc)
        {
            if (Source != null && Width > 0 && Height > 0)
            {
                dc.DrawImage(Source, new Rect(X, Y, Width, Height));
            }
        }

        public override string DimensionsText => $"{(int)Math.Round(Width)} × {(int)Math.Round(Height)} px";

        public override FrameworkElement CreateThumbnailElement()
        {
            var thumbBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F0F0F")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Margin = new Thickness(4, 0, 4, 0)
            };

            var thumbImg = new Image
            {
                Source = Source,
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.LowQuality);
            thumbBorder.Child = thumbImg;
            return thumbBorder;
        }
    }
}
