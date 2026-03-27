using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace logger_client.Converters
{
    public class LevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ErrorLevel level)
            {
                switch (level)
                {
                    case ErrorLevel.Critical:
                        return new SolidColorBrush(Color.FromRgb(220, 38, 38)); // red-600
                    case ErrorLevel.Error:
                        return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // red-500
                    case ErrorLevel.Warning:
                        return new SolidColorBrush(Color.FromRgb(234, 179, 8)); // yellow-500
                    case ErrorLevel.Information:
                        return new SolidColorBrush(Color.FromRgb(59, 130, 246)); // blue-500
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Hidden;
            }
            return Visibility.Hidden;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 3. 리스트 순번 표시 (0, 1, 2... -> 1, 2, 3...)
    public class IndexPlusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index) return (index + 1).ToString();
            return "1";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public sealed class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null && parameter != null && value.ToString() == parameter.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? parameter! : Binding.DoNothing;
    }

    // 상태 컬러 표시(rag, ai)
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public Brush Ready { get; set; } = Brushes.LimeGreen;
        public Brush NotReady { get; set; } = Brushes.IndianRed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.Equals(text, "AI Ready", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Connected", StringComparison.OrdinalIgnoreCase))
            {
                return Ready;
            }

            return NotReady;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}