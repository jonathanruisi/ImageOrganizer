using ImageOrganizer.Controls;
using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI.ViewModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

using System;
using System.Collections.Generic;
using System.Text;

namespace ImageOrganizer
{
    public class FlagMaskConverter : IValueConverter
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

    public class InteractionModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is InteractionMode mode
                   && mode == Enum.Parse<InteractionMode>((string)parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is not bool b || parameter is not string s)
                throw new ArgumentException("Invalid value or parameter");
            if (!Enum.TryParse<InteractionMode>(s, out var mode))
                throw new ArgumentException("Invalid parameter");
            return b ? mode : InteractionMode.None;
        }
    }

    public class ImageTransformToVisibilityConverter : IValueConverter
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
