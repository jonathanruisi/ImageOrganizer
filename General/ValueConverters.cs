using ImageOrganizer.Controls;
using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI.ViewModel;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ImageOrganizer
{
    public class RatingAdjustmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not int rating)
                throw new ArgumentException("Object must be an integer", nameof(value));

            return (double)(rating == 0 ? -1 : rating);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is not double rating)
                throw new ArgumentException("Object must be a double", nameof(value));

            return (int)(rating < 0 ? 0 : rating);
        }
    }

    public class RatingToSolidColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value switch
            {
                5 => new SolidColorBrush(Colors.Orange),
                4 => new SolidColorBrush(Colors.HotPink),
                3 => new SolidColorBrush(Colors.CornflowerBlue),
                2 => new SolidColorBrush(Colors.LimeGreen),
                1 => new SolidColorBrush(Colors.LightGray),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class DecimalTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var formatString = parameter as string ?? "0.##";
            return value is IFormattable formattable
                ? formattable.ToString(formatString, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var text = value as string;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var result))
            {
                return result;
            }

            return 0d;
        }
    }

    public sealed class FlagMaskConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (!int.TryParse(parameter as string, out int flag))
                throw new ArgumentNullException(nameof(parameter));

            flag--;
            if (flag is < 0 or > 63)
                throw new ArgumentOutOfRangeException(nameof(parameter));

            ulong flagValue;
            if (value is ViewModelElement element)
                flagValue = element.Flags;
            else if (value is ulong ulValue)
                flagValue = ulValue;
            else return null;

            var result = (flagValue & (1UL << flag)) != 0;
            if (targetType == typeof(Visibility))
                return result ? Visibility.Visible : Visibility.Collapsed;
            else return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ImageTransformToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ImageTransform transform && transform != default)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
