using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RamGuard.Models
{
    /// <summary>يمثل صف واحد بجدول العمليات بالواجهة.</summary>
    public class ProcessInfoModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;

        private double _memoryMb;
        public double MemoryMb
        {
            get => _memoryMb;
            set { _memoryMb = value; OnChanged(); }
        }

        private double _cpuPercent;
        public double CpuPercent
        {
            get => _cpuPercent;
            set { _cpuPercent = value; OnChanged(); }
        }

        private double _limitMb;
        /// <summary>القيمة اللي يكتبها المستخدم بخانة "الحد" (بالميجابايت). صفر = بدون حد.</summary>
        public double LimitMb
        {
            get => _limitMb;
            set { _limitMb = value; OnChanged(); }
        }

        private bool _isHardLimited;
        public bool IsHardLimited
        {
            get => _isHardLimited;
            set { _isHardLimited = value; OnChanged(); }
        }

        private bool _isSuspicious;
        /// <summary>true لو العملية ظهرت بطريقة تعداد وما ظهرت بطريقة ثانية.</summary>
        public bool IsSuspicious
        {
            get => _isSuspicious;
            set { _isSuspicious = value; OnChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
