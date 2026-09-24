using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace RamGuard.Services
{
    /// <summary>
    /// أيقونة صينية النظام. بنستخدم NotifyIcon من WinForms لأن WPF ما
    /// عنده مكافئ أصلي له - هذا شي طبيعي ومعتمد بكثير تطبيقات WPF.
    /// الأيقونة نفسها منسحبة من ملف الـ exe مباشرة (نفس الأيقونة اللي
    /// محددة بـ ApplicationIcon بالـ csproj)، فما نحتاج ملف منفصل وقت التشغيل.
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;

        public event EventHandler? OpenRequested;
        public event EventHandler? ExitRequested;

        public TrayIconService()
        {
            Icon appIcon;
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                appIcon = Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
            }
            catch
            {
                appIcon = SystemIcons.Application;
            }

            var menu = new ContextMenuStrip();
            menu.Items.Add("فتح RAM Guard", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("خروج نهائي", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "RAM Guard",
                Visible = true,
                ContextMenuStrip = menu
            };

            _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Warning)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(4000);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
