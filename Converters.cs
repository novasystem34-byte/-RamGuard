using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RamGuard
{
    /// <summary>يحوّل نسبة مئوية (0-100) إلى عرض بالبكسل ضمن شريط بعرض ثابت تقريبي.</summary>
    public class PercentToWidthConverter : IValueConverter
    {
        // عرض الحاوية التقريبي بالـ XAML (Grid.Column="1" بشريط الذاكرة)
        private const double ContainerWidth = 560;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double percent = value is double d ? d : 0;
            percent = Math.Clamp(percent, 0, 100);
            return ContainerWidth * (percent / 100.0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>سهم توسيع/طي المجموعة: ▼ لما تكون موسّعة، ▶ لما تكون مطوية.</summary>
    public class BoolToExpandArrowConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? "▼" : "◀";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
