using System;
using System.Windows;
using System.Windows.Media;

namespace ImageEditor
{
    public enum LayerType
    {
        Image,
        Stroke
    }

    public abstract class LayerItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Layer";
        public abstract LayerType Type { get; }
        public int ZIndex { get; set; }
        public bool IsLocked { get; set; }

        private double _opacity = 1.0;
        public virtual double Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Math.Clamp(value, 0.0, 1.0);
                if (VisualElement != null)
                {
                    VisualElement.Opacity = _opacity;
                }
            }
        }

        private bool _isVisible = true;
        public virtual bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                if (VisualElement != null)
                {
                    VisualElement.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public abstract bool IsSelected { get; set; }

        public abstract FrameworkElement VisualElement { get; }

        public abstract void RenderTo(DrawingContext dc);

        public abstract FrameworkElement CreateThumbnailElement();

        public abstract string DimensionsText { get; }
    }
}
