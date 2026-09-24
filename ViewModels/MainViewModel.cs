using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;
using RamGuard.Models;
using RamGuard.Services;

namespace RamGuard.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessMonitorService _monitor = new();
        private readonly MemoryLimiterService _limiter = new();
        private readonly HiddenProcessDetector _detector = new();
        private readonly DispatcherTimer _timer;

        // النسخة الفعلية للبيانات - مو معروضة مباشرة بالواجهة، بس مصدر
        // الحقيقة اللي منها نبني "Groups" كل تحديث.
        private readonly Dictionary<int, ProcessInfoModel> _processesByPid = new();

        // نحافظ على حالة الطي/التوسيع لكل مجموعة بين التحديثات (كل 1.5 ثانية
        // منعيد بناء القائمة من الصفر عشان نضمن المجاميع صحيحة دايمًا).
        private readonly HashSet<string> _expandedGroupNames = new();

        // نتجنب نكرر نفس التنبيه الصوتي/التوست كل تحديث لنفس العملية.
        private readonly HashSet<int> _alreadyAlertedPids = new();

        public ObservableCollection<ProcessGroupModel> Groups { get; } = new();

        /// <summary>يرفع حدث لما عملية تتجاوز حد التنبيه - الواجهة (MainWindow) تستخدمه لعرض بالون بالصينية.</summary>
        public event Action<string, double>? MemoryAlertRaised;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnChanged(); RebuildGroups(); }
        }

        private double _alertThresholdMb = 800;
        /// <summary>لو عملية وحدة (مو مجموعة) تجاوزت هاي القيمة بالميجابايت، تصير صوت + بالون تنبيه.</summary>
        public double AlertThresholdMb
        {
            get => _alertThresholdMb;
            set { _alertThresholdMb = value; OnChanged(); }
        }

        private bool _soundAlertsEnabled = true;
        public bool SoundAlertsEnabled
        {
            get => _soundAlertsEnabled;
            set { _soundAlertsEnabled = value; OnChanged(); }
        }

        private string _totalMemoryText = string.Empty;
        public string TotalMemoryText
        {
            get => _totalMemoryText;
            private set { _totalMemoryText = value; OnChanged(); }
        }

        private double _totalMemoryPercent;
        public double TotalMemoryPercent
        {
            get => _totalMemoryPercent;
            private set { _totalMemoryPercent = value; OnChanged(); }
        }

        private string _statusText = "جاهز";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnChanged(); }
        }

        public System.Windows.Input.ICommand TrimCommand { get; }
        public System.Windows.Input.ICommand ApplyLimitCommand { get; }
        public System.Windows.Input.ICommand RemoveLimitCommand { get; }
        public System.Windows.Input.ICommand RefreshNowCommand { get; }
        public System.Windows.Input.ICommand ToggleGroupCommand { get; }
        public System.Windows.Input.ICommand TrimGroupCommand { get; }

        public MainViewModel()
        {
            TrimCommand = new RelayCommand(p => Trim(p as ProcessInfoModel));
            ApplyLimitCommand = new RelayCommand(p => ApplyLimit(p as ProcessInfoModel));
            RemoveLimitCommand = new RelayCommand(p => RemoveLimit(p as ProcessInfoModel));
            RefreshNowCommand = new RelayCommand(_ => RefreshSnapshot());
            ToggleGroupCommand = new RelayCommand(g => ToggleGroup(g as ProcessGroupModel));
            TrimGroupCommand = new RelayCommand(g => TrimGroup(g as ProcessGroupModel));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += (_, _) => RefreshSnapshot();
            _timer.Start();

            RefreshSnapshot();
        }

        private void ToggleGroup(ProcessGroupModel? g)
        {
            if (g == null) return;
            g.IsExpanded = !g.IsExpanded;
            if (g.IsExpanded) _expandedGroupNames.Add(g.Name);
            else _expandedGroupNames.Remove(g.Name);
        }

        private void TrimGroup(ProcessGroupModel? g)
        {
            if (g == null) return;
            int okCount = 0;
            foreach (var child in g.Children)
                if (_limiter.TrimWorkingSet(child.Id)) okCount++;

            StatusText = $"تم تفريغ {okCount} من {g.Children.Count} عملية ضمن {g.Name}";
        }

        private void RefreshSnapshot()
        {
            var snapshot = _monitor.GetSnapshot();
            var suspicious = _detector.FindSuspiciousPids();
            var newIds = snapshot.Select(s => s.Id).ToHashSet();

            // احذف اللي خلص
            foreach (var deadPid in _processesByPid.Keys.Where(id => !newIds.Contains(id)).ToList())
            {
                _processesByPid.Remove(deadPid);
                _alreadyAlertedPids.Remove(deadPid);
            }

            foreach (var item in snapshot)
            {
                item.IsSuspicious = suspicious.Contains(item.Id);

                if (_processesByPid.TryGetValue(item.Id, out var existing))
                {
                    existing.MemoryMb = item.MemoryMb;
                    existing.CpuPercent = item.CpuPercent;
                    existing.IsSuspicious = item.IsSuspicious;
                    existing.FullPath = item.FullPath;
                }
                else
                {
                    _processesByPid[item.Id] = item;
                }

                CheckAlertThreshold(_processesByPid[item.Id]);
            }

            RebuildGroups();
            UpdateTotalMemoryBar();
        }

        private void CheckAlertThreshold(ProcessInfoModel p)
        {
            if (AlertThresholdMb <= 0) return;

            if (p.MemoryMb >= AlertThresholdMb)
            {
                if (!_alreadyAlertedPids.Contains(p.Id))
                {
                    _alreadyAlertedPids.Add(p.Id);
                    if (SoundAlertsEnabled)
                    {
                        try { SystemSounds.Exclamation.Play(); } catch { /* تجاهل لو ما فيه جهاز صوت */ }
                    }
                    MemoryAlertRaised?.Invoke(p.Name, p.MemoryMb);
                    StatusText = $"تنبيه: {p.Name} (PID {p.Id}) تجاوزت {AlertThresholdMb:N0} MB";
                }
            }
            else
            {
                // رجعت تحت الحد - نسمح بتنبيه جديد لو رجعت تتجاوزه مرة ثانية
                _alreadyAlertedPids.Remove(p.Id);
            }
        }

        /// <summary>يبني قائمة المجموعات من الصفر كل مرة، عشان نضمن المجاميع (الرام/المعالج) صحيحة 100% دايمًا.</summary>
        private void RebuildGroups()
        {
            IEnumerable<ProcessInfoModel> source = _processesByPid.Values;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                source = source.Where(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString().Contains(SearchText));
            }

            var grouped = source
                .GroupBy(p => p.Name)
                .Select(g =>
                {
                    var groupModel = new ProcessGroupModel
                    {
                        Name = g.Key,
                        IsExpanded = _expandedGroupNames.Contains(g.Key)
                    };
                    foreach (var child in g.OrderByDescending(c => c.MemoryMb))
                        groupModel.Children.Add(child);
                    return groupModel;
                })
                .OrderByDescending(g => g.TotalMemoryMb)
                .ToList();

            Groups.Clear();
            foreach (var g in grouped)
                Groups.Add(g);
        }

        private void UpdateTotalMemoryBar()
        {
            var status = new NativeMethods.MEMORYSTATUSEX
            {
                dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
            };

            if (NativeMethods.GlobalMemoryStatusEx(ref status))
            {
                double totalGb = status.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
                double usedGb = totalGb - (status.ullAvailPhys / 1024.0 / 1024.0 / 1024.0);
                TotalMemoryPercent = status.dwMemoryLoad;
                TotalMemoryText = $"{usedGb:F1} / {totalGb:F1} GB  ({status.dwMemoryLoad}%)";
            }
        }

        private void Trim(ProcessInfoModel? p)
        {
            if (p == null) return;
            bool ok = _limiter.TrimWorkingSet(p.Id);
            StatusText = ok
                ? $"تم تفريغ ذاكرة {p.Name} (PID {p.Id})"
                : $"فشل تفريغ الذاكرة لـ {p.Name} — جرّب تشغيل البرنامج كـ Administrator";
        }

        private void ApplyLimit(ProcessInfoModel? p)
        {
            if (p == null) return;
            if (p.LimitMb <= 0)
            {
                StatusText = "حدد قيمة أكبر من صفر بخانة الحد أولاً";
                return;
            }

            long bytes = (long)(p.LimitMb * 1024 * 1024);
            var result = _limiter.ApplyHardLimit(p.Id, bytes);

            switch (result)
            {
                case LimitResult.Success:
                    p.IsHardLimited = true;
                    StatusText = $"تم تحديد سقف {p.LimitMb} MB لـ {p.Name} — تنبيه: تجاوز الحد قد يعلّق أو يكرش البرنامج";
                    break;
                case LimitResult.AccessDenied:
                    StatusText = "الصلاحيات غير كافية — شغّل التطبيق كـ Administrator";
                    break;
                case LimitResult.AlreadyInAnotherJob:
                    StatusText = $"{p.Name} منتمية أصلًا لمجموعة Job ثانية — ما قدرنا نربطها";
                    break;
                default:
                    StatusText = $"تعذّر تطبيق الحد على {p.Name}";
                    break;
            }
        }

        private void RemoveLimit(ProcessInfoModel? p)
        {
            if (p == null) return;
            if (_limiter.RemoveHardLimit(p.Id))
            {
                p.IsHardLimited = false;
                StatusText = $"تم رفع الحد عن {p.Name}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Shutdown()
        {
            _timer.Stop();
            _limiter.Cleanup();
        }
    }
}
