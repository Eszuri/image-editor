using System.ComponentModel;

namespace ImageEditor
{
    public enum BatchItemStatus
    {
        Waiting,
        Processing,
        Completed,
        Skipped,
        Error
    }

    public class BatchItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public long OriginalSize { get; set; }
        public string OriginalSizeFormatted => ImageCompressor.FormatBytes(OriginalSize);
        public int Width { get; set; }
        public int Height { get; set; }
        public string ResolutionFormatted => (Width > 0 && Height > 0) ? $"{Width} × {Height}" : "-";

        private long _newSize;
        public long NewSize
        {
            get => _newSize;
            set
            {
                _newSize = value;
                OnPropertyChanged(nameof(NewSize));
                OnPropertyChanged(nameof(NewSizeFormatted));
                OnPropertyChanged(nameof(ReductionFormatted));
            }
        }

        public string NewSizeFormatted => _newSize > 0 ? ImageCompressor.FormatBytes(_newSize) : "-";

        public string ReductionFormatted
        {
            get
            {
                if (_newSize <= 0 || OriginalSize <= 0) return "";
                double diff = (1.0 - ((double)_newSize / OriginalSize)) * 100.0;
                if (diff > 0.5) return $"-{diff:F0}%";
                if (diff < -0.5) return $"+{-diff:F0}%";
                return "0%";
            }
        }

        private BatchItemStatus _status = BatchItemStatus.Waiting;
        public BatchItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string StatusText => _status switch
        {
            BatchItemStatus.Waiting => "Waiting",
            BatchItemStatus.Processing => "Processing...",
            BatchItemStatus.Completed => "Completed",
            BatchItemStatus.Skipped => "Skipped",
            BatchItemStatus.Error => "Error",
            _ => "Waiting"
        };

        public string StatusColor => _status switch
        {
            BatchItemStatus.Waiting => "#888888",
            BatchItemStatus.Processing => "#0078D4",
            BatchItemStatus.Completed => "#4CAF50",
            BatchItemStatus.Skipped => "#FFA726",
            BatchItemStatus.Error => "#E53935",
            _ => "#888888"
        };

        public string ErrorMessage { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
