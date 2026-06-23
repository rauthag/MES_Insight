using System.Collections.ObjectModel;
using System.ComponentModel;
using MESInsight.Core;

namespace MESInsight.UI
{
    public class AssemblyNodeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isExpanded = true;
        private bool _showHistory = false;

        public string Uid { get; set; }
        public string Result { get; set; }
        public ObservableCollection<AssemblyNodeViewModel> Children { get; } = new ObservableCollection<AssemblyNodeViewModel>();
        public System.Collections.Generic.List<ResponseRecord> History { get; set; } = new System.Collections.Generic.List<ResponseRecord>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); }
        }

        public bool ShowHistory
        {
            get => _showHistory;
            set { _showHistory = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowHistory))); }
        }

        public System.Windows.Visibility HistoryVisibility =>
            ShowHistory ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string ResultBadgeColor
        {
            get
            {
                switch (Result?.ToUpper())
                {
                    case "Y": case "P": return "#2EA043";
                    case "N": case "F": return "#F85149";
                    default: return "#6E7681";
                }
            }
        }

        public string ResultDisplay => string.IsNullOrEmpty(Result) ? "?" : Result.ToUpper();

        public void ToggleHistory()
        {
            ShowHistory = !ShowHistory;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HistoryVisibility)));
        }
    }
}