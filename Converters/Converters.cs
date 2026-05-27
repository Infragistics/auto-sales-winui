using System;
using System.Globalization;
using AutoSales.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AutoSales.Converters
{
    /// <summary>
    /// Returns max/5 for axis-interval bindings. Mirrors the WPF IntervalConverter.
    /// </summary>
    public class IntervalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d && !double.IsNaN(d))
                return d / 5;
            return 0d;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Picks a numeric format string based on the current MeasureType (Revenue → "M" formatting).
    /// </summary>
    public class MeasureTypeStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MeasureType mt && mt == MeasureType.Revenue)
                return "{0:#,#0,,.# M}";
            return "{0}";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Picks a numeric format string based on magnitude (M for millions, K for thousands).
    /// </summary>
    public class ValueToStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double || value is int)
            {
                double v = System.Convert.ToDouble(value);
                if (v != 0 && v / 1_000_000 > 1) return "#,#0,, M ";
                if (v != 0 && v / 1_000 > 1) return "#,##0,.# K";
            }
            return "{0}";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → Visibility. WinUI Visibility is just Visible/Collapsed.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Formats a date as MM/dd/yyyy (en) or yyyy/MM/dd (ja-JP).
    /// </summary>
    public class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
            {
                var ui = CultureInfo.CurrentUICulture.Name;
                return dt.ToString(ui == "ja-JP" ? "yyyy/MM/dd" : "MM/dd/yyyy");
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → SolidColorBrush (gray for true, blue for false). Mirrors the WPF "is target reached" coloring.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && b
                ? new SolidColorBrush(Color.FromArgb(0xFF, 0x5A, 0x5A, 0x5A))
                : new SolidColorBrush(Color.FromArgb(0xFF, 0x05, 0xA0, 0xFA));
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => parameter;
    }

    /// <summary>
    /// bool → "M" / "F" gender label (the WPF version returned a brand image; we use a plain glyph
    /// so the port doesn't carry image assets it doesn't strictly need).
    /// </summary>
    public class BoolToGenderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b) return b ? "M" : "F";
            return string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Brand-name → string passthrough (the WPF version returned a brand image; the WinUI port
    /// uses the model name directly).
    /// </summary>
    public class StringToBrandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => value?.ToString() ?? string.Empty;
        public object ConvertBack(object value, Type targetType, object parameter, string language) => parameter;
    }
}
