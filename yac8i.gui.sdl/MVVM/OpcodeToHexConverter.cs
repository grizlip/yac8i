using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace yac8i.gui.sdl.MVVM
{
    public class OpcodeToHexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ushort opcode)
            {
                return $"0x{opcode:X4}";
            }
            throw new ArgumentException($"{nameof(OpcodeToHexConverter)} Convert only ushort type supported. Provided: {value?.GetType().Name}");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex)
            {
                if (hex.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase) || hex.StartsWith("&H", StringComparison.CurrentCultureIgnoreCase))
                {
                    hex = hex.Substring(2);
                }
                ushort result;
                ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result);
                return result;
            }
            throw new ArgumentException($"{nameof(OpcodeToHexConverter)} ConvertBack only string type supported. Provided: {value?.GetType().Name}");
        }
    }
}