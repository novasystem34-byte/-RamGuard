using System;
using System.Collections.Generic;

namespace RamGuard.Services
{
    public enum LimitResult
    {
        Success,
        ProcessNotFound,
        AccessDenied,
        AlreadyInAnotherJob,
        Failed
    }

    /// <summary>
    /// وضعين للتعامل مع الذاكرة:
    ///
    /// 1) TrimWorkingSet — آمن تمامًا: يطلب من ويندوز يفرّغ الصفحات
    ///    الفيزيائية غير المستخدمة للعملية (تنتقل لملف الصفحات أو تُحرَّر).
    ///    البرنامج يقدر ياخذ رام من جديد بأي وقت بدون مشاكل.
    ///
    /// 2) ApplyHardLimit — عبر Job Objects: سقف صارم. إذا حاولت العملية
    ///    تتجاوزه، تخصيص الذاكرة الجديد **يفشل** من جهة ويندوز، وأغلب
    ///    البرامج ما بتتعامل مع هذا بلطف (ممكن تكرش أو تعلّق). استخدمه
    ///    بحذر وبس على برامج ثانوية مو حساسة.
    /// </summary>
    public class MemoryLimiterService
    {
        // نحتفظ بمقبض الـ Job لكل PID عشان نقدر نعدّل الحد لاحقًا
        // بدون الحاجة نعيد ربط العملية.
        private readonly Dictionary<int, IntPtr> _jobHandles = new();

        public bool TrimWorkingSet(int pid)
        {
            IntPtr handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_OPERATION,
                false, pid);

            if (handle == IntPtr.Zero)
                return false;

            try
            {
                return NativeMethods.EmptyWorkingSet(handle);
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        public LimitResult ApplyHardLimit(int pid, long limitBytes)
        {
            // لو فيه Job سابق لنفس العملية، بس نحدّث رقم الحد بدون إعادة ربط
            if (_jobHandles.TryGetValue(pid, out IntPtr existingJob))
            {
                return SetLimitOnJob(existingJob, limitBytes) ? LimitResult.Success : LimitResult.Failed;
            }

            IntPtr processHandle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_SET_QUOTA | NativeMethods.PROCESS_TERMINATE | NativeMethods.PROCESS_QUERY_INFORMATION,
                false, pid);

            if (processHandle == IntPtr.Zero)
                return LimitResult.AccessDenied;

            try
            {
                IntPtr job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero)
                    return LimitResult.Failed;

                if (!NativeMethods.AssignProcessToJobObject(job, processHandle))
                {
                    // غالبًا العملية منتمية أصلًا لـ Job ثاني (متصفحات كروم/UWP
                    // كثيرًا يسوّون هذا لعملياتهم الفرعية). على ويندوز 8+
                    // الـ Nested Jobs مدعومة عادة، فإذا فشل هنا الاحتمال الأكبر
                    // صلاحيات غير كافية.
                    NativeMethods.CloseHandle(job);
                    return LimitResult.AlreadyInAnotherJob;
                }

                if (!SetLimitOnJob(job, limitBytes))
                {
                    NativeMethods.CloseHandle(job);
                    return LimitResult.Failed;
                }

                _jobHandles[pid] = job;
                return LimitResult.Success;
            }
            finally
            {
                NativeMethods.CloseHandle(processHandle);
            }
        }

        /// <summary>
        /// "إزالة" الحد فعليًا مو ممكنة 100% (العملية تضل بالـ Job جسديًا)،
        /// فالحل العملي: نرفع السقف لأعلى قيمة ممكنة فيصير بلا تأثير عملي.
        /// </summary>
        public bool RemoveHardLimit(int pid)
        {
            if (_jobHandles.TryGetValue(pid, out IntPtr job))
                return SetLimitOnJob(job, long.MaxValue);
            return true; // ما كان فيه حد أصلًا
        }

        private static bool SetLimitOnJob(IntPtr job, long limitBytes)
        {
            var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_PROCESS_MEMORY
                },
                ProcessMemoryLimit = (UIntPtr)(ulong)limitBytes
            };

            int size = System.Runtime.InteropServices.Marshal.SizeOf(info);
            return NativeMethods.SetInformationJobObject(
                job,
                NativeMethods.JobObjectInfoType.ExtendedLimitInformation,
                ref info,
                (uint)size);
        }

        public void Cleanup()
        {
            foreach (var handle in _jobHandles.Values)
                NativeMethods.CloseHandle(handle);
            _jobHandles.Clear();
        }
    }
}
