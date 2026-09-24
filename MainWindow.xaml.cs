using System.ComponentModel;
using System.Windows;
using RamGuard.Services;
using RamGuard.ViewModels;

namespace RamGuard
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();
        private readonly TrayIconService _tray = new();
        private bool _isExiting;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;

            _viewModel.MemoryAlertRaised += (name, mb) =>
                _tray.ShowBalloon("RAM Guard - تنبيه استهلاك", $"{name} تجاوزت {mb:N0} MB");

            _tray.OpenRequested += (_, _) => RestoreFromTray();
            _tray.ExitRequested += (_, _) =>
            {
                _isExiting = true;
                Close();
            };

            StateChanged += (_, _) =>
            {
                // تصغير للصينية بدل شريط المهام
                if (WindowState == WindowState.Minimized)
                    Hide();
            };

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExiting)
            {
                _tray.Dispose();
                _viewModel.Shutdown();
                return;
            }

            // زر الإغلاق (X) يصغّر للصينية بدل ما يقفل البرنامج فعليًا.
            // الخروج الفعلي بس من قائمة الصينية -> "خروج نهائي".
            e.Cancel = true;
            Hide();
            _tray.ShowBalloon("RAM Guard", "البرنامج ضل شغال بالخلفية - اضغط أيقونة الصينية للرجوع أو للخروج نهائيًا.");
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
    }
}
