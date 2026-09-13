using System;
using System.Globalization;
using System.Windows.Data;

namespace PizarraPro.Converters;

/// <summary>Permite enlazar un RadioButton.IsChecked a un valor concreto de un enum.</summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is not null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }

        return Binding.DoNothing;
    }
}
