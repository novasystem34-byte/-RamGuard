using System;
using System.Collections.Generic;
using System.Diagnostics;
using RamGuard.Models;

namespace RamGuard.Services
{
    /// <summary>
    /// يقرأ لائحة العمليات الحية كل مرة يُطلب منه، ويحسب نسبة استخدام
    /// المعالج بمقارنة وقت المعالج التراكمي بين قراءتين متتاليتين.
    /// </summary>
    public class ProcessMonitorService
    {
        private readonly Dictionary<int, (TimeSpan cpuTime, DateTime sampledAt)> _lastSample = new();

        public List<ProcessInfoModel> GetSnapshot()
        {
            var result = new List<ProcessInfoModel>();
            var now = DateTime.UtcNow;

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    double memMb = proc.WorkingSet64 / 1024.0 / 1024.0;
                    double cpuPercent = 0;

                    TimeSpan currentCpu = TimeSpan.Zero;
                    bool haveCpu = true;
                    try { currentCpu = proc.TotalProcessorTime; }
                    catch { haveCpu = false; } // عمليات نظام محمية بدون صلاحية كافية

                    if (haveCpu && _lastSample.TryGetValue(proc.Id, out var prev))
                    {
                        double elapsedMs = (now - prev.sampledAt).TotalMilliseconds;
                        double cpuMs = (currentCpu - prev.cpuTime).TotalMilliseconds;
                        if (elapsedMs > 0)
                            cpuPercent = Math.Max(0, (cpuMs / elapsedMs) * 100.0 / Environment.ProcessorCount);
                    }

                    if (haveCpu)
                        _lastSample[proc.Id] = (currentCpu, now);

                    string path = string.Empty;
                    try { path = proc.MainModule?.FileName ?? string.Empty; }
                    catch { /* عمليات نظام ما نقدر نقرأ مسارها بدون رفع صلاحيات */ }

                    result.Add(new ProcessInfoModel
                    {
                        Id = proc.Id,
                        Name = proc.ProcessName,
                        FullPath = path,
                        MemoryMb = Math.Round(memMb, 1),
                        CpuPercent = Math.Round(cpuPercent, 1)
                    });
                }
                catch
                {
                    // عملية انتهت أثناء القراءة أو ما فيه صلاحية - تجاهلها بهدوء
                }
            }

            return result;
        }
    }
}
