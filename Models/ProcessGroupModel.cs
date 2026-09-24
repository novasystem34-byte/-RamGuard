using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RamGuard.Models
{
    /// <summary>
    /// يجمع كل العمليات اللي عندها نفس الاسم (متل عمليات Brave الفرعية)
    /// تحت صف واحد قابل للتوسيع، بنفس فكرة تبويب "Processes" بتاسك مانجر.
    /// هذا هو اللي يخلي الأرقام تنطابق مع تاسك مانجر بدل ما يبين كل
    /// عملية فرعية لحالها وتصير المقارنة مربكة.
    /// </summary>
    public class ProcessGroupModel : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<ProcessInfoModel> Children { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnChanged(); }
        }

        public int Count => Children.Count;
        public double TotalMemoryMb => Children.Sum(c => c.MemoryMb);
        public double TotalCpuPercent => Children.Sum(c => c.CpuPercent);
        public bool HasSuspicious => Children.Any(c => c.IsSuspicious);

        public void RaiseAggregatesChanged()
        {
            OnChanged(nameof(Count));
            OnChanged(nameof(TotalMemoryMb));
            OnChanged(nameof(TotalCpuPercent));
            OnChanged(nameof(HasSuspicious));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
