using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace yac8i.gui.sdl.MVVM
{

    public class BoolToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool state)
            {
                return state ? parameter : string.Empty;
            }
            throw new ArgumentException($"{nameof(BoolToStringConverter)} Convert only bool type supported. Provided: {value.GetType().Name}");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
            //throw new ArgumentException($"{nameof(OpcodeToHexConverter)} ConvertBack only string type supported. Provided: {value.GetType().Name}");
        }
    }
}