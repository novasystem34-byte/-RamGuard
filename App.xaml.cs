using System.Security.Principal;
// ملاحظة: بسبب تفعيل UseWindowsForms جنب UseWPF (لازم لأيقونة الصينية)
// صارت أسماء متل Application و MessageBox موجودة بمساحتين أسماء بنفس
// الوقت (System.Windows و System.Windows.Forms)، فلازم نحددها بالكامل
// (Fully-qualified) بدل ما نعتمد على "using" وحده.

namespace RamGuard
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!IsRunningAsAdministrator())
            {
                System.Windows.MessageBox.Show(
                    "التطبيق يشتغل بدون صلاحيات Administrator.\n" +
                    "أغلب ميزات التطبيق (خصوصًا تحديد سقف الذاكرة والتعامل مع عمليات النظام) " +
                    "ما راح تشتغل بشكل كامل. يفضّل تشغيل التطبيق كـ Administrator.",
                    "RAM Guard",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
