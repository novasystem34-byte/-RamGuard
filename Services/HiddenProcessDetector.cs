using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RamGuard.Services
{
    /// <summary>
    /// ملاحظة مهمة بالصدق: هذا مو كاشف Rootkit حقيقي. Task Manager نفسه
    /// يعرض كل شي يظهر بواجهة NtQuerySystemInformation، وهذا اللي
    /// Process.GetProcesses() يستخدمه أصلًا بالخلف.
    ///
    /// اللي نقدر نسويه فعليًا بدون kernel driver: نقارن 3 طرق تعداد
    /// مختلفة (CLR / psapi EnumProcesses / Toolhelp32 Snapshot). لو PID
    /// ظهر بطريقة وما ظهر بثانية، هذا مؤشر تناقض يستاهل انتباه -
    /// لكنه ليس دليل قاطع على إخفاء متعمد (ممكن يكون العملية طلعت
    /// بالضبط أثناء لحظة المقارنة).
    /// </summary>
    public class HiddenProcessDetector
    {
        public HashSet<int> FindSuspiciousPids()
        {
            var fromClr = GetFromClr();
            var fromPsapi = GetFromPsapi();
            var fromToolhelp = GetFromToolhelp();

            var suspicious = new HashSet<int>();

            void FlagMissing(HashSet<int> reference, HashSet<int> other)
            {
                foreach (var pid in reference)
                    if (!other.Contains(pid))
                        suspicious.Add(pid);
            }

            // أي PID ناقص من إحدى الطرق الثلاث يتعلّم كمشبوه
            FlagMissing(fromClr, fromPsapi);
            FlagMissing(fromClr, fromToolhelp);
            FlagMissing(fromPsapi, fromClr);
            FlagMissing(fromToolhelp, fromClr);

            return suspicious;
        }

        private static HashSet<int> GetFromClr()
        {
            var set = new HashSet<int>();
            foreach (var p in Process.GetProcesses())
                set.Add(p.Id);
            return set;
        }

        private static HashSet<int> GetFromPsapi()
        {
            var set = new HashSet<int>();
            uint arraySize = 4096 * sizeof(uint);
            var pids = new uint[4096];

            if (NativeMethods.EnumProcesses(pids, arraySize, out uint bytesReturned))
            {
                int count = (int)(bytesReturned / sizeof(uint));
                for (int i = 0; i < count; i++)
                    if (pids[i] != 0)
                        set.Add((int)pids[i]);
            }
            return set;
        }

        private static HashSet<int> GetFromToolhelp()
        {
            var set = new HashSet<int>();
            IntPtr snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (snapshot == NativeMethods.INVALID_HANDLE_VALUE || snapshot == IntPtr.Zero)
                return set;

            try
            {
                var entry = new NativeMethods.PROCESSENTRY32
                {
                    dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.PROCESSENTRY32>()
                };

                if (NativeMethods.Process32First(snapshot, ref entry))
                {
                    do
                    {
                        set.Add((int)entry.th32ProcessID);
                    } while (NativeMethods.Process32Next(snapshot, ref entry));
                }
            }
            finally
            {
                NativeMethods.CloseHandle(snapshot);
            }

            return set;
        }
    }
}
